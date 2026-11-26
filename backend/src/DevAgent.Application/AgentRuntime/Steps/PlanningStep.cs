using DevAgent.Application.Abstractions.Ai;
using DevAgent.Application.Abstractions.Integrations;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.AgentRuntime.Budget;
using DevAgent.Application.AgentRuntime.Planner;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Tasks;
using DevAgent.Application.Options;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Approvals.Repositories;
using DevAgent.Domain.Common;
using DevAgent.Domain.Executions;
using DevAgent.Domain.Executions.Repositories;
using DevAgent.Domain.Policies;
using DevAgent.Domain.Tasks;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.AgentRuntime.Steps;

/// <summary>
/// PLANNING → NEEDS_CLARIFICATION | WAITING_FOR_APPROVAL | IN_PROGRESS.
/// Calls the Planner within budget, applies the repository policy to the plan, then the workflow approval rule.
/// Attempts count only failures in the current planning round; history and cost of earlier rounds are kept.
/// </summary>
internal sealed class PlanningStep(
    IExecutionRepository executions,
    IApprovalRepository approvals,
    IJobQueue jobQueue,
    IUnitOfWork unitOfWork,
    ISourceControlGateway sourceControl,
    IPlannerAgent planner,
    BudgetGuard budget,
    IOptions<AgentLimitsOptions> limits,
    TimeProvider clock) : IWorkflowStep
{
    public string Name => "planner";

    public bool CanHandle(AgentTaskStatus status) => status == AgentTaskStatus.Planning;

    public async Task ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (task, repository, workflow, workerId) = context;
        var options = limits.Value;

        var roundStartedAt = task.CurrentPlanningStartedAt ?? task.CreatedAt;
        var failures = await executions.CountFailuresSinceAsync(task.Id, AgentStep.Planner, roundStartedAt, cancellationToken);
        if (failures >= workflow.PlannerMaxAttempts)
        {
            task.Fail(
                $"Planner thất bại {failures} lần trong lượt lập kế hoạch này (giới hạn {workflow.PlannerMaxAttempts} theo quy trình).",
                StepActors.Orchestrator,
                clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var coordinates = new RepositoryCoordinates(repository.InstallationId, repository.Owner, repository.Name);
        var files = await sourceControl.ListFilesAsync(coordinates, task.BaseCommitSha!, options.PlannerMaxFiles, cancellationToken);

        var model = workflow.Models.Planner ?? planner.DefaultModel();
        var draft = planner.BuildRequest(new PlannerInput(task, repository, files), model, options.MaxOutputTokensPerCall);

        // The budget is checked before the call and decides this call's output cap.
        var grant = await budget.ReserveAsync(task.Id, model, BudgetGuard.EstimateInputTokens(draft), draft.MaxOutputTokens, cancellationToken);
        if (!grant.Allowed)
        {
            task.Block(grant.Reason!, StepActors.Orchestrator, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var attempt = await executions.CountAttemptsAsync(task.Id, AgentStep.Planner, cancellationToken) + 1;
        var execution = AgentExecution.Start(task.Id, AgentStep.Planner, attempt, null, model, workerId, clock.GetUtcNow());
        execution.RecordModelCall(TokenUsage.Zero);
        executions.Add(execution);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        PlannerResult result;
        try
        {
            result = await planner.ExecuteAsync(draft with { MaxOutputTokens = grant.MaxOutputTokens }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            execution.Fail(ex.Message, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }

        // Record cost before touching the task so it is saved even if the task is cancelled concurrently.
        var usage = result.Response.Usage;
        execution.RecordUsage(new TokenUsage(usage.InputTokens, usage.OutputTokens, usage.CacheReadTokens, usage.EstimatedCostUsd));
        DevAgentTelemetry.ModelTokens.Add(usage.InputTokens + usage.OutputTokens, new KeyValuePair<string, object?>("step", Name));

        var now = clock.GetUtcNow();
        if (result.Response.StopReason == ModelStopReason.Refusal)
        {
            execution.Fail($"Model từ chối yêu cầu: {result.Response.StopDetail ?? "không rõ lý do"}.", now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            task.Block("Model từ chối lập kế hoạch cho yêu cầu này; cần người phụ trách xem xét.", StepActors.Planner, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        if (result.Output is null)
        {
            var reason = result.Response.StopReason == ModelStopReason.MaxTokens
                ? $"Kế hoạch bị cắt do chạm trần {grant.MaxOutputTokens} token đầu ra."
                : result.ParseError ?? "Không đọc được kế hoạch.";
            execution.Fail(reason, now);
            await jobQueue.EnqueueAsync(RetryJob(task, attempt), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        execution.Succeed(now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var output = result.Output;
        if (output.NeedsClarification)
        {
            task.RequestClarification(output.ClarificationQuestion!, StepActors.Planner, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        // The system decides (repository policy + workflow), never the model.
        var changes = output.AffectedPaths.Select(p => new ProposedChange(p, ChangeKind.Modify)).ToArray();
        var decision = ChangePolicyEvaluator.Evaluate(repository.Policy, changes);
        var plan = task.AttachPlan(output.Summary, output.Steps, output.AcceptanceCriteria, output.Risks, output.AffectedPaths, decision, now);

        if (!workflow.RequiresPlanApproval(decision))
        {
            task.StartCoding(
                StepActors.Orchestrator,
                $"Kế hoạch v{plan.Version} nằm trong phạm vi tự động (chính sách v{decision.PolicyVersion}, quy trình v{task.WorkflowVersion}).",
                now);
            await jobQueue.EnqueueAsync(TaskJobs.ProcessTask(task), cancellationToken);
        }
        else
        {
            var approvalDecision = decision.CanProceedAutomatically
                ? decision with { Reasons = [.. decision.Reasons, $"Quy trình v{task.WorkflowVersion} của repo yêu cầu luôn duyệt kế hoạch."] }
                : decision;
            approvals.Add(Approval.ForPlan(task.Id, plan.Version, approvalDecision, now, TimeSpan.FromHours(options.ApprovalTtlHours)));
            task.WaitForApproval(ApprovalReason(decision, plan.Version, task.WorkflowVersion), StepActors.Orchestrator, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string ApprovalReason(PolicyDecision decision, int planVersion, int? workflowVersion) => decision.Outcome switch
    {
        PolicyOutcome.NeedsReview => "Không phân loại được thay đổi; cần người phụ trách xem xét kế hoạch.",
        PolicyOutcome.AutoApproved => $"Quy trình v{workflowVersion} yêu cầu luôn duyệt kế hoạch v{planVersion}.",
        _ => $"Kế hoạch v{planVersion} chạm vùng cần duyệt: {string.Join(", ", ApiNames.FlagsToApi(decision.Areas).DefaultIfEmpty("OUT_OF_SCOPE"))}.",
    };

    private static JobRequest RetryJob(AgentTask task, int attempt)
    {
        var job = TaskJobs.ProcessTask(task);
        return job with { DeduplicationKey = $"{job.DeduplicationKey}:planner-retry-{attempt}" };
    }
}

using DevAgent.Application.Abstractions.Ai;
using DevAgent.Application.Abstractions.Integrations;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Tasks;
using DevAgent.Application.Options;
using DevAgent.Application.Orchestration.Agents;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Common;
using DevAgent.Domain.Executions;
using DevAgent.Domain.Policies;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.Orchestration;

/// <summary>
/// Điều phối tuần tự Planner → Coding → Testing → Reviewer → Publisher (checklist 0.5, đề xuất cho pilot).
/// Mỗi lần chạy chỉ tiến task thêm một bước rồi xếp job cho bước tiếp theo, nên mỗi bước là một điểm lưu tiến độ:
/// Worker chết giữa chừng thì Worker khác nhận lại job sau khi lease hết hạn.
/// </summary>
public interface IAgentOrchestrator
{
    Task AdvanceAsync(TaskId taskId, string workerId, CancellationToken cancellationToken);

    /// <summary>Đánh dấu task thất bại khi job đã hết số lần thử (dead-letter).</summary>
    Task MarkFailedAsync(TaskId taskId, string reason, CancellationToken cancellationToken);
}

internal sealed partial class AgentOrchestrator(
    ITaskRepository tasks,
    ICodeRepositoryRepository repositories,
    IExecutionRepository executions,
    IApprovalRepository approvals,
    IJobQueue jobQueue,
    IUnitOfWork unitOfWork,
    ISourceControlGateway sourceControl,
    IPlannerAgent planner,
    IOptions<AgentLimitsOptions> limits,
    TimeProvider clock,
    ILogger<AgentOrchestrator> logger) : IAgentOrchestrator
{
    private const int MaxPlannerAttempts = 3;
    private static readonly Actor OrchestratorActor = Actor.ForAgent("orchestrator");
    private static readonly Actor PlannerActor = Actor.ForAgent("planner");

    public async Task AdvanceAsync(TaskId taskId, string workerId, CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(taskId, cancellationToken);
        if (task is null)
        {
            LogTaskMissing(logger, taskId);
            return;
        }

        if (!AgentTaskStateMachine.IsActive(task.Status))
        {
            // Đã hủy, đang chờ duyệt, đã xong...: không làm gì, chỉ dọn execution còn treo.
            await CloseDanglingExecutionsAsync(task, cancellationToken);
            return;
        }

        var now = clock.GetUtcNow();
        var timeout = TimeSpan.FromMinutes(limits.Value.TaskTimeoutMinutes);
        if (task.ActiveDuration(now) > timeout)
        {
            task.TimeOut($"Vượt thời gian xử lý tối đa {limits.Value.TaskTimeoutMinutes} phút.", now);
            await CloseDanglingExecutionsAsync(task, cancellationToken, save: false);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var repository = await repositories.GetByIdAsync(task.RepositoryId, cancellationToken);
        if (repository is null || !repository.IsActive)
        {
            task.Block("Repo không còn được đăng ký hoặc đã bị vô hiệu hóa.", OrchestratorActor, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            switch (task.Status)
            {
                case AgentTaskStatus.Pending:
                    await StartAsync(task, repository, cancellationToken);
                    break;
                case AgentTaskStatus.Planning:
                    await PlanAsync(task, repository, workerId, cancellationToken);
                    break;
                case AgentTaskStatus.InProgress:
                case AgentTaskStatus.Testing:
                case AgentTaskStatus.Reviewing:
                case AgentTaskStatus.Publishing:
                    // Coding/Testing/Reviewer/Publisher thuộc backlog Phase 2B–2C. Dừng rõ ràng thay vì giả lập kết quả.
                    task.Block(
                        $"Bước {ApiNames.ToApi(task.Status)} chưa được triển khai (Phase 2B/2C). Task dừng trước mọi thao tác ghi.",
                        OrchestratorActor,
                        clock.GetUtcNow());
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    break;
            }
        }
        catch (IntegrationNotConfiguredException ex)
        {
            LogIntegrationMissing(logger, taskId, ex.Message);
            task.Block(ex.Message, OrchestratorActor, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkFailedAsync(TaskId taskId, string reason, CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(taskId, cancellationToken);
        if (task is null || !AgentTaskStateMachine.CanTransition(task.Status, AgentTaskStatus.Failed))
        {
            return;
        }

        task.Fail(reason, OrchestratorActor, clock.GetUtcNow());
        await CloseDanglingExecutionsAsync(task, cancellationToken, save: false);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task StartAsync(AgentTask task, CodeRepository repository, CancellationToken cancellationToken)
    {
        var coordinates = new RepositoryCoordinates(repository.InstallationId, repository.Owner, repository.Name);
        var baseSha = await sourceControl.GetBranchHeadAsync(coordinates, repository.DefaultBranch, cancellationToken);
        var branch = $"devagent/task-{task.Id.Value:N}";

        task.StartPlanning(baseSha, branch, OrchestratorActor, clock.GetUtcNow());
        await jobQueue.EnqueueAsync(TaskJobs.ProcessTask(task), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task PlanAsync(AgentTask task, CodeRepository repository, string workerId, CancellationToken cancellationToken)
    {
        var options = limits.Value;
        var attempt = await executions.CountAttemptsAsync(task.Id, AgentStep.Planner, cancellationToken) + 1;
        if (attempt > MaxPlannerAttempts)
        {
            task.Fail($"Planner thất bại {MaxPlannerAttempts} lần liên tiếp.", OrchestratorActor, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        // Kiểm tra ngân sách TRƯỚC lần gọi model tiếp theo (checklist 2B).
        var budgetError = await CheckBudgetAsync(task.Id, options, cancellationToken);
        if (budgetError is not null)
        {
            task.Block(budgetError, OrchestratorActor, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var execution = AgentExecution.Start(task.Id, AgentStep.Planner, attempt, null, planner.ResolveModel(), workerId, clock.GetUtcNow());
        executions.Add(execution);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var coordinates = new RepositoryCoordinates(repository.InstallationId, repository.Owner, repository.Name);
        var files = await sourceControl.ListFilesAsync(coordinates, task.BaseCommitSha!, options.PlannerMaxFiles, cancellationToken);

        PlannerResult result;
        try
        {
            result = await planner.PlanAsync(
                new PlannerInput(task, repository, files, options.MaxOutputTokensPerCall),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            execution.Fail(ex.Message, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }

        // Lưu usage của execution trước khi đổi task: nếu task bị hủy đồng thời, chi phí vẫn được ghi.
        var usage = result.Response.Usage;
        execution.RecordUsage(new TokenUsage(usage.InputTokens, usage.OutputTokens, usage.CacheReadTokens, usage.EstimatedCostUsd));

        var now = clock.GetUtcNow();
        switch (result.Response.StopReason)
        {
            case ModelStopReason.Refusal:
                execution.Fail($"Model từ chối yêu cầu: {result.Response.StopDetail ?? "không rõ lý do"}.", now);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                task.Block("Model từ chối lập kế hoạch cho yêu cầu này; cần người phụ trách xem xét.", PlannerActor, now);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            case ModelStopReason.MaxTokens:
                execution.Fail("Kế hoạch bị cắt do chạm trần token đầu ra.", now);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await jobQueue.EnqueueAsync(RetryPlanningJob(task, attempt), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return;
        }

        if (result.Output is null)
        {
            execution.Fail(result.ParseError ?? "Không đọc được kế hoạch.", now);
            await jobQueue.EnqueueAsync(RetryPlanningJob(task, attempt), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        execution.Succeed(now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var output = result.Output;
        if (output.NeedsClarification)
        {
            task.RequestClarification(output.ClarificationQuestion!, PlannerActor, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        // Áp chính sách repo lên kế hoạch; quyết định do hệ thống tính, không do model.
        var changes = output.AffectedPaths.Select(p => new ProposedChange(p, ChangeKind.Modify)).ToArray();
        var decision = ChangePolicyEvaluator.Evaluate(repository.Policy, changes);
        var plan = task.AttachPlan(output.Summary, output.Steps, output.AcceptanceCriteria, output.Risks, output.AffectedPaths, decision, now);

        if (decision.CanProceedAutomatically)
        {
            task.StartCoding(OrchestratorActor, $"Kế hoạch v{plan.Version} nằm trong phạm vi tự động (chính sách v{decision.PolicyVersion}).", now);
            await jobQueue.EnqueueAsync(TaskJobs.ProcessTask(task), cancellationToken);
        }
        else
        {
            // RequiresApproval và NeedsReview đều dừng chờ người có quyền xem xét.
            var approval = Approval.ForPlan(task.Id, plan.Version, decision, now, TimeSpan.FromHours(options.ApprovalTtlHours));
            approvals.Add(approval);
            task.WaitForApproval(
                decision.Outcome == PolicyOutcome.NeedsReview
                    ? "Không phân loại được thay đổi; cần người phụ trách xem xét kế hoạch."
                    : $"Kế hoạch v{plan.Version} chạm vùng cần duyệt: {string.Join(", ", ApiNames.FlagsToApi(decision.Areas).DefaultIfEmpty("OUT_OF_SCOPE"))}.",
                OrchestratorActor,
                now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<string?> CheckBudgetAsync(TaskId taskId, AgentLimitsOptions options, CancellationToken cancellationToken)
    {
        var used = await executions.GetTaskUsageAsync(taskId, cancellationToken);
        if (used.EstimatedCostUsd >= options.MaxCostUsdPerTask)
        {
            return $"Chạm trần ngân sách: đã dùng {used.EstimatedCostUsd:0.0000} USD / {options.MaxCostUsdPerTask:0.00} USD.";
        }

        if (used.TotalTokens >= options.MaxTokensPerTask)
        {
            return $"Chạm trần token: đã dùng {used.TotalTokens} / {options.MaxTokensPerTask} token.";
        }

        return null;
    }

    private static JobRequest RetryPlanningJob(AgentTask task, int attempt) =>
        TaskJobs.ProcessTask(task) with
        {
            DeduplicationKey = $"{TaskJobs.ProcessTask(task).DeduplicationKey}:planner-retry-{attempt}",
        };

    private async Task CloseDanglingExecutionsAsync(AgentTask task, CancellationToken cancellationToken, bool save = true)
    {
        var running = await executions.ListRunningByTaskAsync(task.Id, cancellationToken);
        if (running.Count == 0)
        {
            return;
        }

        var now = clock.GetUtcNow();
        foreach (var execution in running)
        {
            if (task.Status == AgentTaskStatus.TimedOut)
            {
                execution.TimeOut(now);
            }
            else
            {
                execution.Cancel(now);
            }
        }

        if (save)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Không tìm thấy task {TaskId} khi xử lý job")]
    private static partial void LogTaskMissing(ILogger logger, TaskId taskId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Task {TaskId} bị chặn vì tích hợp chưa cấu hình: {Reason}")]
    private static partial void LogIntegrationMissing(ILogger logger, TaskId taskId, string reason);
}

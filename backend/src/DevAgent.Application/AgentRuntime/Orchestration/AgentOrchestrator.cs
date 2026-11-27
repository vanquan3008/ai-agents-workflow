using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.AgentRuntime.Steps;
using DevAgent.Application.Common;
using DevAgent.Application.Options;
using DevAgent.Domain.Common;
using DevAgent.Domain.Executions;
using DevAgent.Domain.Executions.Repositories;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Repositories.Repositories;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Tasks.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.AgentRuntime.Orchestration;

internal sealed partial class AgentOrchestrator(
    ITaskRepository tasks,
    ICodeRepositoryRepository repositories,
    IExecutionRepository executions,
    IUnitOfWork unitOfWork,
    IEnumerable<IWorkflowStep> steps,
    IOptions<AgentLimitsOptions> limits,
    TimeProvider clock,
    ILogger<AgentOrchestrator> logger) : IAgentOrchestrator
{
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
            // Cancelled, waiting for a human or finished: nothing to advance, only close dangling executions.
            await CloseDanglingExecutionsAsync(task, reason: null, cancellationToken);
            return;
        }

        var now = clock.GetUtcNow();
        if (task.ActiveDuration(now) > TimeSpan.FromMinutes(limits.Value.TaskTimeoutMinutes))
        {
            task.TimeOut($"Vượt thời gian xử lý tối đa {limits.Value.TaskTimeoutMinutes} phút.", now);
            await CloseDanglingExecutionsAsync(task, reason: null, cancellationToken, save: false);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var repository = await repositories.GetByIdAsync(task.RepositoryId, cancellationToken);
        if (repository is null || !repository.IsActive)
        {
            task.Block("Repo không còn được đăng ký hoặc đã bị vô hiệu hóa.", StepActors.Orchestrator, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var step = steps.FirstOrDefault(s => s.CanHandle(task.Status))
            ?? throw new InvalidOperationException($"No workflow step handles status {task.Status}.");

        // A running task uses its own workflow snapshot; a task that has not started uses the repository's current workflow.
        var workflow = task.Workflow ?? repository.Workflow;
        using var activity = DevAgentTelemetry.ActivitySource.StartActivity($"step {step.Name}");
        activity?.SetTag(DevAgentTelemetry.TaskIdTag, task.Id.ToString());

        try
        {
            await step.ExecuteAsync(new WorkflowContext(task, repository, workflow, workerId), cancellationToken);
        }
        catch (IntegrationNotConfiguredException ex)
        {
            LogIntegrationMissing(logger, taskId, ex.Message);
            task.Block(ex.Message, StepActors.Orchestrator, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkFailedAsync(TaskId taskId, string reason, CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(taskId, cancellationToken);
        if (task is null)
        {
            return;
        }

        if (AgentTaskStateMachine.CanTransition(task.Status, AgentTaskStatus.Failed))
        {
            task.Fail(reason, StepActors.Orchestrator, clock.GetUtcNow());
        }

        // Even when the task already finished, executions still marked as running must be closed with the reason.
        await CloseDanglingExecutionsAsync(task, reason, cancellationToken, save: false);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Fails dangling executions with the reason when given, times them out for timed-out tasks, otherwise cancels them.</summary>
    private async Task CloseDanglingExecutionsAsync(AgentTask task, string? reason, CancellationToken cancellationToken, bool save = true)
    {
        var running = await executions.ListRunningByTaskAsync(task.Id, cancellationToken);
        if (running.Count == 0)
        {
            return;
        }

        var now = clock.GetUtcNow();
        foreach (var execution in running)
        {
            if (reason is not null)
            {
                execution.Fail(reason, now);
            }
            else if (task.Status == AgentTaskStatus.TimedOut)
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Task {TaskId} not found while processing its job")]
    private static partial void LogTaskMissing(ILogger logger, TaskId taskId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Task {TaskId} blocked because an integration is not configured: {Reason}")]
    private static partial void LogIntegrationMissing(ILogger logger, TaskId taskId, string reason);
}

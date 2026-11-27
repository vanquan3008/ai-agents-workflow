using DevAgent.Application.AgentRuntime.Steps;
using DevAgent.Domain.Common;

namespace DevAgent.Application.AgentRuntime.Orchestration;

/// <summary>
/// Drives Planner → Coding → Testing → Reviewer → Publisher sequentially. Each run advances the task by one
/// <see cref="Steps.IWorkflowStep"/> and enqueues the next job, so every step is a checkpoint: if a worker dies,
/// another one picks the job up after the lease expires.
/// </summary>
public interface IAgentOrchestrator
{
    Task AdvanceAsync(TaskId taskId, string workerId, CancellationToken cancellationToken);

    /// <summary>Marks the task failed (e.g. its job ran out of attempts). Idempotent for finished tasks.</summary>
    Task MarkFailedAsync(TaskId taskId, string reason, CancellationToken cancellationToken);
}

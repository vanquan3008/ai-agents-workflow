using DevAgent.Domain.Tasks;

namespace DevAgent.Application.AgentRuntime.Steps;

/// <summary>
/// One step of the pipeline. The orchestrator picks the step by task status; each run advances exactly one step,
/// persists the result and enqueues the next job. Steps must be idempotent with respect to status.
/// </summary>
public interface IWorkflowStep
{
    /// <summary>Step name used in logs and traces.</summary>
    string Name { get; }

    bool CanHandle(AgentTaskStatus status);

    Task ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken);
}

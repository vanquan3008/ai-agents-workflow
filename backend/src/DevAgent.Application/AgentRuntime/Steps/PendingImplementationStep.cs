using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Common;
using DevAgent.Domain.Tasks;

namespace DevAgent.Application.AgentRuntime.Steps;

/// <summary>
/// Coding, Testing, Reviewer and Publisher are not implemented yet. This step blocks the task explicitly before any
/// write instead of faking results. When a real step lands, register a new <see cref="IWorkflowStep"/> for its status
/// and remove that status from here.
/// </summary>
internal sealed class PendingImplementationStep(IUnitOfWork unitOfWork, TimeProvider clock) : IWorkflowStep
{
    private static readonly AgentTaskStatus[] Handled =
        [AgentTaskStatus.InProgress, AgentTaskStatus.Testing, AgentTaskStatus.Reviewing, AgentTaskStatus.Publishing];

    public string Name => "pending-implementation";

    public bool CanHandle(AgentTaskStatus status) => Handled.Contains(status);

    public async Task ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Task.Block(
            $"Bước {ApiNames.ToApi(context.Task.Status)} chưa được triển khai (Phase 2B/2C). Task dừng trước mọi thao tác ghi.",
            StepActors.Orchestrator,
            clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

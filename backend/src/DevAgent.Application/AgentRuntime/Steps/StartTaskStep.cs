using DevAgent.Application.Abstractions.Integrations;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Features.Tasks;
using DevAgent.Domain.Tasks;

namespace DevAgent.Application.AgentRuntime.Steps;

/// <summary>
/// PENDING → PLANNING: pins the base commit, the working branch and a snapshot of the repository's current workflow.
/// From here on the task follows that workflow version even if the repository workflow changes.
/// </summary>
internal sealed class StartTaskStep(ISourceControlGateway sourceControl, IJobQueue jobQueue, IUnitOfWork unitOfWork, TimeProvider clock)
    : IWorkflowStep
{
    public string Name => "start";

    public bool CanHandle(AgentTaskStatus status) => status == AgentTaskStatus.Pending;

    public async Task ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (task, repository, _, _) = context;

        var coordinates = new RepositoryCoordinates(repository.InstallationId, repository.Owner, repository.Name);
        var baseSha = await sourceControl.GetBranchHeadAsync(coordinates, repository.DefaultBranch, cancellationToken);

        task.StartPlanning(
            baseSha,
            $"devagent/task-{task.Id.Value:N}",
            repository.Workflow,
            repository.WorkflowVersion,
            StepActors.Orchestrator,
            clock.GetUtcNow());

        await jobQueue.EnqueueAsync(TaskJobs.ProcessTask(task), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

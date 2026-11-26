using DevAgent.Domain.Common;
using DevAgent.Domain.Workflows;

namespace DevAgent.Domain.Workflows.Repositories;

public interface IWorkflowRevisionRepository
{
    Task<IReadOnlyList<WorkflowRevision>> ListByRepositoryAsync(RepositoryId repositoryId, int limit, CancellationToken cancellationToken);

    void Add(WorkflowRevision revision);
}

using DevAgent.Domain.Common;
using DevAgent.Domain.Workflows;
using DevAgent.Domain.Workflows.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DevAgent.Infrastructure.Persistence.Repositories;

internal sealed class WorkflowRevisionRepository(DevAgentDbContext context) : IWorkflowRevisionRepository
{
    public async Task<IReadOnlyList<WorkflowRevision>> ListByRepositoryAsync(RepositoryId repositoryId, int limit, CancellationToken cancellationToken) =>
        await context.WorkflowRevisions.AsNoTracking()
            .Where(r => r.RepositoryId == repositoryId)
            .OrderByDescending(r => r.Version)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public void Add(WorkflowRevision revision) => context.WorkflowRevisions.Add(revision);
}

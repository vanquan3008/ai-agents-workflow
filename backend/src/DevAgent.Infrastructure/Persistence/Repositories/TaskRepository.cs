using DevAgent.Domain.Common;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Tasks.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DevAgent.Infrastructure.Persistence.Repositories;

internal sealed class TaskRepository(DevAgentDbContext context) : ITaskRepository
{
    // Owned collections (transitions, plans) load with the aggregate.
    public Task<AgentTask?> GetByIdAsync(TaskId id, CancellationToken cancellationToken) =>
        context.Tasks.AsSplitQuery().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<AgentTask?> FindByIdempotencyKeyAsync(ProjectId projectId, IdempotencyKey key, CancellationToken cancellationToken) =>
        context.Tasks.AsSplitQuery().FirstOrDefaultAsync(t => t.ProjectId == projectId && t.IdempotencyKey == key, cancellationToken);

    public void Add(AgentTask task) => context.Tasks.Add(task);
}

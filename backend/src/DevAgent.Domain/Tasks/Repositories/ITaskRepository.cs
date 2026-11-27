using DevAgent.Domain.Common;
using DevAgent.Domain.Tasks;

namespace DevAgent.Domain.Tasks.Repositories;

public interface ITaskRepository
{
    /// <summary>Loads the whole aggregate, including plans and status history.</summary>
    Task<AgentTask?> GetByIdAsync(TaskId id, CancellationToken cancellationToken);

    Task<AgentTask?> FindByIdempotencyKeyAsync(ProjectId projectId, IdempotencyKey key, CancellationToken cancellationToken);

    void Add(AgentTask task);
}

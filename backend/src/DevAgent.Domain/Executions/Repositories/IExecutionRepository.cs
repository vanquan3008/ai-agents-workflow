using DevAgent.Domain.Common;
using DevAgent.Domain.Executions;

namespace DevAgent.Domain.Executions.Repositories;

public interface IExecutionRepository
{
    Task<AgentExecution?> GetByIdAsync(ExecutionId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentExecution>> ListRunningByTaskAsync(TaskId taskId, CancellationToken cancellationToken);

    /// <summary>Total runs of one step, used to number attempts while keeping history.</summary>
    Task<int> CountAttemptsAsync(TaskId taskId, AgentStep step, CancellationToken cancellationToken);

    /// <summary>Failed runs of one step since <paramref name="since"/>, used to cap retries within the current round.</summary>
    Task<int> CountFailuresSinceAsync(TaskId taskId, AgentStep step, DateTimeOffset since, CancellationToken cancellationToken);

    Task<TaskUsageTotals> GetTaskUsageAsync(TaskId taskId, CancellationToken cancellationToken);

    void Add(AgentExecution execution);
}

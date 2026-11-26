using DevAgent.Domain.Common;

namespace DevAgent.Application.Features.Executions;

public interface IExecutionQueries
{
    Task<IReadOnlyList<ExecutionSummaryDto>> ListByTaskAsync(TaskId taskId, CancellationToken cancellationToken);

    Task<ExecutionDetailDto?> GetAsync(ExecutionId executionId, CancellationToken cancellationToken);
}

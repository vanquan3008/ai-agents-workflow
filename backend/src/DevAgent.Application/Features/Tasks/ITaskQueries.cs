using DevAgent.Application.Common;
using DevAgent.Domain.Common;

namespace DevAgent.Application.Features.Tasks;

/// <summary>Read-side projections for the Tasks module.</summary>
public interface ITaskQueries
{
    Task<PagedResult<TaskSummaryDto>> ListAsync(
        IReadOnlyCollection<ProjectId> projectIds,
        TaskListFilter filter,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<TaskDetailDto?> GetAsync(TaskId taskId, CancellationToken cancellationToken);

    /// <summary>Status history after <paramref name="afterSequence"/>, in ascending order.</summary>
    Task<IReadOnlyList<TimelineEntryDto>> GetTimelineAsync(TaskId taskId, long afterSequence, int limit, CancellationToken cancellationToken);

    Task<ProjectId?> GetProjectIdAsync(TaskId taskId, CancellationToken cancellationToken);
}

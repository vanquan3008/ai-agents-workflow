using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Common;
using DevAgent.Domain.Tasks;

namespace DevAgent.Application.Features.Tasks;

public sealed record ListTasksQuery(Guid? ProjectId, Guid? RepositoryId, string? Status, string? Search, int? Page, int? PageSize);

internal sealed class ListTasksHandler(ProjectAccess access, ITaskQueries taskQueries)
    : IQueryHandler<ListTasksQuery, PagedResult<TaskSummaryDto>>
{
    public async Task<Result<PagedResult<TaskSummaryDto>>> HandleAsync(ListTasksQuery query, CancellationToken cancellationToken)
    {
        AgentTaskStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!ApiNames.TryParse<AgentTaskStatus>(query.Status, out var parsed))
            {
                return Error.Validation("task.status_invalid", $"Trạng thái không hợp lệ: {query.Status}.");
            }

            status = parsed;
        }

        var projectIds = await access.ResolveViewableProjectsAsync(query.ProjectId, cancellationToken);
        if (projectIds.IsFailure)
        {
            return projectIds.Error!;
        }

        var filter = new TaskListFilter(status, query.RepositoryId is { } r ? new RepositoryId(r) : null, query.Search?.Trim());
        return await taskQueries.ListAsync(projectIds.Value, filter, new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}

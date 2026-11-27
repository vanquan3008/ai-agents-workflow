using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Shared;
using DevAgent.Application.Features.Tasks;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;
using DevAgent.Domain.Tasks;

namespace DevAgent.Application.Features.Departments.Queries;

public sealed record ListDepartmentTasksQuery(Guid ProjectId, Guid DepartmentId, string? Status, string? Search, int? Page, int? PageSize);

/// <summary>
/// Tasks of the repositories the published revision follows. The repository filter is applied in the database before
/// pagination, so pages and totals match what the office shows.
/// </summary>
internal sealed class ListDepartmentTasksHandler(DepartmentAccess access, ITaskQueries taskQueries)
    : IQueryHandler<ListDepartmentTasksQuery, PagedResult<TaskSummaryDto>>
{
    public async Task<Result<PagedResult<TaskSummaryDto>>> HandleAsync(ListDepartmentTasksQuery query, CancellationToken cancellationToken)
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

        var context = await access.RequirePublishedAsync(new ProjectId(query.ProjectId), new ItDepartmentId(query.DepartmentId), cancellationToken);
        if (context.IsFailure)
        {
            return context.Error!;
        }

        var page = new PageRequest(query.Page, query.PageSize);
        var repositoryIds = context.Value.Revision.RepositoryIds;
        if (repositoryIds.Count == 0)
        {
            return new PagedResult<TaskSummaryDto>([], page.Page, page.PageSize, 0);
        }

        var filter = new TaskListFilter(status, null, query.Search?.Trim(), repositoryIds);
        return await taskQueries.ListAsync([context.Value.Pointer.ProjectId], filter, page, cancellationToken);
    }
}

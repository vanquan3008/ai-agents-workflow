using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Projects;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Tasks;

namespace DevAgent.Application.Features.Tasks;

public sealed record ListTasksQuery(Guid? ProjectId, Guid? RepositoryId, string? Status, string? Search, int? Page, int? PageSize);

internal sealed class ListTasksHandler(ProjectAccess access, IProjectQueries projectQueries, ITaskQueries taskQueries)
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

        IReadOnlyCollection<ProjectId> projectIds;
        if (query.ProjectId is { } projectGuid)
        {
            var projectId = new ProjectId(projectGuid);
            var user = await access.RequireAsync(projectId, ProjectPermission.ViewTasks, "project", projectId.ToString(), cancellationToken);
            if (user.IsFailure)
            {
                return user.Error!;
            }

            projectIds = [projectId];
        }
        else
        {
            var user = access.RequireAuthenticated();
            if (user.IsFailure)
            {
                return user.Error!;
            }

            // Không truyền project: chỉ trả task thuộc các project người dùng được xem.
            projectIds = await projectQueries.ListProjectIdsWithPermissionAsync(user.Value, ProjectPermission.ViewTasks, cancellationToken);
        }

        var filter = new TaskListFilter(status, query.RepositoryId is { } r ? new RepositoryId(r) : null, query.Search?.Trim());
        return await taskQueries.ListAsync(projectIds, filter, new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}

public sealed record GetTaskQuery(Guid TaskId);

internal sealed class GetTaskHandler(ProjectAccess access, ITaskQueries taskQueries) : IQueryHandler<GetTaskQuery, TaskDetailDto>
{
    public async Task<Result<TaskDetailDto>> HandleAsync(GetTaskQuery query, CancellationToken cancellationToken)
    {
        var taskId = new TaskId(query.TaskId);
        var projectId = await taskQueries.GetProjectIdAsync(taskId, cancellationToken);
        if (projectId is null)
        {
            return Error.NotFound("task", query.TaskId);
        }

        var user = await access.RequireAsync(projectId.Value, ProjectPermission.ViewTasks, "task", taskId.ToString(), cancellationToken);
        if (user.IsFailure)
        {
            return user.Error!;
        }

        var detail = await taskQueries.GetAsync(taskId, cancellationToken);
        return detail is null ? Error.NotFound("task", query.TaskId) : detail;
    }
}

public sealed record GetTaskTimelineQuery(Guid TaskId, long AfterSequence, int? Limit);

internal sealed class GetTaskTimelineHandler(ProjectAccess access, ITaskQueries taskQueries)
    : IQueryHandler<GetTaskTimelineQuery, IReadOnlyList<TimelineEntryDto>>
{
    public async Task<Result<IReadOnlyList<TimelineEntryDto>>> HandleAsync(GetTaskTimelineQuery query, CancellationToken cancellationToken)
    {
        var taskId = new TaskId(query.TaskId);
        var projectId = await taskQueries.GetProjectIdAsync(taskId, cancellationToken);
        if (projectId is null)
        {
            return Error.NotFound("task", query.TaskId);
        }

        var user = await access.RequireAsync(projectId.Value, ProjectPermission.ViewTasks, "task", taskId.ToString(), cancellationToken);
        if (user.IsFailure)
        {
            return user.Error!;
        }

        var limit = Math.Clamp(query.Limit ?? 200, 1, 1000);
        return Result<IReadOnlyList<TimelineEntryDto>>.Success(
            await taskQueries.GetTimelineAsync(taskId, Math.Max(0, query.AfterSequence), limit, cancellationToken));
    }
}

/// <summary>
/// Kiểm tra quyền đăng ký nhận sự kiện realtime của một task (dùng bởi SignalR hub).
/// Cùng chính sách với đọc dữ liệu (checklist 1.3).
/// </summary>
public sealed record AuthorizeTaskSubscriptionQuery(Guid TaskId);

internal sealed class AuthorizeTaskSubscriptionHandler(ProjectAccess access, ITaskQueries taskQueries)
    : IQueryHandler<AuthorizeTaskSubscriptionQuery, Guid>
{
    public async Task<Result<Guid>> HandleAsync(AuthorizeTaskSubscriptionQuery query, CancellationToken cancellationToken)
    {
        var taskId = new TaskId(query.TaskId);
        var projectId = await taskQueries.GetProjectIdAsync(taskId, cancellationToken);
        if (projectId is null)
        {
            return Error.NotFound("task", query.TaskId);
        }

        var user = await access.RequireAsync(projectId.Value, ProjectPermission.ViewTasks, "task.subscription", taskId.ToString(), cancellationToken);
        return user.IsFailure ? Result<Guid>.Failure(user.Error!) : Result<Guid>.Success(projectId.Value.Value);
    }
}

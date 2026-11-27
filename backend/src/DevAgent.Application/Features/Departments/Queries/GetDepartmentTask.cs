using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Shared;
using DevAgent.Application.Features.Tasks;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;

namespace DevAgent.Application.Features.Departments.Queries;

public sealed record GetDepartmentTaskQuery(Guid ProjectId, Guid DepartmentId, Guid TaskId);

/// <summary>
/// A task deep link within an office. Answers 404 when the task's repository is not followed by the current revision,
/// so a shared link cannot reveal work outside the department.
/// </summary>
internal sealed class GetDepartmentTaskHandler(DepartmentAccess access, ITaskQueries taskQueries)
    : IQueryHandler<GetDepartmentTaskQuery, TaskDetailDto>
{
    public async Task<Result<TaskDetailDto>> HandleAsync(GetDepartmentTaskQuery query, CancellationToken cancellationToken)
    {
        var context = await access.RequirePublishedAsync(new ProjectId(query.ProjectId), new ItDepartmentId(query.DepartmentId), cancellationToken);
        if (context.IsFailure)
        {
            return context.Error!;
        }

        var taskId = new TaskId(query.TaskId);
        var scope = await taskQueries.GetScopeAsync(taskId, cancellationToken);
        if (scope is null
            || scope.ProjectId != context.Value.Pointer.ProjectId
            || !context.Value.Revision.RepositoryIds.Contains(scope.RepositoryId))
        {
            return Error.NotFound("task", query.TaskId);
        }

        var detail = await taskQueries.GetAsync(taskId, cancellationToken);
        return detail is null ? Error.NotFound("task", query.TaskId) : detail;
    }
}

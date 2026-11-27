using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Tasks;

public sealed record GetTaskQuery(Guid TaskId);

internal sealed class GetTaskHandler(ProjectAccess access, ITaskQueries taskQueries) : IQueryHandler<GetTaskQuery, TaskDetailDto>
{
    public async Task<Result<TaskDetailDto>> HandleAsync(GetTaskQuery query, CancellationToken cancellationToken)
    {
        var taskId = new TaskId(query.TaskId);
        var permission = await access.RequireTaskPermissionAsync(
            taskQueries, taskId, ProjectPermission.ViewTasks, "task", taskId.ToString(), cancellationToken);
        if (permission.IsFailure)
        {
            return permission.Error!;
        }

        var detail = await taskQueries.GetAsync(taskId, cancellationToken);
        return detail is null ? Error.NotFound("task", query.TaskId) : detail;
    }
}

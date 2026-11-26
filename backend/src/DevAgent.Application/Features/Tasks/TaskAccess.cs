using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Tasks;

internal static class TaskAccess
{
    /// <summary>Resolves the task's project and checks <paramref name="permission"/> on it.</summary>
    public static async Task<Result<ProjectId>> RequireTaskPermissionAsync(
        this ProjectAccess access,
        ITaskQueries taskQueries,
        TaskId taskId,
        ProjectPermission permission,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken)
    {
        var projectId = await taskQueries.GetProjectIdAsync(taskId, cancellationToken);
        if (projectId is null)
        {
            return Error.NotFound("task", taskId.Value);
        }

        var user = await access.RequireAsync(projectId.Value, permission, resourceType, resourceId, cancellationToken);
        return user.IsFailure ? Result<ProjectId>.Failure(user.Error!) : projectId.Value;
    }
}

using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Tasks;

/// <summary>Authorizes a SignalR subscription to one task's events using the same policy as reading the task.</summary>
public sealed record AuthorizeTaskSubscriptionQuery(Guid TaskId);

internal sealed class AuthorizeTaskSubscriptionHandler(ProjectAccess access, ITaskQueries taskQueries)
    : IQueryHandler<AuthorizeTaskSubscriptionQuery, Guid>
{
    public async Task<Result<Guid>> HandleAsync(AuthorizeTaskSubscriptionQuery query, CancellationToken cancellationToken)
    {
        var taskId = new TaskId(query.TaskId);
        var projectId = await access.RequireTaskPermissionAsync(
            taskQueries, taskId, ProjectPermission.ViewTasks, "task.subscription", taskId.ToString(), cancellationToken);
        return projectId.IsFailure ? Result<Guid>.Failure(projectId.Error!) : Result<Guid>.Success(projectId.Value.Value);
    }
}

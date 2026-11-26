using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Tasks;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Executions;

public sealed record ListTaskExecutionsQuery(Guid TaskId);

internal sealed class ListTaskExecutionsHandler(ProjectAccess access, ITaskQueries taskQueries, IExecutionQueries executionQueries)
    : IQueryHandler<ListTaskExecutionsQuery, IReadOnlyList<ExecutionSummaryDto>>
{
    public async Task<Result<IReadOnlyList<ExecutionSummaryDto>>> HandleAsync(ListTaskExecutionsQuery query, CancellationToken cancellationToken)
    {
        var taskId = new TaskId(query.TaskId);
        var permission = await access.RequireTaskPermissionAsync(
            taskQueries, taskId, ProjectPermission.ViewTasks, "task", taskId.ToString(), cancellationToken);
        return permission.IsFailure
            ? Result<IReadOnlyList<ExecutionSummaryDto>>.Failure(permission.Error!)
            : Result<IReadOnlyList<ExecutionSummaryDto>>.Success(await executionQueries.ListByTaskAsync(taskId, cancellationToken));
    }
}

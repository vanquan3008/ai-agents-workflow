using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Tasks;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Executions;

public sealed record GetExecutionQuery(Guid ExecutionId);

internal sealed class GetExecutionHandler(ProjectAccess access, ITaskQueries taskQueries, IExecutionQueries executionQueries)
    : IQueryHandler<GetExecutionQuery, ExecutionDetailDto>
{
    public async Task<Result<ExecutionDetailDto>> HandleAsync(GetExecutionQuery query, CancellationToken cancellationToken)
    {
        var detail = await executionQueries.GetAsync(new ExecutionId(query.ExecutionId), cancellationToken);
        if (detail is null)
        {
            return Error.NotFound("execution", query.ExecutionId);
        }

        var permission = await access.RequireTaskPermissionAsync(
            taskQueries,
            new TaskId(detail.Execution.TaskId),
            ProjectPermission.ViewTasks,
            "execution",
            query.ExecutionId.ToString(),
            cancellationToken);
        if (permission.IsFailure)
        {
            // An orphaned execution is reported as missing rather than leaking that its task is gone.
            var error = permission.Error!;
            return error.Type == ErrorType.NotFound ? Error.NotFound("execution", query.ExecutionId) : error;
        }

        return detail;
    }
}

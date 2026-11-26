using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Tasks;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Executions;

public sealed record ExecutionSummaryDto(
    Guid Id,
    Guid TaskId,
    string Step,
    int Attempt,
    string Status,
    string Model,
    UsageDto Usage,
    string? Error,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    double? DurationSeconds);

public sealed record ToolExecutionDto(
    Guid Id,
    int Sequence,
    string ToolName,
    string Status,
    int? ExitCode,
    string InputJson,
    string? OutputJson,
    string? DenialReason,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt);

public sealed record ExecutionDetailDto(ExecutionSummaryDto Execution, string? Checkpoint, IReadOnlyList<ToolExecutionDto> ToolExecutions);

public interface IExecutionQueries
{
    Task<IReadOnlyList<ExecutionSummaryDto>> ListByTaskAsync(TaskId taskId, CancellationToken cancellationToken);

    Task<ExecutionDetailDto?> GetAsync(ExecutionId executionId, CancellationToken cancellationToken);
}

public sealed record ListTaskExecutionsQuery(Guid TaskId);

internal sealed class ListTaskExecutionsHandler(ProjectAccess access, ITaskQueries taskQueries, IExecutionQueries executionQueries)
    : IQueryHandler<ListTaskExecutionsQuery, IReadOnlyList<ExecutionSummaryDto>>
{
    public async Task<Result<IReadOnlyList<ExecutionSummaryDto>>> HandleAsync(ListTaskExecutionsQuery query, CancellationToken cancellationToken)
    {
        var taskId = new TaskId(query.TaskId);
        var projectId = await taskQueries.GetProjectIdAsync(taskId, cancellationToken);
        if (projectId is null)
        {
            return Error.NotFound("task", query.TaskId);
        }

        var user = await access.RequireAsync(projectId.Value, ProjectPermission.ViewTasks, "task", taskId.ToString(), cancellationToken);
        return user.IsFailure
            ? Result<IReadOnlyList<ExecutionSummaryDto>>.Failure(user.Error!)
            : Result<IReadOnlyList<ExecutionSummaryDto>>.Success(await executionQueries.ListByTaskAsync(taskId, cancellationToken));
    }
}

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

        var taskId = new TaskId(detail.Execution.TaskId);
        var projectId = await taskQueries.GetProjectIdAsync(taskId, cancellationToken);
        if (projectId is null)
        {
            return Error.NotFound("execution", query.ExecutionId);
        }

        var user = await access.RequireAsync(projectId.Value, ProjectPermission.ViewTasks, "execution", query.ExecutionId.ToString(), cancellationToken);
        return user.IsFailure ? Result<ExecutionDetailDto>.Failure(user.Error!) : Result<ExecutionDetailDto>.Success(detail);
    }
}

using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Tasks;

public sealed record GetTaskTimelineQuery(Guid TaskId, long AfterSequence, int? Limit);

internal sealed class GetTaskTimelineHandler(ProjectAccess access, ITaskQueries taskQueries)
    : IQueryHandler<GetTaskTimelineQuery, IReadOnlyList<TimelineEntryDto>>
{
    private const int DefaultLimit = 200;
    private const int MaxLimit = 1000;

    public async Task<Result<IReadOnlyList<TimelineEntryDto>>> HandleAsync(GetTaskTimelineQuery query, CancellationToken cancellationToken)
    {
        var taskId = new TaskId(query.TaskId);
        var permission = await access.RequireTaskPermissionAsync(
            taskQueries, taskId, ProjectPermission.ViewTasks, "task", taskId.ToString(), cancellationToken);
        if (permission.IsFailure)
        {
            return permission.Error!;
        }

        var limit = Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit);
        return Result<IReadOnlyList<TimelineEntryDto>>.Success(
            await taskQueries.GetTimelineAsync(taskId, Math.Max(0, query.AfterSequence), limit, cancellationToken));
    }
}

using DevAgent.Application.Abstractions.Events;
using DevAgent.Application.Abstractions.Realtime;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Tasks;
using DevAgent.Domain.Approvals;

namespace DevAgent.Application.Features.Approvals.EventHandlers;

internal sealed class ApprovalDecidedHandler(ITaskEventPublisher publisher, ITaskQueries taskQueries) : IDomainEventHandler<ApprovalDecided>
{
    public async Task HandleAsync(ApprovalDecided domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var projectId = await taskQueries.GetProjectIdAsync(domainEvent.TaskId, cancellationToken);
        if (projectId is null)
        {
            return;
        }

        await publisher.PublishAsync(
            new TaskRealtimeEvent(
                domainEvent.TaskId.Value,
                projectId.Value.Value,
                "approval.decided",
                0,
                ApiNames.ToApi(domainEvent.Status),
                null,
                domainEvent.OccurredAt),
            cancellationToken);
    }
}

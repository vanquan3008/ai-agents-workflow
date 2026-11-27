using DevAgent.Application.Abstractions.Events;
using DevAgent.Application.Abstractions.Realtime;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Notifications;
using DevAgent.Domain.Tasks;

namespace DevAgent.Application.Features.Tasks.EventHandlers;

/// <summary>
/// Pushes status changes to the dashboard after commit (best effort). Chat notifications go through the
/// durable outbox (<see cref="Notifications.TaskNotificationOutbox"/>) instead so they cannot be lost.
/// </summary>
internal sealed class TaskStatusChangedHandler(ITaskEventPublisher publisher) : IDomainEventHandler<TaskStatusChanged>
{
    public Task HandleAsync(TaskStatusChanged domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return publisher.PublishAsync(
            new TaskRealtimeEvent(
                domainEvent.TaskId.Value,
                domainEvent.ProjectId.Value,
                "task.status_changed",
                domainEvent.Sequence,
                ApiNames.ToApi(domainEvent.To),
                domainEvent.Reason,
                domainEvent.OccurredAt),
            cancellationToken);
    }
}

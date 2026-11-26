using System.Text.Json;
using DevAgent.Application.Abstractions.Events;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Domain.Tasks;

namespace DevAgent.Application.Features.Notifications;

/// <summary>
/// Outbox: when a task reaches a notifiable milestone, a delivery job is enqueued <b>in the same transaction</b>.
/// A crash right after commit or a Discord outage cannot lose the notification because the job remains and is retried.
/// The deduplication key is (task, transition sequence), so each milestone produces one notification.
/// </summary>
internal sealed class TaskNotificationOutbox(IJobQueue jobQueue) : ITransactionalDomainEventHandler<TaskStatusChanged>
{
    public Task HandleAsync(TaskStatusChanged domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (NotificationMapping.KindFor(domainEvent) is not { } kind)
        {
            return Task.CompletedTask;
        }

        var payload = new NotificationPayload(domainEvent.TaskId.Value, kind, domainEvent.Sequence, domainEvent.Reason);
        return jobQueue.EnqueueAsync(
            new JobRequest(
                JobTypes.SendNotification,
                JsonSerializer.Serialize(payload),
                NotificationMapping.DeduplicationKey(domainEvent.TaskId.Value, domainEvent.Sequence)),
            cancellationToken);
    }
}

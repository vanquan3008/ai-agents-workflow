using DevAgent.Application.Abstractions.Events;
using DevAgent.Application.Abstractions.Realtime;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Tasks;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Tasks;

namespace DevAgent.Application.Events;

/// <summary>
/// Đẩy thay đổi trạng thái tới dashboard (best-effort, sau commit). Thông báo chat đi qua outbox
/// (<see cref="Notifications.TaskNotificationOutbox"/>) để không bị mất.
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

internal sealed class TaskPlanCreatedHandler(ITaskEventPublisher publisher) : IDomainEventHandler<TaskPlanCreated>
{
    public Task HandleAsync(TaskPlanCreated domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return publisher.PublishAsync(
            new TaskRealtimeEvent(
                domainEvent.TaskId.Value,
                domainEvent.ProjectId.Value,
                "task.plan_created",
                0,
                null,
                $"Kế hoạch v{domainEvent.PlanVersion}",
                domainEvent.OccurredAt),
            cancellationToken);
    }
}

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

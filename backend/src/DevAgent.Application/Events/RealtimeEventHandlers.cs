using DevAgent.Application.Abstractions.Events;
using DevAgent.Application.Abstractions.Integrations;
using DevAgent.Application.Abstractions.Realtime;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Tasks;
using DevAgent.Application.Options;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Tasks;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.Events;

/// <summary>Đẩy thay đổi trạng thái tới dashboard và gửi thông báo chat ở các mốc người dùng cần biết.</summary>
internal sealed class TaskStatusChangedHandler(
    ITaskEventPublisher publisher,
    IChatNotifier notifier,
    ITaskQueries taskQueries,
    IOptions<PublicUrlsOptions> urls) : IDomainEventHandler<TaskStatusChanged>
{
    public async Task HandleAsync(TaskStatusChanged domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await publisher.PublishAsync(
            new TaskRealtimeEvent(
                domainEvent.TaskId.Value,
                domainEvent.ProjectId.Value,
                "task.status_changed",
                domainEvent.Sequence,
                ApiNames.ToApi(domainEvent.To),
                domainEvent.Reason,
                domainEvent.OccurredAt),
            cancellationToken);

        var kind = domainEvent.To switch
        {
            AgentTaskStatus.Pending when domainEvent.From is null => ChatNotificationKind.TaskAccepted,
            AgentTaskStatus.WaitingForApproval => ChatNotificationKind.ApprovalRequired,
            AgentTaskStatus.NeedsClarification => ChatNotificationKind.ClarificationNeeded,
            AgentTaskStatus.Completed => ChatNotificationKind.Completed,
            AgentTaskStatus.Failed or AgentTaskStatus.TimedOut or AgentTaskStatus.Rejected => ChatNotificationKind.Failed,
            AgentTaskStatus.Blocked => ChatNotificationKind.Blocked,
            AgentTaskStatus.Cancelled => ChatNotificationKind.Cancelled,
            _ => (ChatNotificationKind?)null,
        };

        if (kind is null)
        {
            return;
        }

        var task = await taskQueries.GetAsync(domainEvent.TaskId, cancellationToken);
        if (task is null)
        {
            return;
        }

        var link = kind == ChatNotificationKind.Completed && task.PullRequest is not null
            ? task.PullRequest.Url
            : urls.Value.TaskUrl(task.Id);

        await notifier.NotifyAsync(
            new ChatNotification(kind.Value, task.Id, task.ProjectId, task.Title, domainEvent.Reason ?? task.Status, link, task.SourceReference),
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

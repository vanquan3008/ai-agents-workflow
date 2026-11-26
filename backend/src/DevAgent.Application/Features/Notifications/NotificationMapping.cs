using DevAgent.Application.Abstractions.Integrations;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Workflows;

namespace DevAgent.Application.Features.Notifications;

/// <summary>Payload of a <see cref="JobTypes.SendNotification"/> job.</summary>
public sealed record NotificationPayload(Guid TaskId, ChatNotificationKind Kind, long Sequence, string? Reason);

internal static class NotificationMapping
{
    public static ChatNotificationKind? KindFor(TaskStatusChanged change) => change.To switch
    {
        AgentTaskStatus.Pending when change.From is null => ChatNotificationKind.TaskAccepted,
        AgentTaskStatus.WaitingForApproval => ChatNotificationKind.ApprovalRequired,
        AgentTaskStatus.NeedsClarification => ChatNotificationKind.ClarificationNeeded,
        AgentTaskStatus.Completed => ChatNotificationKind.Completed,
        AgentTaskStatus.Failed or AgentTaskStatus.TimedOut or AgentTaskStatus.Rejected => ChatNotificationKind.Failed,
        AgentTaskStatus.Blocked => ChatNotificationKind.Blocked,
        AgentTaskStatus.Cancelled => ChatNotificationKind.Cancelled,
        _ => null,
    };

    public static NotificationEvent ToEvent(ChatNotificationKind kind) => kind switch
    {
        ChatNotificationKind.TaskAccepted => NotificationEvent.TaskAccepted,
        ChatNotificationKind.ApprovalRequired => NotificationEvent.ApprovalRequired,
        ChatNotificationKind.ClarificationNeeded => NotificationEvent.ClarificationNeeded,
        ChatNotificationKind.Completed => NotificationEvent.Completed,
        ChatNotificationKind.Failed => NotificationEvent.Failed,
        ChatNotificationKind.Blocked => NotificationEvent.Blocked,
        ChatNotificationKind.Cancelled => NotificationEvent.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static string DeduplicationKey(Guid taskId, long sequence) => $"{JobTypes.SendNotification}:{taskId}:{sequence}";
}

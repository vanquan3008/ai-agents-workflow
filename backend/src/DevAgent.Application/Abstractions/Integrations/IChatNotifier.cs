namespace DevAgent.Application.Abstractions.Integrations;

public enum ChatNotificationKind
{
    TaskAccepted = 1,
    ApprovalRequired = 2,
    ClarificationNeeded = 3,
    Completed = 4,
    Failed = 5,
    Blocked = 6,
    Cancelled = 7,
}

/// <param name="DeduplicationKey">Stable key the chat channel uses to drop duplicates when a job is retried.</param>
public sealed record ChatNotification(
    ChatNotificationKind Kind,
    Guid TaskId,
    Guid ProjectId,
    string Title,
    string Message,
    string? Link,
    string? SourceReference,
    string DeduplicationKey);

/// <summary>
/// Sends notifications to the chat channel. Implementations throw <see cref="Messaging.RetryableJobException"/> for
/// transient failures (rate limits, network, 5xx) and <see cref="Messaging.PermanentJobException"/> for
/// misconfiguration (401/403/404).
/// </summary>
public interface IChatNotifier
{
    Task NotifyAsync(ChatNotification notification, CancellationToken cancellationToken);
}

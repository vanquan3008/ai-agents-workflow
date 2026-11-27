namespace DevAgent.Domain.Workflows;

/// <summary>Task milestones that can be announced to the chat channel.</summary>
public enum NotificationEvent
{
    TaskAccepted = 1,
    ApprovalRequired = 2,
    ClarificationNeeded = 3,
    Completed = 4,
    Failed = 5,
    Blocked = 6,
    Cancelled = 7,
}

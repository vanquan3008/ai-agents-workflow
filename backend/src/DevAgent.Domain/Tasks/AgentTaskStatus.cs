namespace DevAgent.Domain.Tasks;

/// <summary>Task lifecycle. Persisted as UPPER_SNAKE strings so new values can be added without renumbering.</summary>
public enum AgentTaskStatus
{
    Pending = 1,
    Planning = 2,
    NeedsClarification = 3,
    WaitingForApproval = 4,
    InProgress = 5,
    Testing = 6,
    Reviewing = 7,
    Publishing = 8,
    Completed = 9,
    Rejected = 10,
    Failed = 11,
    Blocked = 12,
    Cancelled = 13,
    TimedOut = 14,
}

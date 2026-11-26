namespace DevAgent.Domain.Tasks;

/// <summary>The only valid status transitions. Every other transition is rejected by the aggregate.</summary>
public static class AgentTaskStateMachine
{
    private static readonly Dictionary<AgentTaskStatus, AgentTaskStatus[]> Allowed = new()
    {
        [AgentTaskStatus.Pending] = [AgentTaskStatus.Planning, AgentTaskStatus.Failed, AgentTaskStatus.Blocked, AgentTaskStatus.Cancelled],
        [AgentTaskStatus.Planning] =
        [
            AgentTaskStatus.NeedsClarification, AgentTaskStatus.WaitingForApproval, AgentTaskStatus.InProgress,
            AgentTaskStatus.Failed, AgentTaskStatus.Blocked, AgentTaskStatus.Cancelled, AgentTaskStatus.TimedOut,
        ],
        [AgentTaskStatus.NeedsClarification] = [AgentTaskStatus.Planning, AgentTaskStatus.Cancelled, AgentTaskStatus.TimedOut],
        [AgentTaskStatus.WaitingForApproval] =
        [
            AgentTaskStatus.InProgress, AgentTaskStatus.Publishing, AgentTaskStatus.Planning,
            AgentTaskStatus.Rejected, AgentTaskStatus.Cancelled, AgentTaskStatus.TimedOut,
        ],
        [AgentTaskStatus.InProgress] =
        [
            AgentTaskStatus.Testing, AgentTaskStatus.WaitingForApproval, AgentTaskStatus.Failed,
            AgentTaskStatus.Blocked, AgentTaskStatus.Cancelled, AgentTaskStatus.TimedOut,
        ],
        [AgentTaskStatus.Testing] =
        [
            AgentTaskStatus.Reviewing, AgentTaskStatus.InProgress, AgentTaskStatus.Failed,
            AgentTaskStatus.Blocked, AgentTaskStatus.Cancelled, AgentTaskStatus.TimedOut,
        ],
        [AgentTaskStatus.Reviewing] =
        [
            AgentTaskStatus.Publishing, AgentTaskStatus.WaitingForApproval, AgentTaskStatus.InProgress,
            AgentTaskStatus.Failed, AgentTaskStatus.Blocked, AgentTaskStatus.Cancelled, AgentTaskStatus.TimedOut,
        ],
        [AgentTaskStatus.Publishing] = [AgentTaskStatus.Completed, AgentTaskStatus.Failed, AgentTaskStatus.Blocked],

        // Operators may retry failed, blocked or timed-out tasks; the task returns to Pending and keeps its history.
        [AgentTaskStatus.Failed] = [AgentTaskStatus.Pending],
        [AgentTaskStatus.Blocked] = [AgentTaskStatus.Pending, AgentTaskStatus.Cancelled],
        [AgentTaskStatus.TimedOut] = [AgentTaskStatus.Pending],
        [AgentTaskStatus.Completed] = [],
        [AgentTaskStatus.Rejected] = [],
        [AgentTaskStatus.Cancelled] = [],
    };

    public static bool CanTransition(AgentTaskStatus from, AgentTaskStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static bool IsTerminal(AgentTaskStatus status) =>
        status is AgentTaskStatus.Completed or AgentTaskStatus.Rejected or AgentTaskStatus.Cancelled;

    /// <summary>Statuses the worker is actively processing (or is about to).</summary>
    public static bool IsActive(AgentTaskStatus status) =>
        status is AgentTaskStatus.Pending or AgentTaskStatus.Planning or AgentTaskStatus.InProgress
            or AgentTaskStatus.Testing or AgentTaskStatus.Reviewing or AgentTaskStatus.Publishing;

    public static IReadOnlyCollection<AgentTaskStatus> NextStates(AgentTaskStatus from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : [];
}

using DevAgent.Domain.Common;

namespace DevAgent.Domain.Tasks;

/// <summary>
/// One status change: who, when and why. This is the timeline the dashboard replays after a SignalR reconnect.
/// </summary>
public sealed class TaskStatusTransition
{
    private TaskStatusTransition()
    {
        ActorId = string.Empty;
    }

    internal TaskStatusTransition(
        long sequence,
        AgentTaskStatus? from,
        AgentTaskStatus to,
        Actor actor,
        string? reason,
        DateTimeOffset occurredAt)
    {
        Sequence = sequence;
        From = from;
        To = to;
        ActorKind = actor.Kind;
        ActorId = actor.Id;
        Reason = reason;
        OccurredAt = occurredAt;
    }

    /// <summary>Monotonic per task, so clients can replay missed entries in order.</summary>
    public long Sequence { get; private set; }

    public AgentTaskStatus? From { get; private set; }

    public AgentTaskStatus To { get; private set; }

    public ActorKind ActorKind { get; private set; }

    public string ActorId { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}

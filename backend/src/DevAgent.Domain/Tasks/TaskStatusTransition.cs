using DevAgent.Domain.Common;

namespace DevAgent.Domain.Tasks;

/// <summary>
/// Bản ghi một lần chuyển trạng thái: ai, khi nào, vì sao. Đây là xương sống của timeline
/// mà dashboard đọc bù sau khi mất kết nối SignalR.
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

    /// <summary>Số thứ tự tăng dần trong task, dùng để client đọc bù đúng thứ tự.</summary>
    public long Sequence { get; private set; }

    public AgentTaskStatus? From { get; private set; }

    public AgentTaskStatus To { get; private set; }

    public ActorKind ActorKind { get; private set; }

    public string ActorId { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}

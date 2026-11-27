using DevAgent.Domain.Common;

namespace DevAgent.Domain.Auditing;

/// <summary>
/// Immutable, append-only audit record: who did what to which resource, whether it was allowed and why.
/// Attached data must already be free of tokens and secrets.
/// </summary>
public sealed class AuditEntry : Entity<AuditEntryId>
{
    private AuditEntry()
    {
        ActorId = string.Empty;
        Action = string.Empty;
        ResourceType = string.Empty;
        ResourceId = string.Empty;
    }

    private AuditEntry(
        AuditEntryId id,
        Actor actor,
        string action,
        string resourceType,
        string resourceId,
        ProjectId? projectId,
        TaskId? taskId,
        AuditOutcome outcome,
        string? reason,
        string? dataJson,
        string? traceId,
        DateTimeOffset occurredAt)
        : base(id)
    {
        ActorKind = actor.Kind;
        ActorId = actor.Id;
        Action = action;
        ResourceType = resourceType;
        ResourceId = resourceId;
        ProjectId = projectId;
        TaskId = taskId;
        Outcome = outcome;
        Reason = reason;
        DataJson = dataJson;
        TraceId = traceId;
        OccurredAt = occurredAt;
    }

    public ActorKind ActorKind { get; private set; }

    public string ActorId { get; private set; }

    public string Action { get; private set; }

    public string ResourceType { get; private set; }

    public string ResourceId { get; private set; }

    public ProjectId? ProjectId { get; private set; }

    public TaskId? TaskId { get; private set; }

    public AuditOutcome Outcome { get; private set; }

    public string? Reason { get; private set; }

    public string? DataJson { get; private set; }

    public string? TraceId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static AuditEntry Record(
        Actor actor,
        string action,
        string resourceType,
        string resourceId,
        AuditOutcome outcome,
        DateTimeOffset now,
        ProjectId? projectId = null,
        TaskId? taskId = null,
        string? reason = null,
        string? dataJson = null,
        string? traceId = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return new AuditEntry(
            AuditEntryId.New(),
            actor,
            Guard.NotEmpty(action, "action", 100),
            Guard.NotEmpty(resourceType, "resource_type", 100),
            Guard.NotEmpty(resourceId, "resource_id", 200),
            projectId,
            taskId,
            outcome,
            Guard.Optional(reason, "reason", 4000),
            dataJson,
            Guard.Optional(traceId, "trace_id", 64),
            Guard.Utc(now, "occurred_at"));
    }
}

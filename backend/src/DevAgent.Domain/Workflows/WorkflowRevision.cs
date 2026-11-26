using DevAgent.Domain.Common;

namespace DevAgent.Domain.Workflows;

public readonly record struct WorkflowRevisionId(Guid Value)
{
    public static WorkflowRevisionId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

/// <summary>
/// Một phiên bản quy trình của repo (append-only): ai đổi, khi nào, vì sao, nội dung gì.
/// Dùng để audit và để biết task đã chạy theo phiên bản nào.
/// </summary>
public sealed class WorkflowRevision : AggregateRoot<WorkflowRevisionId>
{
    private WorkflowRevision()
    {
        Definition = WorkflowDefinition.Default;
    }

    internal WorkflowRevision(
        WorkflowRevisionId id,
        RepositoryId repositoryId,
        int version,
        WorkflowDefinition definition,
        UserId changedBy,
        string? comment,
        DateTimeOffset changedAt)
        : base(id)
    {
        RepositoryId = repositoryId;
        Version = version;
        Definition = definition;
        ChangedBy = changedBy;
        Comment = comment;
        ChangedAt = changedAt;
    }

    public RepositoryId RepositoryId { get; private set; }

    public int Version { get; private set; }

    public WorkflowDefinition Definition { get; private set; }

    public UserId ChangedBy { get; private set; }

    public string? Comment { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }
}

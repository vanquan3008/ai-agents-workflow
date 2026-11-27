using DevAgent.Domain.Common;

namespace DevAgent.Domain.Workflows;

/// <summary>
/// One append-only version of a repository workflow: who changed it, when, why and to what.
/// Used for auditing and for knowing which version a task ran with.
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

using DevAgent.Domain.Common;

namespace DevAgent.Domain.Approvals;

public sealed record ApprovalDecided(ApprovalId ApprovalId, TaskId TaskId, ApprovalStatus Status, UserId DecidedBy, DateTimeOffset OccurredAt)
    : IDomainEvent;

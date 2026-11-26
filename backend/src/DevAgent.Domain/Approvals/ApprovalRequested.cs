using DevAgent.Domain.Common;

namespace DevAgent.Domain.Approvals;

public sealed record ApprovalRequested(ApprovalId ApprovalId, TaskId TaskId, ApprovalKind Kind, DateTimeOffset OccurredAt) : IDomainEvent;

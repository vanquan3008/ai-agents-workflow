using DevAgent.Domain.Common;

namespace DevAgent.Domain.Tasks;

public sealed record TaskStatusChanged(
    TaskId TaskId,
    ProjectId ProjectId,
    long Sequence,
    AgentTaskStatus? From,
    AgentTaskStatus To,
    string? Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;

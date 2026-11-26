using DevAgent.Domain.Common;

namespace DevAgent.Domain.Tasks;

public sealed record TaskCreated(TaskId TaskId, ProjectId ProjectId, RepositoryId RepositoryId, TaskChannel Channel, DateTimeOffset OccurredAt)
    : IDomainEvent;

public sealed record TaskStatusChanged(
    TaskId TaskId,
    ProjectId ProjectId,
    long Sequence,
    AgentTaskStatus? From,
    AgentTaskStatus To,
    string? Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record TaskPlanCreated(TaskId TaskId, ProjectId ProjectId, int PlanVersion, DateTimeOffset OccurredAt) : IDomainEvent;

using DevAgent.Domain.Common;

namespace DevAgent.Domain.Tasks;

public sealed record TaskPlanCreated(TaskId TaskId, ProjectId ProjectId, int PlanVersion, DateTimeOffset OccurredAt) : IDomainEvent;

using DevAgent.Domain.Common;

namespace DevAgent.Domain.Tasks;

public sealed record TaskCreated(TaskId TaskId, ProjectId ProjectId, RepositoryId RepositoryId, TaskChannel Channel, DateTimeOffset OccurredAt)
    : IDomainEvent;

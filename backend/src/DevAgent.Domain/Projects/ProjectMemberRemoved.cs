using DevAgent.Domain.Common;

namespace DevAgent.Domain.Projects;

/// <summary>A user lost membership of a project and with it every project-scoped read, including realtime events.</summary>
public sealed record ProjectMemberRemoved(ProjectId ProjectId, UserId UserId, DateTimeOffset OccurredAt) : IDomainEvent;

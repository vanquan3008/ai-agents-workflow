using DevAgent.Domain.Common;

namespace DevAgent.Domain.Users;

/// <summary>
/// The user's security stamp changed (password, role or activation), so every issued access token and open realtime
/// connection must stop working.
/// </summary>
public sealed record UserSessionsInvalidated(UserId UserId, DateTimeOffset OccurredAt) : IDomainEvent;

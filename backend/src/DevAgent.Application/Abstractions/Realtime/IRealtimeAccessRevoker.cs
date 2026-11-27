using DevAgent.Domain.Common;

namespace DevAgent.Application.Abstractions.Realtime;

/// <summary>
/// A revocation broadcast to every API instance. With <see cref="ProjectId"/> the user's subscriptions in that project
/// end; without it every realtime connection of the user is closed and must reconnect with a fresh token.
/// </summary>
public sealed record RealtimeAccessRevocation(Guid UserId, Guid? ProjectId);

/// <summary>
/// Ends realtime delivery when access is lost. Subscriptions are authorized once, so without this a connection that
/// already joined a group would keep receiving events. Call after the change has committed so reconnects see it.
/// </summary>
public interface IRealtimeAccessRevoker
{
    Task RevokeProjectAsync(UserId userId, ProjectId projectId, CancellationToken cancellationToken);

    Task DisconnectUserAsync(UserId userId, CancellationToken cancellationToken);
}

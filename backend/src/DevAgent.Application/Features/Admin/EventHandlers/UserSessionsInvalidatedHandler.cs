using DevAgent.Application.Abstractions.Events;
using DevAgent.Application.Abstractions.Realtime;
using DevAgent.Domain.Users;

namespace DevAgent.Application.Features.Admin.EventHandlers;

/// <summary>
/// Closes the user's realtime connections after a password, role or activation change. Reconnecting requires a token
/// with the new security stamp, so a deactivated or demoted user cannot keep listening.
/// </summary>
internal sealed class UserSessionsInvalidatedHandler(IRealtimeAccessRevoker revoker) : IDomainEventHandler<UserSessionsInvalidated>
{
    public Task HandleAsync(UserSessionsInvalidated domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return revoker.DisconnectUserAsync(domainEvent.UserId, cancellationToken);
    }
}

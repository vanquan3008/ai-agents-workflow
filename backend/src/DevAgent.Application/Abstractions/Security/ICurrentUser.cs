using DevAgent.Domain.Common;

namespace DevAgent.Application.Abstractions.Security;

/// <summary>
/// The user behind the current request. Identity always comes from a validated access token, never from the payload.
/// Null inside the worker, where actions run on behalf of the system or an agent.
/// </summary>
public interface ICurrentUser
{
    UserId? UserId { get; }

    bool IsAuthenticated => UserId.HasValue;
}

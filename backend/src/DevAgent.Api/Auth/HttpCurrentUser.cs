using System.Security.Claims;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Domain.Common;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DevAgent.Api.Auth;

/// <summary>
/// Reads the DevAgent user id (<c>sub</c> claim) from the authenticated principal. SignalR hubs assign the connection's
/// principal through <see cref="UsePrincipal"/> because HttpContext is not always available inside hub calls.
/// </summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? _principal;

    public UserId? UserId
    {
        get
        {
            var principal = _principal ?? accessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var raw = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(raw, out var id) ? new UserId(id) : null;
        }
    }

    public void UsePrincipal(ClaimsPrincipal? principal) => _principal = principal;
}

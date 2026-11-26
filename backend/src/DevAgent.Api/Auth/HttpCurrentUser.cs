using System.Security.Claims;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Domain.Common;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DevAgent.Api.Auth;

/// <summary>
/// Đọc DevAgent user id (claim <c>sub</c>) từ principal đã xác thực. Với SignalR, hub gán principal của kết nối
/// qua <see cref="UsePrincipal"/> vì HttpContext không luôn sẵn trong lời gọi hub.
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

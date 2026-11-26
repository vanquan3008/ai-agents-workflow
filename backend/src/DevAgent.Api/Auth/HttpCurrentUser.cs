using System.Security.Claims;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Domain.Common;
using Microsoft.Extensions.Options;

namespace DevAgent.Api.Auth;

/// <summary>
/// Đọc DevAgent user id từ claim của principal đã xác thực. Với SignalR, hub gán principal của kết nối
/// qua <see cref="UsePrincipal"/> vì HttpContext không luôn sẵn trong lời gọi hub.
/// </summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor, IOptions<AuthOptions> options) : ICurrentUser
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

            var raw = principal.FindFirst(options.Value.Jwt.UserIdClaim)?.Value;
            return Guid.TryParse(raw, out var id) ? new UserId(id) : null;
        }
    }

    public void UsePrincipal(ClaimsPrincipal? principal) => _principal = principal;
}

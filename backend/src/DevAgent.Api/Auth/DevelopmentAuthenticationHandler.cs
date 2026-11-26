using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DevAgent.Api.Auth;

/// <summary>
/// Xác thực chỉ dành cho phát triển/kiểm thử: danh tính lấy từ header <c>X-DevAgent-User</c>.
/// Quyền hiệu lực vẫn do <c>ProjectAccess</c> kiểm tra như môi trường thật.
/// </summary>
internal sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<AuthOptions> authOptions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Development";
    public const string HeaderName = "X-DevAgent-User";
    public const string QueryName = "dev_user";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? raw = Request.Headers[HeaderName];
        if (string.IsNullOrWhiteSpace(raw) && Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
        {
            raw = Request.Query[QueryName];
        }

        Guid userId;
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (authOptions.Value.Development.DefaultUserId is not { } fallback)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            userId = fallback;
        }
        else if (!Guid.TryParse(raw, out userId))
        {
            return Task.FromResult(AuthenticateResult.Fail($"{HeaderName} phải là Guid."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(authOptions.Value.Jwt.UserIdClaim, userId.ToString()), new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

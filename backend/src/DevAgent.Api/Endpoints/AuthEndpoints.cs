using DevAgent.Api.Auth;
using DevAgent.Api.Common;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Auth;
using Microsoft.Extensions.Options;

namespace DevAgent.Api.Endpoints;

/// <summary>
/// Sign-in and session endpoints. The access token is returned in the body (kept in memory by the client); the refresh
/// token lives only in an HttpOnly, SameSite=Strict cookie scoped to /api/v1/auth. Refresh and logout require the
/// <c>X-DevAgent-Client</c> header so cross-site (CSRF) requests are stopped by the CORS preflight.
/// </summary>
internal static class AuthEndpoints
{
    public const string ClientHeader = "X-DevAgent-Client";
    private const string CookiePath = "/api/v1/auth";

    public sealed record LoginRequest(string Email, string Password);

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    public sealed record SessionResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserDto User);

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginRequest body, HttpContext http, ICommandHandler<LoginCommand, AuthSession> handler, IOptions<AuthOptions> options, CancellationToken ct) =>
                (await handler.HandleAsync(new LoginCommand(body.Email, body.Password, Client(http)), ct)).ToHttp(s => Session(http, s, options.Value)))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithName("Login");

        group.MapPost("/refresh", async (HttpContext http, ICommandHandler<RefreshSessionCommand, AuthSession> handler, IOptions<AuthOptions> options, CancellationToken ct) =>
            {
                if (!http.Request.Headers.ContainsKey(ClientHeader))
                {
                    return Results.Problem("Thiếu header X-DevAgent-Client.", statusCode: StatusCodes.Status400BadRequest);
                }

                var raw = http.Request.Cookies[options.Value.RefreshCookie.Name] ?? string.Empty;
                var result = await handler.HandleAsync(new RefreshSessionCommand(raw, Client(http)), ct);
                if (result.IsFailure)
                {
                    ClearCookie(http, options.Value);
                }

                return result.ToHttp(s => Session(http, s, options.Value));
            })
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithName("RefreshSession");

        group.MapPost("/logout", async (HttpContext http, ICommandHandler<LogoutCommand, bool> handler, IOptions<AuthOptions> options, CancellationToken ct) =>
            {
                if (!http.Request.Headers.ContainsKey(ClientHeader))
                {
                    return Results.Problem("Thiếu header X-DevAgent-Client.", statusCode: StatusCodes.Status400BadRequest);
                }

                await handler.HandleAsync(new LogoutCommand(http.Request.Cookies[options.Value.RefreshCookie.Name]), ct);
                ClearCookie(http, options.Value);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithName("Logout");

        group.MapGet("/me", async (IQueryHandler<GetCurrentUserQuery, CurrentUserDto> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetCurrentUserQuery(), ct)).ToHttp())
            .RequireAuthorization()
            .WithName("CurrentUser");

        group.MapPost("/change-password", async (ChangePasswordRequest body, HttpContext http, ICommandHandler<ChangePasswordCommand, AuthSession> handler, IOptions<AuthOptions> options, CancellationToken ct) =>
                (await handler.HandleAsync(new ChangePasswordCommand(body.CurrentPassword, body.NewPassword, Client(http)), ct)).ToHttp(s => Session(http, s, options.Value)))
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithName("ChangePassword");

        return api;
    }

    private static ClientInfo Client(HttpContext http) =>
        new(http.Connection.RemoteIpAddress?.ToString(), http.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua[..Math.Min(ua.Length, 500)] : null);

    private static IResult Session(HttpContext http, AuthSession session, AuthOptions options)
    {
        http.Response.Cookies.Append(options.RefreshCookie.Name, session.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = options.RefreshCookie.Secure,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            Expires = session.RefreshTokenExpiresAt,
            IsEssential = true,
        });
        http.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new SessionResponse(session.AccessToken.Token, session.AccessToken.ExpiresAt, session.User));
    }

    private static void ClearCookie(HttpContext http, AuthOptions options) =>
        http.Response.Cookies.Delete(options.RefreshCookie.Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = options.RefreshCookie.Secure,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
        });
}

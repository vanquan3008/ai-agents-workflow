using DevAgent.Application.Abstractions.Security;
using DevAgent.Domain.Common;
using DevAgent.Domain.Users;
using DevAgent.Domain.Users.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DevAgent.Api.Auth;

internal static class AuthExtensions
{
    public const string SuperAdminPolicy = "SuperAdmin";

    public static IServiceCollection AddDevAgentAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>().Bind(configuration.GetSection(AuthOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<AuthOptions>, AuthOptionsValidator>();
        services.AddHttpContextAccessor();
        services.AddScoped<HttpCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<HttpCurrentUser>());
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>>((bearer, auth) =>
            {
                var jwt = auth.Value.Jwt;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = JwtTokenService.SigningKey(jwt),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Name,
                    RoleClaimType = DevAgentClaims.Role,
                };
                bearer.Events = new JwtBearerEvents
                {
                    // WebSockets cannot send an Authorization header, so SignalR passes the token in the query string.
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },

                    // Reject validly signed tokens immediately once the account was deactivated or its password/role changed.
                    OnTokenValidated = async context =>
                    {
                        var sub = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                        var stamp = context.Principal?.FindFirst(DevAgentClaims.SecurityStamp)?.Value;
                        if (!Guid.TryParse(sub, out var userId) || string.IsNullOrEmpty(stamp))
                        {
                            context.Fail("Token thiếu thông tin người dùng.");
                            return;
                        }

                        var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                        var user = await users.GetByIdAsync(new UserId(userId), context.HttpContext.RequestAborted);
                        if (user is not { IsActive: true } || user.SecurityStamp != stamp)
                        {
                            context.Fail("Phiên đăng nhập không còn hiệu lực.");
                        }
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(SuperAdminPolicy, policy => policy.RequireAuthenticatedUser().RequireClaim(DevAgentClaims.Role, "SUPER_ADMIN"));

        return services;
    }

    /// <summary>
    /// Accounts on a temporary password may only call the auth endpoints (read self, change password, sign out).
    /// </summary>
    public static RouteGroupBuilder RequirePasswordChanged(this RouteGroupBuilder group) =>
        group.AddEndpointFilter(async (context, next) =>
        {
            var mustChange = context.HttpContext.User.FindFirst(DevAgentClaims.MustChangePassword)?.Value == "true";
            return mustChange
                ? Results.Problem(
                    "Bạn cần đổi mật khẩu tạm trước khi tiếp tục.",
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Cần đổi mật khẩu",
                    extensions: new Dictionary<string, object?> { ["code"] = "auth.password_change_required" })
                : await next(context);
        });
}

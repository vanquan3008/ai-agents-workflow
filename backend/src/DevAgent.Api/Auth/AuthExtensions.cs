using DevAgent.Application.Abstractions.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace DevAgent.Api.Auth;

internal static class AuthExtensions
{
    public static IServiceCollection AddDevAgentAuth(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<AuthOptions>().Bind(configuration.GetSection(AuthOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddHttpContextAccessor();
        services.AddScoped<HttpCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<HttpCurrentUser>());

        var auth = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new();
        if (auth.Development.Enabled)
        {
            if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
            {
                throw new InvalidOperationException("Auth:Development:Enabled chỉ được bật ở môi trường Development hoặc Testing.");
            }

            services.AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.SchemeName, null);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(auth.Jwt.Authority))
            {
                throw new InvalidOperationException("Auth:Jwt:Authority là bắt buộc khi không dùng xác thực Development.");
            }

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = auth.Jwt.Authority;
                    options.Audience = auth.Jwt.Audience;
                    options.RequireHttpsMetadata = auth.Jwt.RequireHttpsMetadata;
                    options.MapInboundClaims = false;
                    options.Events = new JwtBearerEvents
                    {
                        // WebSocket không gửi được header Authorization: SignalR truyền token qua query.
                        OnMessageReceived = context =>
                        {
                            var token = context.Request.Query["access_token"];
                            if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
                            {
                                context.Token = token;
                            }

                            return Task.CompletedTask;
                        },
                    };
                });
        }

        services.AddAuthorization();
        return services;
    }
}

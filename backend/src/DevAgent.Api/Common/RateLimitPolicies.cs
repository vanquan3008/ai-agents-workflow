using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace DevAgent.Api.Common;

internal static class RateLimitPolicies
{
    public const string TaskCreation = "task-creation";
    public const string Auth = "auth";

    /// <summary>Giới hạn tạo task theo người dùng để chặn spam và chi phí model ngoài ý muốn.</summary>
    public static IServiceCollection AddDevAgentRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitPerMinute = configuration.GetValue("RateLimiting:TaskCreationPerMinute", 10);
        var authPerMinute = configuration.GetValue("RateLimiting:AuthPerMinute", 10);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(TaskCreation, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            // Chống dò mật khẩu: giới hạn theo IP cho đăng nhập/refresh/đổi mật khẩu.
            options.AddPolicy(Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });
        return services;
    }
}

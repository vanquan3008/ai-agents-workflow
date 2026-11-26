using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace DevAgent.Api.Common;

internal static class RateLimitPolicies
{
    public const string TaskCreation = "task-creation";

    /// <summary>Giới hạn tạo task theo người dùng để chặn spam và chi phí model ngoài ý muốn.</summary>
    public static IServiceCollection AddDevAgentRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitPerMinute = configuration.GetValue("RateLimiting:TaskCreationPerMinute", 10);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(TaskCreation, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.Identity?.Name ?? context.User.FindFirst("devagent_uid")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });
        return services;
    }
}

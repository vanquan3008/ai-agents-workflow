using System.Net.Http.Json;
using DevAgent.Application.Abstractions.Integrations;
using DevAgent.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevAgent.Infrastructure.Integrations;

/// <summary>Discord tắt: chỉ ghi log thông báo (không chứa secret).</summary>
internal sealed partial class LoggingChatNotifier(ILogger<LoggingChatNotifier> logger) : IChatNotifier
{
    public Task NotifyAsync(ChatNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        LogNotification(logger, notification.Kind, notification.TaskId, notification.Link);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "[chat:disabled] {Kind} cho task {TaskId} ({Link})")]
    private static partial void LogNotification(ILogger logger, ChatNotificationKind kind, Guid taskId, string? link);
}

/// <summary>
/// Gửi thông báo vào kênh Discord qua REST API (bot token). Nhận lệnh slash command thuộc Phase 2D.
/// HttpClient được cấu hình header Authorization ở DI; token không xuất hiện trong log.
/// </summary>
internal sealed partial class DiscordChatNotifier(HttpClient http, IOptions<DiscordOptions> options, ILogger<DiscordChatNotifier> logger)
    : IChatNotifier
{
    public async Task NotifyAsync(ChatNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var channelId = options.Value.NotificationChannelId;
        var icon = notification.Kind switch
        {
            ChatNotificationKind.Completed => "✅",
            ChatNotificationKind.ApprovalRequired => "🛂",
            ChatNotificationKind.ClarificationNeeded => "❓",
            ChatNotificationKind.Failed or ChatNotificationKind.Blocked => "⚠️",
            _ => "ℹ️",
        };

        var content = $"{icon} **{notification.Title}** (`{notification.TaskId}`)\n{notification.Message}";
        if (notification.Link is not null)
        {
            content += $"\n{notification.Link}";
        }

        using var response = await http.PostAsJsonAsync(
            new Uri($"channels/{channelId}/messages", UriKind.Relative),
            new { content = content.Length > 1900 ? content[..1900] : content, allowed_mentions = new { parse = Array.Empty<string>() } },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LogSendFailed(logger, (int)response.StatusCode, notification.TaskId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Discord trả về {StatusCode} khi gửi thông báo cho task {TaskId}")]
    private static partial void LogSendFailed(ILogger logger, int statusCode, Guid taskId);
}

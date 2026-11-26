using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevAgent.Application.Abstractions.Integrations;
using DevAgent.Application.Abstractions.Messaging;
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
/// - Chống gửi trùng khi job chạy lại: <c>nonce</c> suy ra từ khóa thông báo + <c>enforce_nonce</c>.
/// - Giới hạn tần suất (429): ném <see cref="RetryableJobException"/> kèm <c>Retry-After</c> để hàng đợi chờ đúng thời gian.
/// - 5xx, timeout, lỗi mạng: thử lại. 401/403/404 (token/kênh sai): lỗi vĩnh viễn, chuyển dead-letter.
/// </summary>
internal sealed partial class DiscordChatNotifier(HttpClient http, IOptions<DiscordOptions> options, ILogger<DiscordChatNotifier> logger)
    : IChatNotifier
{
    private const int MaxContentLength = 1900;

    public async Task NotifyAsync(ChatNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var body = new
        {
            content = Compose(notification),
            nonce = Nonce(notification.DeduplicationKey),
            enforce_nonce = true,
            allowed_mentions = new { parse = Array.Empty<string>() },
        };

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(
                new Uri($"channels/{options.Value.NotificationChannelId}/messages", UriKind.Relative),
                body,
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new RetryableJobException("Không kết nối được Discord.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RetryableJobException("Discord không phản hồi trong thời gian cho phép.", ex);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var status = (int)response.StatusCode;
            LogSendFailed(logger, status, notification.TaskId);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new RetryableJobException("Discord giới hạn tần suất (429).")
                {
                    RetryAfter = await RetryAfterAsync(response, cancellationToken),
                };
            }

            if (status >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout)
            {
                throw new RetryableJobException($"Discord lỗi tạm thời ({status}).");
            }

            throw new PermanentJobException(
                $"Discord từ chối thông báo ({status}). Kiểm tra Discord:BotToken, quyền bot và Discord:NotificationChannelId.");
        }
    }

    internal static string Compose(ChatNotification n)
    {
        var icon = n.Kind switch
        {
            ChatNotificationKind.Completed => "✅",
            ChatNotificationKind.ApprovalRequired => "🛂",
            ChatNotificationKind.ClarificationNeeded => "❓",
            ChatNotificationKind.Failed or ChatNotificationKind.Blocked => "⚠️",
            _ => "ℹ️",
        };

        var content = $"{icon} **{n.Title}** (`{n.TaskId}`)\n{n.Message}";
        if (n.Link is not null)
        {
            content += $"\n{n.Link}";
        }

        return content.Length > MaxContentLength ? content[..MaxContentLength] : content;
    }

    /// <summary>Discord giới hạn nonce 25 ký tự: dùng băm ổn định của khóa thông báo.</summary>
    internal static string Nonce(string deduplicationKey) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(deduplicationKey)))[..25];

    private static async Task<TimeSpan> RetryAfterAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (response.Headers.TryGetValues("Retry-After", out var values)
            && double.TryParse(values.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out var headerSeconds))
        {
            return TimeSpan.FromSeconds(headerSeconds);
        }

        try
        {
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (json.RootElement.TryGetProperty("retry_after", out var retryAfter) && retryAfter.TryGetDouble(out var seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }
        }
        catch (JsonException)
        {
            // Thân phản hồi không phải JSON: dùng mặc định.
        }

        return TimeSpan.FromSeconds(5);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Discord trả về {StatusCode} khi gửi thông báo cho task {TaskId}")]
    private static partial void LogSendFailed(ILogger logger, int statusCode, Guid taskId);
}

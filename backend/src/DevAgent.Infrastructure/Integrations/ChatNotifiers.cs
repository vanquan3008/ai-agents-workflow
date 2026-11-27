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

/// <summary>Used when Discord is disabled: logs the notification (never secrets).</summary>
internal sealed partial class LoggingChatNotifier(ILogger<LoggingChatNotifier> logger) : IChatNotifier
{
    public Task NotifyAsync(ChatNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        LogNotification(logger, notification.Kind, notification.TaskId, notification.Link);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "[chat:disabled] {Kind} for task {TaskId} ({Link})")]
    private static partial void LogNotification(ILogger logger, ChatNotificationKind kind, Guid taskId, string? link);
}

/// <summary>
/// Posts notifications to a Discord channel through the REST API (bot token).
/// - Retried jobs never double-post: <c>nonce</c> is derived from the deduplication key with <c>enforce_nonce</c>.
/// - Rate limits (429) throw <see cref="RetryableJobException"/> carrying <c>Retry-After</c> so the queue waits exactly that long.
/// - 5xx, timeouts and network errors are retried; 401/403/404 (bad token or channel) are permanent and dead-lettered.
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

    /// <summary>Discord caps nonces at 25 characters, so a stable hash of the deduplication key is used.</summary>
    internal static string Nonce(string deduplicationKey) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(deduplicationKey)))[..25];

    private async Task<TimeSpan> RetryAfterAsync(HttpResponseMessage response, CancellationToken cancellationToken)
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
            // Non-JSON body: fall back to the default delay.
        }

        return TimeSpan.FromSeconds(options.Value.RetryAfterFallbackSeconds);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Discord returned {StatusCode} for the notification of task {TaskId}")]
    private static partial void LogSendFailed(ILogger logger, int statusCode, Guid taskId);
}

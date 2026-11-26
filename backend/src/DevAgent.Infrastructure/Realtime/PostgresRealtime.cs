using System.Text.Json;
using DevAgent.Application.Abstractions.Realtime;
using DevAgent.Infrastructure.Options;
using DevAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DevAgent.Infrastructure.Realtime;

internal static class RealtimeChannel
{
    public const string Name = "devagent_task_events";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
}

/// <summary>
/// Phát sự kiện qua <c>pg_notify</c>. Worker và API dùng chung PostgreSQL nên không cần thêm broker.
/// Khi cần fan-out nhiều instance API với tải lớn hoặc payload &gt; 8KB, chuyển sang Redis backplane (xem ADR trong README).
/// </summary>
internal sealed partial class PostgresTaskEventPublisher(DevAgentDbContext context, ILogger<PostgresTaskEventPublisher> logger)
    : ITaskEventPublisher
{
    public async Task PublishAsync(TaskRealtimeEvent taskEvent, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(taskEvent, RealtimeChannel.Json);
        try
        {
            await context.Database.ExecuteSqlAsync($"SELECT pg_notify({RealtimeChannel.Name}, {payload})", cancellationToken);
        }
        catch (NpgsqlException ex)
        {
            // Best-effort: dashboard đọc bù từ timeline đã lưu.
            LogPublishFailed(logger, ex, taskEvent.TaskId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Không phát được sự kiện realtime cho task {TaskId}")]
    private static partial void LogPublishFailed(ILogger logger, Exception exception, Guid taskId);
}

/// <summary>Đích nhận sự kiện realtime trong tiến trình API (hiện thực bằng SignalR).</summary>
public interface ITaskEventSink
{
    Task DeliverAsync(TaskRealtimeEvent taskEvent, CancellationToken cancellationToken);
}

/// <summary>LISTEN kênh PostgreSQL và chuyển sự kiện cho <see cref="ITaskEventSink"/>. Tự kết nối lại khi mất kết nối.</summary>
internal sealed partial class PostgresTaskEventListener(
    IConfiguration configuration,
    ITaskEventSink sink,
    ILogger<PostgresTaskEventListener> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName)
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Postgres.");
        var delay = TimeSpan.FromSeconds(1);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(stoppingToken);
                connection.Notification += (_, args) => OnNotification(args.Payload, stoppingToken);

                await using (var command = new NpgsqlCommand($"LISTEN {RealtimeChannel.Name}", connection))
                {
                    await command.ExecuteNonQueryAsync(stoppingToken);
                }

                LogListening(logger, RealtimeChannel.Name);
                delay = TimeSpan.FromSeconds(1);
                while (!stoppingToken.IsCancellationRequested)
                {
                    await connection.WaitAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (NpgsqlException ex)
            {
                LogListenFailed(logger, ex, delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
            }
        }
    }

    private void OnNotification(string payload, CancellationToken cancellationToken)
    {
        TaskRealtimeEvent? taskEvent;
        try
        {
            taskEvent = JsonSerializer.Deserialize<TaskRealtimeEvent>(payload, RealtimeChannel.Json);
        }
        catch (JsonException ex)
        {
            LogBadPayload(logger, ex);
            return;
        }

        if (taskEvent is not null)
        {
            // Không chặn vòng LISTEN; lỗi gửi được log trong continuation.
            _ = sink.DeliverAsync(taskEvent, cancellationToken).ContinueWith(
                t => LogDeliverFailed(logger, t.Exception!, taskEvent.TaskId),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Đang LISTEN kênh realtime {Channel}")]
    private static partial void LogListening(ILogger logger, string channel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mất kết nối LISTEN, thử lại sau {DelaySeconds}s")]
    private static partial void LogListenFailed(ILogger logger, Exception exception, double delaySeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Payload realtime không hợp lệ")]
    private static partial void LogBadPayload(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Không gửi được sự kiện realtime của task {TaskId}")]
    private static partial void LogDeliverFailed(ILogger logger, Exception exception, Guid taskId);
}

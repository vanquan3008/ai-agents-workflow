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
/// Publishes through <c>pg_notify</c>; worker and API already share PostgreSQL, so no extra broker is needed.
/// Move to a Redis backplane when fanning out to many API instances under load or with payloads over 8 KB.
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
            // Best effort: the dashboard replays the persisted timeline.
            LogPublishFailed(logger, ex, taskEvent.TaskId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to publish realtime event for task {TaskId}")]
    private static partial void LogPublishFailed(ILogger logger, Exception exception, Guid taskId);
}

/// <summary>Realtime event sink inside the API process (implemented with SignalR).</summary>
public interface ITaskEventSink
{
    Task DeliverAsync(TaskRealtimeEvent taskEvent, CancellationToken cancellationToken);
}

/// <summary>LISTENs on the PostgreSQL channel and forwards events to <see cref="ITaskEventSink"/>, reconnecting with backoff.</summary>
internal sealed partial class PostgresTaskEventListener(
    IConfiguration configuration,
    ITaskEventSink sink,
    ILogger<PostgresTaskEventListener> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName)
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is missing.");
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
            // Never block the LISTEN loop; delivery failures are logged in the continuation.
            _ = sink.DeliverAsync(taskEvent, cancellationToken).ContinueWith(
                t => LogDeliverFailed(logger, t.Exception!, taskEvent.TaskId),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Listening on realtime channel {Channel}")]
    private static partial void LogListening(ILogger logger, string channel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LISTEN connection lost, retrying in {DelaySeconds}s")]
    private static partial void LogListenFailed(ILogger logger, Exception exception, double delaySeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid realtime payload")]
    private static partial void LogBadPayload(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deliver realtime event for task {TaskId}")]
    private static partial void LogDeliverFailed(ILogger logger, Exception exception, Guid taskId);
}

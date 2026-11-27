using DevAgent.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace DevAgent.Api.Realtime;

/// <summary>
/// Closes connections whose access token has expired. SignalR authenticates only when a connection opens, so without
/// this a socket could outlive its token indefinitely; the client reconnects with a fresh token and is re-authorized.
/// </summary>
internal sealed partial class RealtimeConnectionSweeper(
    RealtimeConnectionRegistry registry,
    IOptions<RealtimeOptions> options,
    TimeProvider clock,
    ILogger<RealtimeConnectionSweeper> logger) : BackgroundService
{
    public int Sweep()
    {
        var expired = registry.ExpiredAt(clock.GetUtcNow());
        foreach (var connection in expired)
        {
            connection.Abort();
        }

        if (expired.Count > 0)
        {
            LogClosed(logger, expired.Count);
        }

        return expired.Count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.ExpirySweepIntervalSeconds), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                Sweep();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Closed {Count} realtime connections with an expired access token")]
    private static partial void LogClosed(ILogger logger, int count);
}

using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Orchestration;
using DevAgent.Domain.Agents;
using Microsoft.Extensions.Options;

namespace DevAgent.Worker;

/// <summary>
/// Đăng ký AgentInstance và gửi heartbeat định kỳ vào DB (quan sát vận hành), đồng thời chạm file heartbeat
/// cho Docker healthcheck. Khi dừng, đánh dấu instance Stopped.
/// </summary>
internal sealed partial class HeartbeatService(
    IServiceScopeFactory scopeFactory,
    IWorkerIdentity identity,
    JobProcessingService jobs,
    IOptions<WorkerOptions> options,
    TimeProvider clock,
    ILogger<HeartbeatService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.HeartbeatIntervalSeconds));
        do
        {
            try
            {
                await BeatAsync(stoppingToken);
                TouchHeartbeatFile();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // Heartbeat lỗi không được làm dừng Worker.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogHeartbeatFailed(logger, ex);
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var instances = scope.ServiceProvider.GetRequiredService<IAgentInstanceRepository>();
            var instance = await instances.GetByWorkerIdAsync(identity.WorkerId, cancellationToken);
            if (instance is not null)
            {
                instance.Stop(clock.GetUtcNow());
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogHeartbeatFailed(logger, ex);
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            return await timer.WaitForNextTickAsync(token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task BeatAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var instances = scope.ServiceProvider.GetRequiredService<IAgentInstanceRepository>();
        var now = clock.GetUtcNow();
        var instance = await instances.GetByWorkerIdAsync(identity.WorkerId, cancellationToken);
        if (instance is null)
        {
            instance = AgentInstance.Register(identity.WorkerId, Environment.MachineName, now);
            instances.Add(instance);
        }
        else if (instance.Status == AgentInstanceStatus.Stopped)
        {
            instance.Restart(Environment.MachineName, now);
        }

        instance.Heartbeat(jobs.ActiveJobs, now);

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
    }

    private void TouchHeartbeatFile()
    {
        var path = options.Value.HeartbeatFile;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        File.WriteAllText(path, clock.GetUtcNow().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gửi heartbeat thất bại")]
    private static partial void LogHeartbeatFailed(ILogger logger, Exception exception);
}

using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.AgentRuntime.Jobs;
using DevAgent.Domain.Agents;
using DevAgent.Domain.Agents.Repositories;
using DevAgent.Worker.Jobs;
using Microsoft.Extensions.Options;

namespace DevAgent.Worker.Hosting;

/// <summary>
/// Registers the agent instance and writes periodic heartbeats to the database for observability, touching a heartbeat
/// file for the Docker healthcheck. Marks the instance Stopped on shutdown.
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
#pragma warning disable CA1031 // A failed heartbeat must not stop the worker.
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Heartbeat failed")]
    private static partial void LogHeartbeatFailed(ILogger logger, Exception exception);
}

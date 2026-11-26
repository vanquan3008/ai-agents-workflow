using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Worker.Hosting;
using Microsoft.Extensions.Options;

namespace DevAgent.Worker.Jobs;

/// <summary>
/// Reconciles dead-lettered jobs: for each job out of attempts (including when a worker died on its last attempt and only
/// the job row changed) the job type's handler updates business state (task → FAILED, executions closed with the reason).
/// Safe across parallel workers: jobs are claimed with SKIP LOCKED and released on failure for the next sweep.
/// </summary>
internal sealed partial class DeadLetterReconciler(
    IServiceScopeFactory scopeFactory,
    IOptions<WorkerOptions> options,
    TimeProvider clock,
    ILogger<DeadLetterReconciler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.DeadLetterScanIntervalSeconds), clock);
        do
        {
            try
            {
                await ReconcileOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // One failed sweep must not stop the worker.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogScanFailed(logger, ex);
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    /// <summary>Runs one sweep and returns how many jobs were reconciled.</summary>
    internal async Task<int> ReconcileOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<DeadLetterJob> jobs;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            jobs = await scope.ServiceProvider.GetRequiredService<IJobStore>().ClaimDeadLettersAsync(20, cancellationToken);
        }

        var reconciled = 0;
        foreach (var job in jobs)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            try
            {
                var handler = scope.ServiceProvider.GetServices<IJobHandler>().SingleOrDefault(h => h.JobType == job.Type);
                if (handler is null)
                {
                    LogNoHandler(logger, job.Id, job.Type);
                }
                else
                {
                    await handler.OnDeadLetterAsync(job, cancellationToken);
                }

                await scope.ServiceProvider.GetRequiredService<IJobStore>().CompleteDeadLetterAsync(job.Id, cancellationToken);
                reconciled++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await ReleaseAsync(job, "Worker dừng giữa lúc đối soát.");
                throw;
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogReconcileFailed(logger, ex, job.Id);
                await ReleaseAsync(job, ex.Message);
            }
        }

        return reconciled;
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

    private async Task ReleaseAsync(DeadLetterJob job, string error)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IJobStore>().ReleaseDeadLetterAsync(job.Id, error, CancellationToken.None);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogReconcileFailed(logger, ex, job.Id);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Dead-letter sweep failed")]
    private static partial void LogScanFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Reconciling dead-letter job {JobId} failed; it will be retried")]
    private static partial void LogReconcileFailed(ILogger logger, Exception exception, Guid jobId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dead-letter job {JobId} of type {JobType} has no handler")]
    private static partial void LogNoHandler(ILogger logger, Guid jobId, string jobType);
}

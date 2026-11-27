using System.Diagnostics;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.AgentRuntime.Jobs;
using DevAgent.Application.Common;
using DevAgent.Application.Options;
using DevAgent.Infrastructure.Options;
using DevAgent.Worker.Hosting;
using Microsoft.Extensions.Options;

namespace DevAgent.Worker.Jobs;

/// <summary>
/// The worker's processing loop: lease jobs, renew leases for long runs, complete or retry with limits
/// (backoff, Retry-After) and dead-letter when attempts run out or the failure is permanent.
/// Invariant: every leased job returns exactly one slot (<see cref="_slots"/>), whether lease renewal,
/// result recording or the handler fails.
/// </summary>
internal sealed partial class JobProcessingService(
    IServiceScopeFactory scopeFactory,
    IWorkerIdentity identity,
    IOptions<WorkerOptions> workerOptions,
    IOptions<AgentLimitsOptions> limits,
    IOptions<JobQueueOptions> queueOptions,
    TimeProvider clock,
    ILogger<JobProcessingService> logger) : BackgroundService
{
    private readonly SemaphoreSlim _slots = new(workerOptions.Value.MaxConcurrentJobs, workerOptions.Value.MaxConcurrentJobs);
    private readonly List<Task> _running = [];
    private readonly Lock _runningGate = new();

    public int ActiveJobs => workerOptions.Value.MaxConcurrentJobs - _slots.CurrentCount;

    /// <summary>Free processing slots, exposed for tests and heartbeats.</summary>
    internal int FreeSlots => _slots.CurrentCount;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = workerOptions.Value;
        LogStarted(logger, identity.WorkerId, options.MaxConcurrentJobs);
        var pollInterval = TimeSpan.FromSeconds(options.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var started = await PollOnceAsync(stoppingToken);
            if (started == 0)
            {
                try
                {
                    await Task.Delay(pollInterval, clock, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        await DrainAsync(TimeSpan.FromSeconds(options.ShutdownGraceSeconds));
    }

    /// <summary>Leases a batch sized to the free slots, starts the jobs and returns how many started.</summary>
    internal async Task<int> PollOnceAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<LeasedJob> jobs = [];
        try
        {
            var free = _slots.CurrentCount;
            if (free > 0)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                jobs = await scope.ServiceProvider.GetRequiredService<IJobStore>().LeaseAsync(
                    identity.WorkerId,
                    free,
                    TimeSpan.FromSeconds(workerOptions.Value.LeaseDurationSeconds),
                    new JobConcurrencyLimit(JobTypes.ProcessTask, limits.Value.MaxConcurrentTasks),
                    stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return 0;
        }
#pragma warning disable CA1031 // Transient database errors must not kill the lease loop.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogLeaseFailed(logger, ex);
            return 0;
        }

        foreach (var job in jobs)
        {
            // A leased job always has a slot: we never lease more than the free slots, so this wait returns immediately.
            await _slots.WaitAsync(CancellationToken.None);
            Track(RunJobAsync(job, stoppingToken));
        }

        return jobs.Count;
    }

    /// <summary>Waits for running jobs to finish (tests and graceful shutdown).</summary>
    internal async Task DrainAsync(TimeSpan grace)
    {
        Task[] pending;
        lock (_runningGate)
        {
            pending = [.. _running];
        }

        if (pending.Length > 0)
        {
            LogDraining(logger, pending.Length);
            await Task.WhenAny(Task.WhenAll(pending), Task.Delay(grace, clock));
        }
    }

    public override void Dispose()
    {
        _slots.Dispose();
        base.Dispose();
    }

    private void Track(Task task)
    {
        lock (_runningGate)
        {
            _running.Add(task);
        }

        _ = task.ContinueWith(
            t =>
            {
                lock (_runningGate)
                {
                    _running.Remove(t);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task RunJobAsync(LeasedJob job, CancellationToken stoppingToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "completed";
        var lease = new LeaseState();
        using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        Task renewal = Task.CompletedTask;

        try
        {
            using var activity = DevAgentTelemetry.ActivitySource.StartActivity($"job {job.Type}", ActivityKind.Consumer);
            activity?.SetTag(DevAgentTelemetry.JobIdTag, job.Id);
            using var logScope = logger.BeginScope(new Dictionary<string, object> { ["JobId"] = job.Id, ["JobType"] = job.Type, ["Attempt"] = job.Attempt });

            // Lost (or no longer certain) lease: cancel the running work so two workers never write concurrently.
            renewal = RenewLeaseLoopAsync(job, lease, jobCts);

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetServices<IJobHandler>().SingleOrDefault(h => h.JobType == job.Type)
                    ?? throw new PermanentJobException($"Không có handler cho loại job '{job.Type}'.");

                await handler.HandleAsync(job, jobCts.Token);
                await jobCts.CancelAsync();
                await renewal;

                if (lease.Lost)
                {
                    outcome = "lease_lost";
                    LogResultDiscarded(logger, job.Id);
                }
                else
                {
                    await CompleteAsync(job);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down: do not mark the job failed; another worker takes it once the lease expires.
                outcome = "interrupted";
                LogInterrupted(logger, job.Id);
            }
            catch (OperationCanceledException) when (lease.Lost)
            {
                // Lease lost: record nothing; the job belongs to another worker or will be leased again.
                outcome = "lease_lost";
                LogLeaseLostCancelled(logger, job.Id);
            }
#pragma warning disable CA1031 // Every job failure is recorded and rescheduled.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                var permanent = ex is PermanentJobException;
                outcome = permanent || job.Attempt >= job.MaxAttempts ? "dead_letter" : "retry";
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                LogJobFailed(logger, ex, job.Id, job.Attempt, job.MaxAttempts);
                if (!lease.Lost)
                {
                    await FailAsync(job, ex, permanent);
                }
            }
        }
        finally
        {
            try
            {
                await jobCts.CancelAsync();
                await ObserveAsync(renewal);
                RecordMetrics(job, outcome, startedAt);
            }
            finally
            {
                _slots.Release();
            }
        }
    }

    private async Task RenewLeaseLoopAsync(LeasedJob job, LeaseState lease, CancellationTokenSource jobCts)
    {
        var options = workerOptions.Value;
        var leaseDuration = TimeSpan.FromSeconds(options.LeaseDurationSeconds);
        var interval = TimeSpan.FromSeconds(options.LeaseRenewIntervalSeconds);
        var confirmedUntil = job.LeasedUntil;
        using var timer = new PeriodicTimer(interval, clock);

        try
        {
            while (await timer.WaitForNextTickAsync(jobCts.Token))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var renewed = await scope.ServiceProvider.GetRequiredService<IJobStore>()
                        .RenewLeaseAsync(job.Id, identity.WorkerId, leaseDuration, jobCts.Token);
                    if (!renewed)
                    {
                        LogLeaseLost(logger, job.Id);
                        lease.MarkLost();
                        await jobCts.CancelAsync();
                        return;
                    }

                    confirmedUntil = clock.GetUtcNow() + leaseDuration;
                }
                catch (OperationCanceledException) when (jobCts.IsCancellationRequested)
                {
                    return;
                }
#pragma warning disable CA1031 // Transient renewal errors are handled against the last confirmed lease expiry.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    // Retry on the next tick if the lease is still valid by then; otherwise treat it as lost.
                    if (clock.GetUtcNow() + interval >= confirmedUntil)
                    {
                        LogLeaseUncertain(logger, ex, job.Id);
                        lease.MarkLost();
                        await jobCts.CancelAsync();
                        return;
                    }

                    LogRenewRetry(logger, ex, job.Id);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The job finished or the worker is shutting down.
        }
    }

    private async Task CompleteAsync(LeasedJob job)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IJobStore>().CompleteAsync(job.Id, identity.WorkerId, CancellationToken.None);
        }
#pragma warning disable CA1031 // If the status cannot be written the job is leased again after expiry (handlers are idempotent).
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogStoreFailed(logger, ex, job.Id, "complete");
        }
    }

    private async Task FailAsync(LeasedJob job, Exception exception, bool permanent)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IJobStore>()
                .FailAsync(job.Id, identity.WorkerId, $"{exception.GetType().Name}: {exception.Message}", RetryDelay(job, exception), permanent, CancellationToken.None);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogStoreFailed(logger, ex, job.Id, "fail");
        }
    }

    /// <summary>A Retry-After from the remote service (e.g. Discord 429) wins; otherwise capped exponential backoff.</summary>
    internal TimeSpan RetryDelay(LeasedJob job, Exception exception)
    {
        var queue = queueOptions.Value;
        if (exception is RetryableJobException { RetryAfter: { } retryAfter })
        {
            return TimeSpan.FromSeconds(Math.Max(1, retryAfter.TotalSeconds));
        }

        return TimeSpan.FromSeconds(Math.Min(queue.BaseRetryDelaySeconds * Math.Pow(2, Math.Max(0, job.Attempt - 1)), queue.MaxRetryDelaySeconds));
    }

    private async Task ObserveAsync(Task renewal)
    {
        try
        {
            await renewal;
        }
#pragma warning disable CA1031 // The renewal loop must never leak a processing slot.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogStoreFailed(logger, ex, Guid.Empty, "renew");
        }
    }

    private static void RecordMetrics(LeasedJob job, string outcome, long startedAt)
    {
        DevAgentTelemetry.JobsProcessed.Add(1, new KeyValuePair<string, object?>("type", job.Type), new KeyValuePair<string, object?>("outcome", outcome));
        DevAgentTelemetry.JobDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds, new KeyValuePair<string, object?>("type", job.Type));
    }

    private sealed class LeaseState
    {
        private int _lost;

        public bool Lost => Volatile.Read(ref _lost) == 1;

        public void MarkLost() => Interlocked.Exchange(ref _lost, 1);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker {WorkerId} started with up to {MaxConcurrentJobs} concurrent jobs")]
    private static partial void LogStarted(ILogger logger, string workerId, int maxConcurrentJobs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to lease jobs from the queue")]
    private static partial void LogLeaseFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobId} failed on attempt {Attempt}/{MaxAttempts}")]
    private static partial void LogJobFailed(ILogger logger, Exception exception, Guid jobId, int attempt, int maxAttempts);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Lost the lease of job {JobId}; stopping to avoid duplicate writes")]
    private static partial void LogLeaseLost(ILogger logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not renew the lease of job {JobId} before it expired; cancelling the work")]
    private static partial void LogLeaseUncertain(ILogger logger, Exception exception, Guid jobId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Transient error renewing the lease of job {JobId}; retrying on the next tick")]
    private static partial void LogRenewRetry(ILogger logger, Exception exception, Guid jobId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobId} finished after losing its lease; result not recorded")]
    private static partial void LogResultDiscarded(ILogger logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Job {JobId} cancelled because its lease was lost")]
    private static partial void LogLeaseLostCancelled(ILogger logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not record status of job {JobId} ({Operation}); it will be leased again after expiry")]
    private static partial void LogStoreFailed(ILogger logger, Exception exception, Guid jobId, string operation);

    [LoggerMessage(Level = LogLevel.Information, Message = "Job {JobId} interrupted by worker shutdown; it will be leased again after expiry")]
    private static partial void LogInterrupted(ILogger logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Waiting for {Count} jobs to finish before stopping")]
    private static partial void LogDraining(ILogger logger, int count);
}

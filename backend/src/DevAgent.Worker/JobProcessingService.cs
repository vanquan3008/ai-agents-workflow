using System.Diagnostics;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Common;
using DevAgent.Application.Options;
using DevAgent.Application.Orchestration;
using DevAgent.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace DevAgent.Worker;

/// <summary>
/// Vòng nhận việc của Worker (checklist 1.4): nhận job có lease, gia hạn lease khi chạy lâu,
/// hoàn tất hoặc thử lại có giới hạn (backoff, Retry-After), dead-letter khi hết lượt hoặc lỗi vĩnh viễn.
/// Bất biến: mỗi job đã nhận luôn trả lại đúng một lượt xử lý (<see cref="_slots"/>), dù gia hạn lease,
/// ghi kết quả hay handler có lỗi.
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

    /// <summary>Số lượt còn trống (phục vụ kiểm thử và heartbeat).</summary>
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

    /// <summary>Nhận một lô job theo số lượt còn trống và khởi chạy chúng. Trả số job đã khởi chạy.</summary>
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
#pragma warning disable CA1031 // Lỗi DB tạm thời không được làm chết vòng nhận việc.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogLeaseFailed(logger, ex);
            return 0;
        }

        foreach (var job in jobs)
        {
            // Đã nhận lease thì phải có lượt: số job nhận không vượt số lượt trống nên lệnh chờ trả về ngay.
            await _slots.WaitAsync(CancellationToken.None);
            Track(RunJobAsync(job, stoppingToken));
        }

        return jobs.Count;
    }

    /// <summary>Chờ các job đang chạy kết thúc (phục vụ kiểm thử và tắt Worker).</summary>
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

            // Mất lease (hoặc không còn chắc giữ lease) thì hủy công việc đang chạy để không có hai Worker cùng ghi.
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
                // Tắt Worker: không đánh dấu thất bại; lease hết hạn thì Worker khác nhận lại.
                outcome = "interrupted";
                LogInterrupted(logger, job.Id);
            }
            catch (OperationCanceledException) when (lease.Lost)
            {
                // Đã mất lease: không ghi kết quả; job thuộc về Worker khác hoặc sẽ được nhận lại.
                outcome = "lease_lost";
                LogLeaseLostCancelled(logger, job.Id);
            }
#pragma warning disable CA1031 // Mọi lỗi của job đều được ghi nhận và lên lịch thử lại.
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
#pragma warning disable CA1031 // Lỗi gia hạn (DB tạm thời) được xử lý theo hạn lease đã xác nhận.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    // Còn kịp thử lại trước khi lease hết hạn thì thử ở nhịp sau; không thì coi như mất lease.
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
            // Job đã kết thúc hoặc Worker đang tắt.
        }
    }

    private async Task CompleteAsync(LeasedJob job)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IJobStore>().CompleteAsync(job.Id, identity.WorkerId, CancellationToken.None);
        }
#pragma warning disable CA1031 // Không ghi được trạng thái: job sẽ được nhận lại khi lease hết hạn (handler idempotent).
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

    /// <summary>Retry-After do dịch vụ ngoài chỉ định (vd Discord 429) được ưu tiên; còn lại backoff lũy thừa có trần.</summary>
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
#pragma warning disable CA1031 // Vòng gia hạn không được làm rơi lượt xử lý.
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker {WorkerId} bắt đầu, tối đa {MaxConcurrentJobs} job song song")]
    private static partial void LogStarted(ILogger logger, string workerId, int maxConcurrentJobs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Không nhận được job từ hàng đợi")]
    private static partial void LogLeaseFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobId} lỗi ở lần thử {Attempt}/{MaxAttempts}")]
    private static partial void LogJobFailed(ILogger logger, Exception exception, Guid jobId, int attempt, int maxAttempts);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mất lease của job {JobId}; dừng xử lý để tránh ghi trùng")]
    private static partial void LogLeaseLost(ILogger logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Không gia hạn được lease của job {JobId} trước khi hết hạn; hủy công việc")]
    private static partial void LogLeaseUncertain(ILogger logger, Exception exception, Guid jobId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gia hạn lease job {JobId} lỗi tạm thời; thử lại ở nhịp sau")]
    private static partial void LogRenewRetry(ILogger logger, Exception exception, Guid jobId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobId} chạy xong nhưng đã mất lease; bỏ qua ghi kết quả")]
    private static partial void LogResultDiscarded(ILogger logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Job {JobId} bị hủy do mất lease")]
    private static partial void LogLeaseLostCancelled(ILogger logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Không ghi được trạng thái job {JobId} ({Operation}); lease hết hạn thì job được nhận lại")]
    private static partial void LogStoreFailed(ILogger logger, Exception exception, Guid jobId, string operation);

    [LoggerMessage(Level = LogLevel.Information, Message = "Job {JobId} bị gián đoạn do Worker dừng; sẽ được nhận lại khi lease hết hạn")]
    private static partial void LogInterrupted(ILogger logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Đang chờ {Count} job hoàn tất trước khi dừng")]
    private static partial void LogDraining(ILogger logger, int count);
}

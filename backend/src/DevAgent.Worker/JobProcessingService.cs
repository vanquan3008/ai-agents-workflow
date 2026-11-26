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
/// hoàn tất hoặc thử lại có giới hạn với backoff, dead-letter khi hết lượt.
/// Dừng Worker giữa chừng an toàn: job chưa xong sẽ được Worker khác nhận lại khi lease hết hạn.
/// </summary>
internal sealed partial class JobProcessingService(
    IServiceScopeFactory scopeFactory,
    IWorkerIdentity identity,
    IOptions<WorkerOptions> workerOptions,
    IOptions<AgentLimitsOptions> limits,
    IOptions<JobQueueOptions> queueOptions,
    ILogger<JobProcessingService> logger) : BackgroundService
{
    private readonly SemaphoreSlim _slots = new(workerOptions.Value.MaxConcurrentJobs, workerOptions.Value.MaxConcurrentJobs);
    private readonly List<Task> _running = [];
    private readonly Lock _runningGate = new();

    public int ActiveJobs => workerOptions.Value.MaxConcurrentJobs - _slots.CurrentCount;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = workerOptions.Value;
        LogStarted(logger, identity.WorkerId, options.MaxConcurrentJobs);
        var pollInterval = TimeSpan.FromSeconds(options.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<LeasedJob> jobs = [];
            try
            {
                var free = _slots.CurrentCount;
                if (free > 0)
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var store = scope.ServiceProvider.GetRequiredService<IJobStore>();
                    jobs = await store.LeaseAsync(
                        identity.WorkerId,
                        free,
                        TimeSpan.FromSeconds(options.LeaseDurationSeconds),
                        limits.Value.MaxConcurrentTasks,
                        stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // Lỗi DB tạm thời không được làm chết vòng nhận việc.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogLeaseFailed(logger, ex);
            }

            foreach (var job in jobs)
            {
                await _slots.WaitAsync(stoppingToken);
                var task = RunJobAsync(job, stoppingToken);
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

            if (jobs.Count == 0)
            {
                try
                {
                    await Task.Delay(pollInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        await DrainAsync(TimeSpan.FromSeconds(options.ShutdownGraceSeconds));
    }

    public override void Dispose()
    {
        _slots.Dispose();
        base.Dispose();
    }

    private async Task RunJobAsync(LeasedJob job, CancellationToken stoppingToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        using var activity = DevAgentTelemetry.ActivitySource.StartActivity($"job {job.Type}", ActivityKind.Consumer);
        activity?.SetTag(DevAgentTelemetry.JobIdTag, job.Id);
        using var logScope = logger.BeginScope(new Dictionary<string, object> { ["JobId"] = job.Id, ["JobType"] = job.Type, ["Attempt"] = job.Attempt });

        // Lease mất (Worker khác đã nhận lại) thì hủy công việc đang chạy để không có hai Worker cùng ghi.
        using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var renewal = RenewLeaseLoopAsync(job, jobCts);
        var outcome = "completed";

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetServices<IJobHandler>().SingleOrDefault(h => h.JobType == job.Type)
                ?? throw new InvalidOperationException($"Không có handler cho loại job '{job.Type}'.");

            await handler.HandleAsync(job, jobCts.Token);
            await jobCts.CancelAsync();
            await CompleteAsync(job);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Tắt Worker: không đánh dấu thất bại; lease hết hạn thì Worker khác nhận lại.
            outcome = "interrupted";
            LogInterrupted(logger, job.Id);
        }
#pragma warning disable CA1031 // Mọi lỗi của job đều được ghi nhận và lên lịch thử lại.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            outcome = job.Attempt >= job.MaxAttempts ? "dead_letter" : "retry";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogJobFailed(logger, ex, job.Id, job.Attempt, job.MaxAttempts);
            await FailAsync(job, ex);
        }
        finally
        {
            await jobCts.CancelAsync();
            await renewal;
            DevAgentTelemetry.JobsProcessed.Add(1, new KeyValuePair<string, object?>("type", job.Type), new KeyValuePair<string, object?>("outcome", outcome));
            DevAgentTelemetry.JobDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds, new KeyValuePair<string, object?>("type", job.Type));
            _slots.Release();
        }
    }

    private async Task RenewLeaseLoopAsync(LeasedJob job, CancellationTokenSource jobCts)
    {
        var options = workerOptions.Value;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.LeaseRenewIntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(jobCts.Token))
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<IJobStore>();
                if (!await store.RenewLeaseAsync(job.Id, identity.WorkerId, TimeSpan.FromSeconds(options.LeaseDurationSeconds), jobCts.Token))
                {
                    LogLeaseLost(logger, job.Id);
                    await jobCts.CancelAsync();
                    return;
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
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IJobStore>().CompleteAsync(job.Id, identity.WorkerId, CancellationToken.None);
    }

    private async Task FailAsync(LeasedJob job, Exception exception)
    {
        // Backoff lũy thừa: base * 2^(attempt-1), có trần.
        var queue = queueOptions.Value;
        var delay = TimeSpan.FromSeconds(Math.Min(queue.BaseRetryDelaySeconds * Math.Pow(2, job.Attempt - 1), queue.MaxRetryDelaySeconds));
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IJobStore>()
            .FailAsync(job.Id, identity.WorkerId, $"{exception.GetType().Name}: {exception.Message}", delay, CancellationToken.None);
    }

    private async Task DrainAsync(TimeSpan grace)
    {
        Task[] pending;
        lock (_runningGate)
        {
            pending = [.. _running];
        }

        if (pending.Length > 0)
        {
            LogDraining(logger, pending.Length);
            await Task.WhenAny(Task.WhenAll(pending), Task.Delay(grace));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker {WorkerId} bắt đầu, tối đa {MaxConcurrentJobs} job song song")]
    private static partial void LogStarted(ILogger logger, string workerId, int maxConcurrentJobs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Không nhận được job từ hàng đợi")]
    private static partial void LogLeaseFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobId} lỗi ở lần thử {Attempt}/{MaxAttempts}")]
    private static partial void LogJobFailed(ILogger logger, Exception exception, Guid jobId, int attempt, int maxAttempts);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mất lease của job {JobId}; dừng xử lý để tránh ghi trùng")]
    private static partial void LogLeaseLost(ILogger logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Job {JobId} bị gián đoạn do Worker dừng; sẽ được nhận lại khi lease hết hạn")]
    private static partial void LogInterrupted(ILogger logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Đang chờ {Count} job hoàn tất trước khi dừng")]
    private static partial void LogDraining(ILogger logger, int count);
}

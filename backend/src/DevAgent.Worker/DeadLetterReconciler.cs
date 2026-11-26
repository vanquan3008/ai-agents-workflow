using DevAgent.Application.Abstractions.Messaging;
using Microsoft.Extensions.Options;

namespace DevAgent.Worker;

/// <summary>
/// Đối soát job dead-letter: với mỗi job hết lượt (kể cả khi Worker chết ở lần thử cuối, lúc đó chỉ bảng job được cập nhật),
/// gọi handler của loại job để cập nhật trạng thái nghiệp vụ (task → FAILED, execution đóng kèm lý do).
/// Nhiều Worker chạy song song an toàn: job được nhận bằng SKIP LOCKED; lỗi đối soát thì trả lại để lần sau thử tiếp.
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
#pragma warning disable CA1031 // Lỗi một lần quét không được dừng Worker.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogScanFailed(logger, ex);
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    /// <summary>Quét một lượt; trả số job đã đối soát thành công.</summary>
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Quét dead-letter thất bại")]
    private static partial void LogScanFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Đối soát job dead-letter {JobId} thất bại; sẽ thử lại")]
    private static partial void LogReconcileFailed(ILogger logger, Exception exception, Guid jobId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job dead-letter {JobId} loại {JobType} không có handler")]
    private static partial void LogNoHandler(ILogger logger, Guid jobId, string jobType);
}

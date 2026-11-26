namespace DevAgent.Application.Abstractions.Messaging;

public static class JobTypes
{
    /// <summary>Tiến một task qua bước tiếp theo của Orchestrator.</summary>
    public const string ProcessTask = "task.process";
}

/// <summary>
/// Yêu cầu đưa job vào hàng đợi bền vững.
/// <paramref name="DeduplicationKey"/> là duy nhất: enqueue lặp cùng khóa không sinh job thứ hai.
/// </summary>
public sealed record JobRequest(string Type, string PayloadJson, string DeduplicationKey, DateTimeOffset? AvailableAt = null);

/// <summary>
/// Ghi job vào cùng giao dịch với thay đổi nghiệp vụ (transactional outbox):
/// job chỉ tồn tại nếu task đã được lưu, và ngược lại.
/// </summary>
public interface IJobQueue
{
    Task EnqueueAsync(JobRequest request, CancellationToken cancellationToken);
}

/// <summary>Job đã được một Worker nhận với lease có thời hạn.</summary>
public sealed record LeasedJob(Guid Id, string Type, string PayloadJson, int Attempt, int MaxAttempts, DateTimeOffset LeasedUntil);

/// <summary>Phía tiêu thụ hàng đợi, dùng bởi Worker.</summary>
public interface IJobStore
{
    /// <summary>
    /// Nhận tối đa <paramref name="maxJobs"/> job sẵn sàng; dùng FOR UPDATE SKIP LOCKED để hai Worker không nhận trùng,
    /// và không vượt <paramref name="globalConcurrencyLimit"/> job đang chạy trên toàn hệ thống.
    /// </summary>
    Task<IReadOnlyList<LeasedJob>> LeaseAsync(
        string workerId,
        int maxJobs,
        TimeSpan leaseDuration,
        int globalConcurrencyLimit,
        CancellationToken cancellationToken);

    /// <summary>Gia hạn lease khi job chạy lâu. Trả false nếu đã mất lease (Worker khác đã nhận lại).</summary>
    Task<bool> RenewLeaseAsync(Guid jobId, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken);

    Task CompleteAsync(Guid jobId, string workerId, CancellationToken cancellationToken);

    /// <summary>Đánh dấu thất bại; thử lại với backoff, hoặc chuyển sang dead-letter khi hết số lần thử.</summary>
    Task FailAsync(Guid jobId, string workerId, string error, TimeSpan retryDelay, CancellationToken cancellationToken);
}

/// <summary>Xử lý một loại job. Handler phải idempotent vì job có thể chạy lại sau khi lease hết hạn.</summary>
public interface IJobHandler
{
    string JobType { get; }

    Task HandleAsync(LeasedJob job, CancellationToken cancellationToken);
}

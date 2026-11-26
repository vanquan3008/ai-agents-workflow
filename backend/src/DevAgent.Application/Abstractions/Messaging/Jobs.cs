namespace DevAgent.Application.Abstractions.Messaging;

public static class JobTypes
{
    /// <summary>Tiến một task qua bước tiếp theo của Orchestrator.</summary>
    public const string ProcessTask = "task.process";

    /// <summary>Gửi một thông báo ra kênh chat (outbox, có thử lại).</summary>
    public const string SendNotification = "notification.send";
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

/// <summary>Job đã hết lượt thử (dead-letter), chờ đối soát trạng thái nghiệp vụ.</summary>
public sealed record DeadLetterJob(Guid Id, string Type, string PayloadJson, int Attempt, int MaxAttempts, string? LastError);

/// <summary>Trần số job đang chạy đồng thời của một loại job trên toàn hệ thống.</summary>
public sealed record JobConcurrencyLimit(string JobType, int MaxActive);

/// <summary>Phía tiêu thụ hàng đợi, dùng bởi Worker.</summary>
public interface IJobStore
{
    /// <summary>
    /// Nhận tối đa <paramref name="maxJobs"/> job sẵn sàng; dùng FOR UPDATE SKIP LOCKED để hai Worker không nhận trùng.
    /// Loại job trong <paramref name="limit"/> không vượt trần đồng thời toàn hệ thống; loại khác (vd thông báo) không bị chặn bởi trần đó.
    /// </summary>
    Task<IReadOnlyList<LeasedJob>> LeaseAsync(
        string workerId,
        int maxJobs,
        TimeSpan leaseDuration,
        JobConcurrencyLimit? limit,
        CancellationToken cancellationToken);

    /// <summary>Gia hạn lease khi job chạy lâu. Trả false nếu đã mất lease (Worker khác đã nhận lại).</summary>
    Task<bool> RenewLeaseAsync(Guid jobId, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken);

    Task CompleteAsync(Guid jobId, string workerId, CancellationToken cancellationToken);

    /// <summary>
    /// Đánh dấu thất bại: thử lại sau <paramref name="retryDelay"/>, hoặc chuyển dead-letter khi hết lượt
    /// hay khi <paramref name="permanent"/> (lỗi không thể tự hết, như cấu hình sai).
    /// </summary>
    Task FailAsync(Guid jobId, string workerId, string error, TimeSpan retryDelay, bool permanent, CancellationToken cancellationToken);

    /// <summary>Nhận các job dead-letter chưa đối soát (đánh dấu đã nhận để Worker khác không xử lý trùng).</summary>
    Task<IReadOnlyList<DeadLetterJob>> ClaimDeadLettersAsync(int maxJobs, CancellationToken cancellationToken);

    /// <summary>Chỉ xác nhận đã đối soát sau khi handler cập nhật nghiệp vụ thành công.</summary>
    Task CompleteDeadLetterAsync(Guid jobId, CancellationToken cancellationToken);

    /// <summary>Trả lại job dead-letter khi đối soát thất bại để lần quét sau thử lại.</summary>
    Task ReleaseDeadLetterAsync(Guid jobId, string error, CancellationToken cancellationToken);
}

/// <summary>Xử lý một loại job. Handler phải idempotent vì job có thể chạy lại sau khi lease hết hạn.</summary>
public interface IJobHandler
{
    string JobType { get; }

    Task HandleAsync(LeasedJob job, CancellationToken cancellationToken);

    /// <summary>
    /// Job đã hết lượt thử (kể cả khi Worker chết ở lần thử cuối): cập nhật trạng thái nghiệp vụ để không có gì
    /// "đang chạy" mà không còn job tiếp tục. Phải idempotent.
    /// </summary>
    Task OnDeadLetterAsync(DeadLetterJob job, CancellationToken cancellationToken);
}

/// <summary>Lỗi tạm thời: job được thử lại, sau <see cref="RetryAfter"/> nếu dịch vụ ngoài chỉ định (vd HTTP 429).</summary>
public sealed class RetryableJobException : Exception
{
    public RetryableJobException()
    {
    }

    public RetryableJobException(string message)
        : base(message)
    {
    }

    public RetryableJobException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public TimeSpan? RetryAfter { get; init; }
}

/// <summary>Lỗi không tự hết khi thử lại (cấu hình sai, thiếu quyền): chuyển dead-letter ngay để người vận hành xử lý.</summary>
public sealed class PermanentJobException : Exception
{
    public PermanentJobException()
    {
    }

    public PermanentJobException(string message)
        : base(message)
    {
    }

    public PermanentJobException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

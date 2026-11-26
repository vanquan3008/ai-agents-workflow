namespace DevAgent.Infrastructure.Persistence.Jobs;

public enum JobStatus
{
    Queued = 1,
    Leased = 2,
    Completed = 3,

    /// <summary>Hết số lần thử: chờ người vận hành xử lý (checklist 1.4).</summary>
    DeadLetter = 4,
}

/// <summary>
/// Bản ghi hàng đợi bền vững. Không phải khái niệm domain nên nằm ở Infrastructure.
/// Lưu cùng giao dịch với thay đổi nghiệp vụ (transactional outbox).
/// </summary>
public sealed class JobRecord
{
    public Guid Id { get; set; }

    public required string Type { get; set; }

    public required string Payload { get; set; }

    public required string DeduplicationKey { get; set; }

    public JobStatus Status { get; set; } = JobStatus.Queued;

    public int Attempt { get; set; }

    public int MaxAttempts { get; set; }

    public DateTimeOffset AvailableAt { get; set; }

    public string? LeasedBy { get; set; }

    public DateTimeOffset? LeasedUntil { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}

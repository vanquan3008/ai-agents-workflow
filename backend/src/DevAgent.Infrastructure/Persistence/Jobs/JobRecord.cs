namespace DevAgent.Infrastructure.Persistence.Jobs;

public enum JobStatus
{
    Queued = 1,
    Leased = 2,
    Completed = 3,

    /// <summary>Out of attempts; waiting for reconciliation and operator attention.</summary>
    DeadLetter = 4,
}

/// <summary>
/// Durable queue row. Not a domain concept, so it lives in infrastructure.
/// Saved in the same transaction as the business change (transactional outbox).
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

    public DateTimeOffset? DeadLetteredAt { get; set; }

    /// <summary>When business state was reconciled for a dead-lettered job; null until then.</summary>
    public DateTimeOffset? ReconciledAt { get; set; }
}

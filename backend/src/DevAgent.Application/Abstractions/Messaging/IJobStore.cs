namespace DevAgent.Application.Abstractions.Messaging;

/// <summary>A job leased by a worker until <see cref="LeasedUntil"/>.</summary>
public sealed record LeasedJob(Guid Id, string Type, string PayloadJson, int Attempt, int MaxAttempts, DateTimeOffset LeasedUntil);

/// <summary>A job that ran out of attempts and is waiting for business-state reconciliation.</summary>
public sealed record DeadLetterJob(Guid Id, string Type, string PayloadJson, int Attempt, int MaxAttempts, string? LastError);

/// <summary>System-wide cap on concurrently running jobs of one type.</summary>
public sealed record JobConcurrencyLimit(string JobType, int MaxActive);

/// <summary>Consumer side of the durable queue, used by the worker.</summary>
public interface IJobStore
{
    /// <summary>
    /// Leases up to <paramref name="maxJobs"/> ready jobs with FOR UPDATE SKIP LOCKED so two workers never take the same job.
    /// Jobs of the type in <paramref name="limit"/> respect its system-wide cap; other types are not blocked by it.
    /// </summary>
    Task<IReadOnlyList<LeasedJob>> LeaseAsync(
        string workerId,
        int maxJobs,
        TimeSpan leaseDuration,
        JobConcurrencyLimit? limit,
        CancellationToken cancellationToken);

    /// <summary>Extends a lease. Returns false when the lease was lost to another worker.</summary>
    Task<bool> RenewLeaseAsync(Guid jobId, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken);

    Task CompleteAsync(Guid jobId, string workerId, CancellationToken cancellationToken);

    /// <summary>
    /// Records a failure: retry after <paramref name="retryDelay"/>, or dead-letter when attempts are exhausted
    /// or the failure is <paramref name="permanent"/> (it cannot heal by itself, e.g. misconfiguration).
    /// </summary>
    Task FailAsync(Guid jobId, string workerId, string error, TimeSpan retryDelay, bool permanent, CancellationToken cancellationToken);

    /// <summary>Claims unreconciled dead-letter jobs so no other worker processes them concurrently.</summary>
    Task<IReadOnlyList<DeadLetterJob>> ClaimDeadLettersAsync(int maxJobs, CancellationToken cancellationToken);

    /// <summary>Marks a dead letter reconciled; call only after the handler updated business state successfully.</summary>
    Task CompleteDeadLetterAsync(Guid jobId, CancellationToken cancellationToken);

    /// <summary>Releases a dead letter after a failed reconciliation so the next sweep retries it.</summary>
    Task ReleaseDeadLetterAsync(Guid jobId, string error, CancellationToken cancellationToken);
}

using System.ComponentModel.DataAnnotations.Schema;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DevAgent.Infrastructure.Persistence.Jobs;

/// <summary>
/// Hàng đợi bền vững trên PostgreSQL cho pilot (checklist 0.8: chưa cần Redis).
/// - Enqueue: thêm bản ghi vào DbContext hiện tại, commit cùng giao dịch nghiệp vụ (outbox).
/// - Lease: <c>FOR UPDATE SKIP LOCKED</c> để nhiều Worker nhận việc song song mà không trùng; lease có thời hạn.
/// - Dead-letter: hết lượt thử (kể cả Worker chết ở lần cuối) → ghi <c>dead_lettered_at</c>; bước đối soát
///   nhận các job này để cập nhật trạng thái nghiệp vụ, đánh dấu <c>reconciled_at</c>.
/// </summary>
internal sealed class PostgresJobQueue(DevAgentDbContext context, IOptions<JobQueueOptions> options, TimeProvider clock)
    : IJobQueue, IJobStore
{
    private const long LeaseLockKey = 0x0DE7A6E7;

    public async Task EnqueueAsync(JobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Chống trùng ở cả hai mức: bản ghi đang chờ lưu trong DbContext và bản ghi đã có trong DB.
        var pendingDuplicate = context.ChangeTracker.Entries<JobRecord>()
            .Any(e => e.State == EntityState.Added && e.Entity.DeduplicationKey == request.DeduplicationKey);
        if (pendingDuplicate || await context.Jobs.AnyAsync(j => j.DeduplicationKey == request.DeduplicationKey, cancellationToken))
        {
            return;
        }

        var now = clock.GetUtcNow();
        context.Jobs.Add(new JobRecord
        {
            Id = Guid.CreateVersion7(),
            Type = request.Type,
            Payload = request.PayloadJson,
            DeduplicationKey = request.DeduplicationKey,
            Status = JobStatus.Queued,
            Attempt = 0,
            MaxAttempts = options.Value.MaxAttempts,
            AvailableAt = request.AvailableAt ?? now,
            CreatedAt = now,
            UpdatedAt = now,
        });
    }

    public async Task<IReadOnlyList<LeasedJob>> LeaseAsync(
        string workerId,
        int maxJobs,
        TimeSpan leaseDuration,
        JobConcurrencyLimit? limit,
        CancellationToken cancellationToken)
    {
        var leaseSeconds = leaseDuration.TotalSeconds;
        var strategy = context.Database.CreateExecutionStrategy();

        var rows = await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            // Khóa tư vấn trong giao dịch: tuần tự hóa việc nhận job giữa các Worker để không vượt trần đồng thời.
            await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({LeaseLockKey})", ct);

            // Lease hết hạn ở lần thử cuối (Worker chết giữa chừng): không nhận lại, chuyển dead-letter để đối soát.
            await context.Database.ExecuteSqlAsync(
                $"""
                 UPDATE devagent.jobs
                 SET status = 'DeadLetter', dead_lettered_at = now(),
                     last_error = COALESCE(last_error || ' | ', '') || 'Lease hết hạn ở lần thử cuối (Worker dừng giữa chừng).',
                     leased_by = NULL, leased_until = NULL, updated_at = now()
                 WHERE status = 'Leased' AND leased_until < now() AND attempt >= max_attempts
                 """,
                ct);

            var leased = new List<LeasedJobRow>();
            if (limit is not null)
            {
                var active = await context.Jobs.CountAsync(
                    j => j.Type == limit.JobType && j.Status == JobStatus.Leased && j.LeasedUntil > DateTimeOffset.UtcNow, ct);
                var take = Math.Min(maxJobs, limit.MaxActive - active);
                if (take > 0)
                {
                    leased.AddRange(await LeaseBatchAsync(workerId, leaseSeconds, take, onlyType: limit.JobType, excludeType: null, ct));
                }
            }

            var remaining = maxJobs - leased.Count;
            if (remaining > 0)
            {
                leased.AddRange(await LeaseBatchAsync(workerId, leaseSeconds, remaining, onlyType: null, excludeType: limit?.JobType, ct));
            }

            await transaction.CommitAsync(ct);
            return leased;
        }, cancellationToken);

        return rows.Select(r => new LeasedJob(r.Id, r.Type, r.Payload, r.Attempt, r.MaxAttempts, r.LeasedUntil)).ToArray();
    }

    public async Task<bool> RenewLeaseAsync(Guid jobId, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        var leaseSeconds = leaseDuration.TotalSeconds;
        var affected = await context.Database.ExecuteSqlAsync(
            $"""
             UPDATE devagent.jobs
             SET leased_until = now() + make_interval(secs => {leaseSeconds}), updated_at = now()
             WHERE id = {jobId} AND leased_by = {workerId} AND status = 'Leased' AND leased_until > now()
             """,
            cancellationToken);
        return affected == 1;
    }

    public Task CompleteAsync(Guid jobId, string workerId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlAsync(
            $"""
             UPDATE devagent.jobs
             SET status = 'Completed', completed_at = now(), leased_by = NULL, leased_until = NULL, updated_at = now()
             WHERE id = {jobId} AND leased_by = {workerId} AND status = 'Leased'
             """,
            cancellationToken);

    public Task FailAsync(Guid jobId, string workerId, string error, TimeSpan retryDelay, bool permanent, CancellationToken cancellationToken)
    {
        var message = error.Length > 8000 ? error[..8000] : error;
        var delaySeconds = Math.Max(0, retryDelay.TotalSeconds);
        return context.Database.ExecuteSqlAsync(
            $"""
             UPDATE devagent.jobs
             SET status = CASE WHEN {permanent} OR attempt >= max_attempts THEN 'DeadLetter' ELSE 'Queued' END,
                 dead_lettered_at = CASE WHEN {permanent} OR attempt >= max_attempts THEN now() ELSE NULL END,
                 available_at = now() + make_interval(secs => {delaySeconds}),
                 last_error = {message}, leased_by = NULL, leased_until = NULL, updated_at = now()
             WHERE id = {jobId} AND leased_by = {workerId} AND status = 'Leased'
             """,
            cancellationToken);
    }

    public async Task<IReadOnlyList<DeadLetterJob>> ClaimDeadLettersAsync(int maxJobs, CancellationToken cancellationToken)
    {
        var rows = await context.Database.SqlQuery<DeadLetterRow>(
                $"""
                 UPDATE devagent.jobs AS j
                 SET leased_until = now() + interval '5 minutes', updated_at = now()
                 WHERE j.id IN (
                     SELECT id FROM devagent.jobs
                     WHERE status = 'DeadLetter' AND reconciled_at IS NULL
                       AND (leased_until IS NULL OR leased_until < now())
                     ORDER BY dead_lettered_at NULLS FIRST
                     LIMIT {maxJobs}
                     FOR UPDATE SKIP LOCKED)
                 RETURNING j.id, j.type, j.payload::text AS payload, j.attempt, j.max_attempts, j.last_error
                 """)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new DeadLetterJob(r.Id, r.Type, r.Payload, r.Attempt, r.MaxAttempts, r.LastError)).ToArray();
    }

    public Task CompleteDeadLetterAsync(Guid jobId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlAsync(
            $"""
             UPDATE devagent.jobs
             SET reconciled_at = now(), leased_until = NULL, updated_at = now()
             WHERE id = {jobId} AND status = 'DeadLetter'
             """,
            cancellationToken);

    public Task ReleaseDeadLetterAsync(Guid jobId, string error, CancellationToken cancellationToken)
    {
        var message = $"Đối soát thất bại: {(error.Length > 2000 ? error[..2000] : error)}";
        return context.Database.ExecuteSqlAsync(
            $"""
             UPDATE devagent.jobs
             SET reconciled_at = NULL, leased_until = NULL,
                 last_error = left(COALESCE(last_error || ' | ', '') || {message}, 8000), updated_at = now()
             WHERE id = {jobId} AND status = 'DeadLetter'
             """,
            cancellationToken);
    }

    private Task<List<LeasedJobRow>> LeaseBatchAsync(
        string workerId,
        double leaseSeconds,
        int take,
        string? onlyType,
        string? excludeType,
        CancellationToken cancellationToken) =>
        context.Database.SqlQuery<LeasedJobRow>(
                $"""
                 UPDATE devagent.jobs AS j
                 SET status = 'Leased',
                     leased_by = {workerId},
                     leased_until = now() + make_interval(secs => {leaseSeconds}),
                     attempt = j.attempt + 1,
                     updated_at = now()
                 WHERE j.id IN (
                     SELECT id FROM devagent.jobs
                     WHERE ((status = 'Queued' AND available_at <= now())
                            OR (status = 'Leased' AND leased_until < now() AND attempt < max_attempts))
                       AND ({onlyType}::text IS NULL OR type = {onlyType})
                       AND ({excludeType}::text IS NULL OR type <> {excludeType})
                     ORDER BY available_at
                     LIMIT {take}
                     FOR UPDATE SKIP LOCKED)
                 RETURNING j.id, j.type, j.payload::text AS payload, j.attempt, j.max_attempts, j.leased_until
                 """)
            .ToListAsync(cancellationToken);

    // Tên cột tường minh để không phụ thuộc naming convention khi ánh xạ kết quả SQL thô.
    private sealed class LeasedJobRow
    {
        [Column("id")]
        public Guid Id { get; init; }

        [Column("type")]
        public string Type { get; init; } = string.Empty;

        [Column("payload")]
        public string Payload { get; init; } = string.Empty;

        [Column("attempt")]
        public int Attempt { get; init; }

        [Column("max_attempts")]
        public int MaxAttempts { get; init; }

        [Column("leased_until")]
        public DateTimeOffset LeasedUntil { get; init; }
    }

    private sealed class DeadLetterRow
    {
        [Column("id")]
        public Guid Id { get; init; }

        [Column("type")]
        public string Type { get; init; } = string.Empty;

        [Column("payload")]
        public string Payload { get; init; } = string.Empty;

        [Column("attempt")]
        public int Attempt { get; init; }

        [Column("max_attempts")]
        public int MaxAttempts { get; init; }

        [Column("last_error")]
        public string? LastError { get; init; }
    }
}

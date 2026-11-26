using System.ComponentModel.DataAnnotations.Schema;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DevAgent.Infrastructure.Persistence.Jobs;

/// <summary>
/// Hàng đợi bền vững trên PostgreSQL cho pilot (checklist 0.8: chưa cần Redis).
/// - Enqueue: thêm bản ghi vào DbContext hiện tại, commit cùng giao dịch nghiệp vụ.
/// - Lease: <c>FOR UPDATE SKIP LOCKED</c> để nhiều Worker nhận việc song song mà không trùng.
/// - Lease có thời hạn: Worker chết thì job tự quay lại hàng đợi khi lease hết hạn.
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
        int globalConcurrencyLimit,
        CancellationToken cancellationToken)
    {
        var leaseSeconds = leaseDuration.TotalSeconds;
        var strategy = context.Database.CreateExecutionStrategy();

        var rows = await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            // Khóa tư vấn trong giao dịch: tuần tự hóa việc nhận job giữa các Worker để không vượt trần đồng thời toàn hệ thống.
            await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({LeaseLockKey})", ct);

            // Lease hết hạn ở lần thử cuối: không nhận lại, chuyển dead-letter để người vận hành xử lý.
            await context.Database.ExecuteSqlAsync(
                $"""
                 UPDATE devagent.jobs
                 SET status = 'DeadLetter', last_error = 'Lease hết hạn ở lần thử cuối (Worker dừng giữa chừng).',
                     leased_by = NULL, leased_until = NULL, updated_at = now()
                 WHERE status = 'Leased' AND leased_until < now() AND attempt >= max_attempts
                 """,
                ct);

            var active = await context.Jobs.CountAsync(j => j.Status == JobStatus.Leased && j.LeasedUntil > DateTimeOffset.UtcNow, ct);
            var take = Math.Min(maxJobs, globalConcurrencyLimit - active);
            if (take <= 0)
            {
                await transaction.CommitAsync(ct);
                return [];
            }

            var leased = await context.Database.SqlQuery<LeasedJobRow>(
                    $"""
                     UPDATE devagent.jobs AS j
                     SET status = 'Leased',
                         leased_by = {workerId},
                         leased_until = now() + make_interval(secs => {leaseSeconds}),
                         attempt = j.attempt + 1,
                         updated_at = now()
                     WHERE j.id IN (
                         SELECT id FROM devagent.jobs
                         WHERE (status = 'Queued' AND available_at <= now())
                            OR (status = 'Leased' AND leased_until < now() AND attempt < max_attempts)
                         ORDER BY available_at
                         LIMIT {take}
                         FOR UPDATE SKIP LOCKED)
                     RETURNING j.id, j.type, j.payload::text AS payload, j.attempt, j.max_attempts, j.leased_until
                     """)
                .ToListAsync(ct);

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
             WHERE id = {jobId} AND leased_by = {workerId} AND status = 'Leased'
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

    public Task FailAsync(Guid jobId, string workerId, string error, TimeSpan retryDelay, CancellationToken cancellationToken)
    {
        var message = error.Length > 8000 ? error[..8000] : error;
        var delaySeconds = Math.Min(retryDelay.TotalSeconds, options.Value.MaxRetryDelaySeconds);
        return context.Database.ExecuteSqlAsync(
            $"""
             UPDATE devagent.jobs
             SET status = CASE WHEN attempt >= max_attempts THEN 'DeadLetter' ELSE 'Queued' END,
                 available_at = now() + make_interval(secs => {delaySeconds}),
                 last_error = {message}, leased_by = NULL, leased_until = NULL, updated_at = now()
             WHERE id = {jobId} AND leased_by = {workerId} AND status = 'Leased'
             """,
            cancellationToken);
    }

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
}

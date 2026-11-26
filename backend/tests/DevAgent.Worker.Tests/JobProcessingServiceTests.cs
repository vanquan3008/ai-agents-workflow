using System.Collections.Concurrent;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.AgentRuntime.Jobs;
using DevAgent.Application.Options;
using DevAgent.Infrastructure.Options;
using DevAgent.Worker.Hosting;
using DevAgent.Worker.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace DevAgent.Worker.Tests;

/// <summary>
/// Invariants of the processing loop: slots are always returned; a lost (or uncertain) lease cancels work and records nothing;
/// transient failures honour Retry-After; permanent failures dead-letter immediately.
/// </summary>
public sealed class JobProcessingServiceTests : IDisposable
{
    private const string JobType = "test.job";
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 26, 8, 0, 0, TimeSpan.Zero));
    private readonly FakeJobStore _store = new();
    private readonly TestHandler _handler = new();
    private readonly ServiceProvider _services;
    private readonly JobProcessingService _service;

    public JobProcessingServiceTests()
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<IJobStore>(_store);
        collection.AddSingleton<IJobHandler>(_handler);
        _services = collection.BuildServiceProvider();

        _service = new JobProcessingService(
            _services.GetRequiredService<IServiceScopeFactory>(),
            new FixedWorker(),
            MsOptions.Create(new WorkerOptions { MaxConcurrentJobs = 2, LeaseDurationSeconds = 15, LeaseRenewIntervalSeconds = 5 }),
            MsOptions.Create(new AgentLimitsOptions()),
            MsOptions.Create(new JobQueueOptions { BaseRetryDelaySeconds = 10, MaxRetryDelaySeconds = 600 }),
            _clock,
            NullLogger<JobProcessingService>.Instance);
    }

    public void Dispose()
    {
        _service.Dispose();
        _services.Dispose();
    }

    private LeasedJob Job(int attempt = 1) => new(Guid.NewGuid(), JobType, "{}", attempt, 5, _clock.GetUtcNow().AddSeconds(15));

    /// <summary>Advances the fake clock one second at a time until the condition holds (timers run on the thread pool).</summary>
    private async Task AdvanceUntilAsync(Func<bool> condition, int maxSeconds = 60)
    {
        for (var i = 0; i < maxSeconds && !condition(); i++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(15, TestContext.Current.CancellationToken);
        }

        condition().ShouldBeTrue();
    }

    [Fact]
    public async Task Renewal_errors_until_lease_expiry_cancel_job_and_still_release_slot()
    {
        _store.Lease.Enqueue(Job());
        _store.RenewBehavior = () => throw new InvalidOperationException("PostgreSQL mất kết nối");
        _handler.Behavior = ct => Task.Delay(Timeout.Infinite, ct);

        (await _service.PollOnceAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        _service.FreeSlots.ShouldBe(1);

        await AdvanceUntilAsync(() => _service.FreeSlots == 2);
        await _service.DrainAsync(TimeSpan.FromSeconds(5));

        _store.RenewAttempts.ShouldBeGreaterThanOrEqualTo(2); // retries while the lease is valid, then gives up
        _store.Completed.ShouldBeEmpty();
        _store.Failed.ShouldBeEmpty();                        // no lease, no recorded result
        _handler.WasCancelled.ShouldBeTrue();
    }

    [Fact]
    public async Task Transient_renewal_error_followed_by_success_keeps_job_running()
    {
        _store.Lease.Enqueue(Job());
        var calls = 0;
        _store.RenewBehavior = () => Interlocked.Increment(ref calls) == 1 ? throw new InvalidOperationException("tạm thời") : true;
        var release = new TaskCompletionSource();
        _handler.Behavior = ct => release.Task.WaitAsync(ct);

        await _service.PollOnceAsync(TestContext.Current.CancellationToken);
        await AdvanceUntilAsync(() => _store.RenewAttempts >= 3);
        release.SetResult();
        await _service.DrainAsync(TimeSpan.FromSeconds(5));

        _store.Completed.ShouldHaveSingleItem();
        _handler.WasCancelled.ShouldBeFalse();
        _service.FreeSlots.ShouldBe(2);
    }

    [Fact]
    public async Task Lost_lease_is_not_completed_even_if_handler_finishes()
    {
        _store.Lease.Enqueue(Job());
        _store.RenewBehavior = () => false;
        _handler.Behavior = ct => Task.Delay(Timeout.Infinite, ct);

        await _service.PollOnceAsync(TestContext.Current.CancellationToken);
        await AdvanceUntilAsync(() => _service.FreeSlots == 2);

        _store.Completed.ShouldBeEmpty();
        _store.Failed.ShouldBeEmpty();
    }

    [Fact]
    public async Task Store_failure_on_complete_still_releases_slot()
    {
        _store.Lease.Enqueue(Job());
        _store.CompleteBehavior = () => throw new InvalidOperationException("DB down");
        _handler.Behavior = _ => Task.CompletedTask;

        await _service.PollOnceAsync(TestContext.Current.CancellationToken);
        await _service.DrainAsync(TimeSpan.FromSeconds(5));

        _service.FreeSlots.ShouldBe(2);
    }

    [Fact]
    public async Task Retryable_error_uses_retry_after_and_permanent_error_dead_letters()
    {
        _store.Lease.Enqueue(Job());
        _handler.Behavior = _ => throw new RetryableJobException("429") { RetryAfter = TimeSpan.FromSeconds(42) };
        await _service.PollOnceAsync(TestContext.Current.CancellationToken);
        await _service.DrainAsync(TimeSpan.FromSeconds(5));

        _store.Lease.Enqueue(Job());
        _handler.Behavior = _ => throw new PermanentJobException("token sai");
        await _service.PollOnceAsync(TestContext.Current.CancellationToken);
        await _service.DrainAsync(TimeSpan.FromSeconds(5));

        _store.Failed.Count.ShouldBe(2);
        _store.Failed[0].Delay.ShouldBe(TimeSpan.FromSeconds(42));
        _store.Failed[0].Permanent.ShouldBeFalse();
        _store.Failed[1].Permanent.ShouldBeTrue();
        _service.FreeSlots.ShouldBe(2);
    }

    [Fact]
    public void Backoff_is_exponential_and_capped()
    {
        _service.RetryDelay(Job(attempt: 1), new InvalidOperationException()).ShouldBe(TimeSpan.FromSeconds(10));
        _service.RetryDelay(Job(attempt: 3), new InvalidOperationException()).ShouldBe(TimeSpan.FromSeconds(40));
        _service.RetryDelay(Job(attempt: 20), new InvalidOperationException()).ShouldBe(TimeSpan.FromSeconds(600));
        _service.RetryDelay(Job(), new RetryableJobException("429") { RetryAfter = TimeSpan.FromSeconds(1200) })
            .ShouldBe(TimeSpan.FromSeconds(1200));
    }

    private sealed class FixedWorker : IWorkerIdentity
    {
        public string WorkerId => "worker-test";
    }

    private sealed class TestHandler : IJobHandler
    {
        public Func<CancellationToken, Task> Behavior { get; set; } = _ => Task.CompletedTask;

        public bool WasCancelled { get; private set; }

        public string JobType => JobProcessingServiceTests.JobType;

        public async Task HandleAsync(LeasedJob job, CancellationToken cancellationToken)
        {
            try
            {
                await Behavior(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                throw;
            }
        }

        public Task OnDeadLetterAsync(DeadLetterJob job, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    internal sealed class FakeJobStore : IJobStore
    {
        private int _renewAttempts;

        public ConcurrentQueue<LeasedJob> Lease { get; } = new();

        public Func<bool> RenewBehavior { get; set; } = () => true;

        public Action CompleteBehavior { get; set; } = () => { };

        public int RenewAttempts => Volatile.Read(ref _renewAttempts);

        public List<Guid> Completed { get; } = [];

        public List<(Guid Id, TimeSpan Delay, bool Permanent)> Failed { get; } = [];

        public List<DeadLetterJob> DeadLetters { get; } = [];

        public List<Guid> Released { get; } = [];

        public List<Guid> Reconciled { get; } = [];

        public Task<IReadOnlyList<LeasedJob>> LeaseAsync(string workerId, int maxJobs, TimeSpan leaseDuration, JobConcurrencyLimit? limit, CancellationToken cancellationToken)
        {
            var result = new List<LeasedJob>();
            while (result.Count < maxJobs && Lease.TryDequeue(out var job))
            {
                result.Add(job);
            }

            return Task.FromResult<IReadOnlyList<LeasedJob>>(result);
        }

        public Task<bool> RenewLeaseAsync(Guid jobId, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _renewAttempts);
            return Task.FromResult(RenewBehavior());
        }

        public Task CompleteAsync(Guid jobId, string workerId, CancellationToken cancellationToken)
        {
            CompleteBehavior();
            lock (Completed)
            {
                Completed.Add(jobId);
            }

            return Task.CompletedTask;
        }

        public Task FailAsync(Guid jobId, string workerId, string error, TimeSpan retryDelay, bool permanent, CancellationToken cancellationToken)
        {
            lock (Failed)
            {
                Failed.Add((jobId, retryDelay, permanent));
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DeadLetterJob>> ClaimDeadLettersAsync(int maxJobs, CancellationToken cancellationToken)
        {
            var batch = DeadLetters.Take(maxJobs).ToList();
            DeadLetters.RemoveRange(0, batch.Count);
            return Task.FromResult<IReadOnlyList<DeadLetterJob>>(batch);
        }

        public Task ReleaseDeadLetterAsync(Guid jobId, string error, CancellationToken cancellationToken)
        {
            Released.Add(jobId);
            return Task.CompletedTask;
        }

        public Task CompleteDeadLetterAsync(Guid jobId, CancellationToken cancellationToken)
        {
            Reconciled.Add(jobId);
            return Task.CompletedTask;
        }
    }
}

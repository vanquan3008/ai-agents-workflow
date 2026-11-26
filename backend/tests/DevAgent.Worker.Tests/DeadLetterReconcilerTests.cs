using DevAgent.Application.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace DevAgent.Worker.Tests;

public sealed class DeadLetterReconcilerTests
{
    [Fact]
    public async Task Dead_letters_are_dispatched_to_handler_and_failures_are_released_for_retry()
    {
        var store = new JobProcessingServiceTests.FakeJobStore();
        var ok = new DeadLetterJob(Guid.NewGuid(), "ok", "{}", 5, 5, "boom");
        var broken = new DeadLetterJob(Guid.NewGuid(), "broken", "{}", 5, 5, "boom");
        store.DeadLetters.AddRange([ok, broken]);
        var handled = new List<Guid>();

        var services = new ServiceCollection()
            .AddSingleton<IJobStore>(store)
            .AddSingleton<IJobHandler>(new Handler("ok", job => handled.Add(job.Id)))
            .AddSingleton<IJobHandler>(new Handler("broken", _ => throw new InvalidOperationException("DB lỗi")))
            .BuildServiceProvider();

        var reconciler = new DeadLetterReconciler(
            services.GetRequiredService<IServiceScopeFactory>(),
            MsOptions.Create(new WorkerOptions()),
            new FakeTimeProvider(),
            NullLogger<DeadLetterReconciler>.Instance);

        var reconciled = await reconciler.ReconcileOnceAsync(TestContext.Current.CancellationToken);

        reconciled.ShouldBe(1);
        handled.ShouldBe([ok.Id]);
        store.Reconciled.ShouldBe([ok.Id]);
        store.Released.ShouldBe([broken.Id]);
    }

    private sealed class Handler(string type, Action<DeadLetterJob> onDeadLetter) : IJobHandler
    {
        public string JobType => type;

        public Task HandleAsync(LeasedJob job, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task OnDeadLetterAsync(DeadLetterJob job, CancellationToken cancellationToken)
        {
            onDeadLetter(job);
            return Task.CompletedTask;
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevAgent.Application.Abstractions.Integrations;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Features.Approvals;
using DevAgent.Application.Features.Tasks;
using DevAgent.Application.Notifications;
using DevAgent.Application.Orchestration;
using DevAgent.Domain.Common;
using DevAgent.Domain.Executions;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Workflows;
using DevAgent.Infrastructure.Persistence;
using DevAgent.Infrastructure.Persistence.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DevAgent.Api.IntegrationTests;

public sealed class ReliabilityAndWorkflowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";
    private static readonly Guid Owner = TestData.OwnerUserId.Value;
    private static readonly Guid Requester = TestData.RequesterUserId.Value;

    private async Task<Guid> ArrangeAsync()
    {
        Assert.SkipUnless(factory.DockerAvailable, "Cần Docker.");
        _ = factory.CreateClient();
        await factory.SeedAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
        return (await db.Repositories.FirstAsync(TestContext.Current.CancellationToken)).Id.Value;
    }

    [Fact]
    public async Task Worker_dying_on_last_attempt_is_reconciled_to_failed_task()
    {
        var repositoryId = await ArrangeAsync();
        var ct = TestContext.Current.CancellationToken;
        TaskId taskId;
        Guid jobId;

        // Dựng tình huống: task đang PLANNING, execution đang chạy, job ở lần thử cuối với lease đã hết hạn.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
            var repo = await db.Repositories.SingleAsync(r => r.Id == new RepositoryId(repositoryId), ct);
            var task = AgentTask.Create(repo.ProjectId, repo.Id, "t", "d", new TaskSource(TaskChannel.Api, null),
                new IdempotencyKey($"dl-{Guid.NewGuid():N}"), TestData.OwnerUserId, DateTimeOffset.UtcNow);
            task.StartPlanning(Sha, "b", WorkflowDefinition.Default, 0, Actor.System, DateTimeOffset.UtcNow);
            db.Tasks.Add(task);
            db.Executions.Add(AgentExecution.Start(task.Id, AgentStep.Planner, 1, null, "m", "dead-worker", DateTimeOffset.UtcNow));
            jobId = Guid.CreateVersion7();
            db.Jobs.Add(new JobRecord
            {
                Id = jobId,
                Type = JobTypes.ProcessTask,
                Payload = JsonSerializer.Serialize(new ProcessTaskPayload(task.Id.Value)),
                DeduplicationKey = $"dl-test:{jobId}",
                Status = JobStatus.Leased,
                Attempt = 5,
                MaxAttempts = 5,
                LeasedBy = "dead-worker",
                LeasedUntil = DateTimeOffset.UtcNow.AddMinutes(-1),
                AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            taskId = task.Id;
        }

        // Lần quét nhận việc kế tiếp chuyển job sang dead-letter; bước đối soát cập nhật task.
        using (var scope = factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IJobStore>();
            (await store.LeaseAsync("other-worker", 10, TimeSpan.FromMinutes(1), null, ct)).ShouldNotContain(j => j.Id == jobId);
            var dead = await store.ClaimDeadLettersAsync(50, ct);
            var mine = dead.Single(d => d.Id == jobId);

            var orchestrator = ActivatorUtilities.CreateInstance<AgentOrchestrator>(scope.ServiceProvider,
                (IEnumerable<Application.Orchestration.Steps.IWorkflowStep>)[]);
            var handler = ActivatorUtilities.CreateInstance<ProcessTaskJobHandler>(scope.ServiceProvider, (IAgentOrchestrator)orchestrator, new StubWorker());
            await handler.OnDeadLetterAsync(mine, ct);
            await store.CompleteDeadLetterAsync(mine.Id, ct);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
            var task = await scope.ServiceProvider.GetRequiredService<ITaskRepository>().GetByIdAsync(taskId, ct);
            task!.Status.ShouldBe(AgentTaskStatus.Failed);
            task.StatusReason!.ShouldContain("Lease hết hạn");
            (await db.Executions.SingleAsync(e => e.TaskId == taskId, ct)).Status.ShouldBe(ExecutionStatus.Failed);
            var job = await db.Jobs.SingleAsync(j => j.Id == jobId, ct);
            job.Status.ShouldBe(JobStatus.DeadLetter);
            job.DeadLetteredAt.ShouldNotBeNull();
            job.ReconciledAt.ShouldNotBeNull();
            (await scope.ServiceProvider.GetRequiredService<IJobStore>().ClaimDeadLettersAsync(50, ct)).ShouldNotContain(d => d.Id == jobId);
        }
    }

    [Fact]
    public async Task Reconciliation_claim_is_recoverable_after_worker_crash()
    {
        await ArrangeAsync();
        var ct = TestContext.Current.CancellationToken;
        var jobId = Guid.CreateVersion7();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
            db.Jobs.Add(new JobRecord
            {
                Id = jobId,
                Type = "test.reconcile",
                Payload = "{}",
                DeduplicationKey = $"reconcile:{jobId}",
                Status = JobStatus.DeadLetter,
                Attempt = 1,
                MaxAttempts = 1,
                AvailableAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                DeadLetteredAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            var store = scope.ServiceProvider.GetRequiredService<IJobStore>();
            (await store.ClaimDeadLettersAsync(100, ct)).ShouldContain(job => job.Id == jobId);
            (await store.ClaimDeadLettersAsync(100, ct)).ShouldNotContain(job => job.Id == jobId);

            await db.Entry(await db.Jobs.SingleAsync(job => job.Id == jobId, ct)).ReloadAsync(ct);
            (await db.Jobs.SingleAsync(job => job.Id == jobId, ct)).ReconciledAt.ShouldBeNull();

            // Mô phỏng tiến trình chết sau khi nhận đối soát: lease hết hạn phải nhận lại được.
            await db.Database.ExecuteSqlAsync(
                $"UPDATE devagent.jobs SET leased_until = now() - interval '1 second' WHERE id = {jobId}", ct);
        }

        using var recovery = factory.Services.CreateScope();
        var recoveredStore = recovery.ServiceProvider.GetRequiredService<IJobStore>();
        (await recoveredStore.ClaimDeadLettersAsync(100, ct)).ShouldContain(job => job.Id == jobId);
        await recoveredStore.CompleteDeadLetterAsync(jobId, ct);
        (await recoveredStore.ClaimDeadLettersAsync(100, ct)).ShouldNotContain(job => job.Id == jobId);
    }

    [Fact]
    public async Task Notification_is_stored_in_same_transaction_and_sent_with_dedup_key()
    {
        var repositoryId = await ArrangeAsync();
        var ct = TestContext.Current.CancellationToken;
        var client = factory.ClientFor(Owner);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/tasks")
        {
            Content = JsonContent.Create(new { repositoryId, title = "Thông báo", description = "Mô tả" }),
            Headers = { { "Idempotency-Key", $"notify-{Guid.NewGuid():N}" } },
        }, ct);
        var taskId = (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("taskId").GetGuid();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
        var job = await db.Jobs.SingleAsync(j => j.DeduplicationKey == $"{JobTypes.SendNotification}:{taskId}:1", ct);

        var notifier = new CapturingNotifier();
        var handler = ActivatorUtilities.CreateInstance<NotificationJobHandler>(scope.ServiceProvider, (IChatNotifier)notifier);
        await handler.HandleAsync(new LeasedJob(job.Id, job.Type, job.Payload, 1, 5, DateTimeOffset.UtcNow.AddMinutes(1)), ct);

        var sent = notifier.Sent.ShouldHaveSingleItem();
        sent.Kind.ShouldBe(ChatNotificationKind.TaskAccepted);
        sent.Title.ShouldBe("Thông báo");
        sent.DeduplicationKey.ShouldBe(job.DeduplicationKey);
        sent.Link!.ShouldEndWith($"/tasks/{taskId}");
    }

    [Fact]
    public async Task Workflow_api_versions_changes_and_enforces_permissions()
    {
        var repositoryId = await ArrangeAsync();
        var ct = TestContext.Current.CancellationToken;
        var owner = factory.ClientFor(Owner);
        var url = $"/api/v1/repositories/{repositoryId}/workflow";

        var current = await owner.GetFromJsonAsync<JsonElement>(url, ct);
        var version = current.GetProperty("version").GetInt32();
        var definition = JsonSerializer.Deserialize<Dictionary<string, object?>>(current.GetProperty("definition").GetRawText())!;
        definition["planApproval"] = version % 2 == 0 ? "ALWAYS" : "ON_SENSITIVE";
        definition["maxFixIterations"] = 1;

        var requesterResponse = await factory.ClientFor(Requester).PutAsJsonAsync(url, new { expectedVersion = version, definition, comment = "x" }, ct);
        requesterResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var updated = await owner.PutAsJsonAsync(url, new { expectedVersion = version, definition, comment = "siết duyệt" }, ct);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await updated.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("version").GetInt32().ShouldBe(version + 1);

        var stale = await owner.PutAsJsonAsync(url, new { expectedVersion = version, definition, comment = "cũ" }, ct);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        definition["reviewerBlockingSeverity"] = "CRITICAL";
        var loosened = await owner.PutAsJsonAsync(url, new { expectedVersion = version + 1, definition, comment = "nới" }, ct);
        loosened.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var revisions = await owner.GetFromJsonAsync<JsonElement>($"{url}/revisions", ct);
        revisions[0].GetProperty("version").GetInt32().ShouldBe(version + 1);
        revisions[0].GetProperty("comment").GetString().ShouldBe("siết duyệt");

        var outsider = factory.ClientFor(TestData.OutsiderUserId.Value);
        (await outsider.GetAsync(url, ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Concurrency_cap_applies_only_to_limited_job_type()
    {
        await ArrangeAsync();
        var ct = TestContext.Current.CancellationToken;
        using (var scope = factory.Services.CreateScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
            for (var i = 0; i < 3; i++)
            {
                await queue.EnqueueAsync(new JobRequest("test.limited", "{}", $"limited-{Guid.NewGuid():N}"), ct);
            }

            for (var i = 0; i < 2; i++)
            {
                await queue.EnqueueAsync(new JobRequest("test.free", "{}", $"free-{Guid.NewGuid():N}"), ct);
            }

            await scope.ServiceProvider.GetRequiredService<DevAgentDbContext>().SaveChangesAsync(ct);
        }

        using var leaseScope = factory.Services.CreateScope();
        var leased = await leaseScope.ServiceProvider.GetRequiredService<IJobStore>()
            .LeaseAsync("w", 50, TimeSpan.FromMinutes(1), new JobConcurrencyLimit("test.limited", 1), ct);

        leased.Count(j => j.Type == "test.limited").ShouldBe(1);
        leased.Count(j => j.Type == "test.free").ShouldBe(2);
    }

    private sealed class StubWorker : IWorkerIdentity
    {
        public string WorkerId => "test-worker";
    }

    private sealed class CapturingNotifier : IChatNotifier
    {
        public List<ChatNotification> Sent { get; } = [];

        public Task NotifyAsync(ChatNotification notification, CancellationToken cancellationToken)
        {
            Sent.Add(notification);
            return Task.CompletedTask;
        }
    }
}

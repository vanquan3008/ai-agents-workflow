using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Infrastructure.Persistence;
using DevAgent.Infrastructure.Persistence.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DevAgent.Api.IntegrationTests;

/// <summary>
/// Phase 1 acceptance scenarios on real PostgreSQL: repeated submissions create one task, outsiders are denied,
/// and two workers never lease the same job.
/// </summary>
public sealed class Phase1AcceptanceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Guid Owner = TestData.OwnerUserId.Value;
    private static readonly Guid Outsider = TestData.OutsiderUserId.Value;

    private async Task<(HttpClient Client, Guid RepositoryId)> ArrangeAsync()
    {
        Assert.SkipUnless(factory.DockerAvailable, "Cần Docker để chạy PostgreSQL bằng Testcontainers.");
        _ = factory.CreateClient();
        await factory.SeedAsync();
        var client = factory.ClientFor(Owner);

        var projects = await client.GetFromJsonAsync<JsonElement>("/api/v1/projects", TestContext.Current.CancellationToken);
        var projectId = projects.EnumerateArray().Single(p => p.GetProperty("slug").GetString() == TestData.ProjectSlug).GetProperty("id").GetGuid();
        var repositories = await client.GetFromJsonAsync<JsonElement>($"/api/v1/projects/{projectId}/repositories", TestContext.Current.CancellationToken);
        return (client, repositories[0].GetProperty("id").GetGuid());
    }

    private static HttpRequestMessage CreateTaskRequest(Guid repositoryId, string? idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tasks")
        {
            Content = JsonContent.Create(new { repositoryId, title = "Bộ lọc sai trạng thái", description = "Lọc DONE trả về IN_PROGRESS." }),
        };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return request;
    }

    [Fact]
    public async Task Health_endpoints_report_ready()
    {
        Assert.SkipUnless(factory.DockerAvailable, "Cần Docker.");
        var client = factory.CreateClient();

        (await client.GetAsync("/health/live", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Repeated_request_with_same_key_creates_exactly_one_task_and_one_job()
    {
        var (client, repositoryId) = await ArrangeAsync();
        var key = $"it-{Guid.NewGuid():N}";
        var ct = TestContext.Current.CancellationToken;

        var first = await client.SendAsync(CreateTaskRequest(repositoryId, key), ct);
        var second = await client.SendAsync(CreateTaskRequest(repositoryId, key), ct);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(ct);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(ct);
        var taskId = firstBody.GetProperty("taskId").GetGuid();
        secondBody.GetProperty("taskId").GetGuid().ShouldBe(taskId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
        var prefix = $"{JobTypes.ProcessTask}:{taskId}:";
        (await db.Jobs.CountAsync(j => j.DeduplicationKey.StartsWith(prefix), ct)).ShouldBe(1);
    }

    [Fact]
    public async Task Concurrent_requests_with_same_key_still_create_one_task()
    {
        var (client, repositoryId) = await ArrangeAsync();
        var key = $"race-{Guid.NewGuid():N}";
        var ct = TestContext.Current.CancellationToken;

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.SendAsync(CreateTaskRequest(repositoryId, key), ct)));

        responses.ShouldAllBe(r => r.IsSuccessStatusCode);
        var ids = await Task.WhenAll(responses.Select(async r => (await r.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("taskId").GetGuid()));
        ids.Distinct().Count().ShouldBe(1);
    }

    [Fact]
    public async Task Missing_idempotency_key_is_rejected()
    {
        var (client, repositoryId) = await ArrangeAsync();

        var response = await client.SendAsync(CreateTaskRequest(repositoryId, null), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Outsider_cannot_read_task_or_see_it_in_list()
    {
        var (client, repositoryId) = await ArrangeAsync();
        var ct = TestContext.Current.CancellationToken;
        var created = await client.SendAsync(CreateTaskRequest(repositoryId, $"auth-{Guid.NewGuid():N}"), ct);
        var taskId = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("taskId").GetGuid();
        var outsider = factory.ClientFor(Outsider);

        (await outsider.GetAsync($"/api/v1/tasks/{taskId}", ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await outsider.GetAsync($"/api/v1/tasks/{taskId}/timeline", ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var list = await outsider.GetFromJsonAsync<JsonElement>("/api/v1/tasks", ct);
        list.GetProperty("totalCount").GetInt64().ShouldBe(0);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
        (await db.AuditEntries.AnyAsync(a => a.ResourceId == taskId.ToString() && a.Outcome == Domain.Auditing.AuditOutcome.Denied, ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Timeline_returns_history_in_order()
    {
        var (client, repositoryId) = await ArrangeAsync();
        var ct = TestContext.Current.CancellationToken;
        var created = await client.SendAsync(CreateTaskRequest(repositoryId, $"tl-{Guid.NewGuid():N}"), ct);
        var taskId = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("taskId").GetGuid();

        var timeline = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{taskId}/timeline", ct);

        timeline[0].GetProperty("sequence").GetInt64().ShouldBe(1);
        timeline[0].GetProperty("to").GetString().ShouldBe("PENDING");
    }

    [Fact]
    public async Task Two_workers_never_lease_the_same_job()
    {
        Assert.SkipUnless(factory.DockerAvailable, "Cần Docker.");
        var ct = TestContext.Current.CancellationToken;
        _ = factory.CreateClient(); // start the host to apply migrations

        using (var scope = factory.Services.CreateScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
            for (var i = 0; i < 10; i++)
            {
                await queue.EnqueueAsync(new JobRequest("test.noop", "{}", $"lease-{Guid.NewGuid():N}"), ct);
            }

            await scope.ServiceProvider.GetRequiredService<DevAgentDbContext>().SaveChangesAsync(ct);
        }

        async Task<IReadOnlyList<LeasedJob>> LeaseAs(string worker)
        {
            using var scope = factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IJobStore>().LeaseAsync(worker, 10, TimeSpan.FromMinutes(1), null, ct);
        }

        var results = await Task.WhenAll(LeaseAs("worker-a"), LeaseAs("worker-b"));
        var all = results.SelectMany(r => r.Where(j => j.Type == "test.noop")).Select(j => j.Id).ToArray();

        all.Length.ShouldBe(all.Distinct().Count());

        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<DevAgentDbContext>();
        (await db.Jobs.CountAsync(j => j.Type == "test.noop" && j.Status == JobStatus.Leased, ct)).ShouldBe(all.Length);
    }
}

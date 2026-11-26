using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Domain.Common;
using DevAgent.Domain.Executions;
using DevAgent.Domain.Policies;
using DevAgent.Domain.Tasks;
using DevAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DevAgent.Api.IntegrationTests;

/// <summary>Aggregate được lưu/tải lại đúng qua EF Core + PostgreSQL thật (owned collection, value object, xmin).</summary>
public sealed class PersistenceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public async Task Task_aggregate_round_trips_transitions_plans_and_pull_request()
    {
        Assert.SkipUnless(factory.DockerAvailable, "Cần Docker.");
        var ct = TestContext.Current.CancellationToken;
        _ = factory.CreateClient();
        await factory.SeedAsync();
        var now = DateTimeOffset.UtcNow;
        TaskId taskId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
            var repo = await db.Repositories.FirstAsync(ct);
            var task = AgentTask.Create(repo.ProjectId, repo.Id, "t", "d", new TaskSource(TaskChannel.Api, "ref-1"),
                new IdempotencyKey($"persist-{Guid.NewGuid():N}"), TestData.OwnerUserId, now);
            scope.ServiceProvider.GetRequiredService<ITaskRepository>().Add(task);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
            taskId = task.Id;
        }

        // Mỗi bước dùng scope mới, giống Worker xử lý từng job.
        await Mutate(t => t.StartPlanning(Sha, "devagent/task-x", Domain.Workflows.WorkflowDefinition.Default, 0, Actor.System, now));
        await Mutate(t =>
        {
            t.AttachPlan("plan", ["s1"], ["c1"], ["r1"], ["src/a.cs"],
                new PolicyDecision(PolicyOutcome.AutoApproved, SensitiveArea.None, 1, ["ok"]), now);
            t.StartCoding(Actor.System, "auto", now);
        });
        await Mutate(t =>
        {
            t.StartTesting(Actor.System, now);
            t.StartReview(Actor.System, now);
            t.StartPublishing(Actor.System, "ok", now);
            t.Complete(new PullRequestLink(7, "https://github.com/acme/app/pull/7", "devagent/task-x", Sha), Actor.System, now);
        });

        using (var scope = factory.Services.CreateScope())
        {
            var task = (await scope.ServiceProvider.GetRequiredService<ITaskRepository>().GetByIdAsync(taskId, ct))!;
            task.Status.ShouldBe(AgentTaskStatus.Completed);
            task.Transitions.Select(x => x.Sequence).Order().ShouldBe([1L, 2, 3, 4, 5, 6, 7]);
            task.CurrentPlan!.Steps.ShouldBe(["s1"]);
            task.PullRequest!.Number.ShouldBe(7);
            task.Source.ExternalReference.ShouldBe("ref-1");
        }

        async Task Mutate(Action<AgentTask> change)
        {
            using var scope = factory.Services.CreateScope();
            var task = (await scope.ServiceProvider.GetRequiredService<ITaskRepository>().GetByIdAsync(taskId, ct))!;
            change(task);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        }
    }

    [Fact]
    public async Task Execution_usage_and_tool_calls_round_trip()
    {
        Assert.SkipUnless(factory.DockerAvailable, "Cần Docker.");
        var ct = TestContext.Current.CancellationToken;
        _ = factory.CreateClient();
        await factory.SeedAsync();
        var now = DateTimeOffset.UtcNow;
        ExecutionId executionId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
            var repo = await db.Repositories.FirstAsync(ct);
            var task = AgentTask.Create(repo.ProjectId, repo.Id, "t", "d", new TaskSource(TaskChannel.Api, null),
                new IdempotencyKey($"exec-{Guid.NewGuid():N}"), TestData.OwnerUserId, now);
            db.Tasks.Add(task);
            var execution = AgentExecution.Start(task.Id, AgentStep.Planner, 1, null, "m", "w", now);
            db.Executions.Add(execution);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
            executionId = execution.Id;
        }

        using (var scope = factory.Services.CreateScope())
        {
            var executions = scope.ServiceProvider.GetRequiredService<IExecutionRepository>();
            var execution = (await executions.GetByIdAsync(executionId, ct))!;
            execution.RecordModelCall(new TokenUsage(100, 20, 5, 0.0123m));
            execution.BeginTool("repo.read_file", "{\"path\":\"src/a.cs\"}", now).Succeed("{\"ok\":true}", 0, now);
            execution.Succeed(now);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var execution = (await scope.ServiceProvider.GetRequiredService<IExecutionRepository>().GetByIdAsync(executionId, ct))!;
            execution.Status.ShouldBe(ExecutionStatus.Succeeded);
            execution.Usage.EstimatedCostUsd.ShouldBe(0.0123m);
            execution.ToolExecutions.ShouldHaveSingleItem().ExitCode.ShouldBe(0);
        }
    }
}

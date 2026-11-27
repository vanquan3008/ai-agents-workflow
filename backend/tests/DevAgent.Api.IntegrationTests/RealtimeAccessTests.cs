using System.Net;
using System.Threading.Channels;
using DevAgent.Api.Realtime;
using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Abstractions.Realtime;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Domain.Common;
using DevAgent.Domain.Policies;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Users;
using DevAgent.Domain.Users.Repositories;
using DevAgent.Infrastructure.Options;
using DevAgent.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace DevAgent.Api.IntegrationTests;

/// <summary>
/// Realtime access through the real hub, PostgreSQL NOTIFY and the listener: a connection that already joined a task
/// group stops receiving events once access is revoked, and connections outliving their token are closed.
/// </summary>
public sealed class RealtimeAccessTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Silence = TimeSpan.FromSeconds(2);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Removed_member_stops_receiving_events_of_a_task_it_already_subscribed_to()
    {
        var scenario = await ArrangeAsync();
        await using var hub = await ConnectAsync(scenario.MemberId);
        var events = ListenForEvents(hub);
        (await hub.InvokeAsync<bool>("SubscribeTask", scenario.TaskId, Ct)).ShouldBeTrue();

        // Positive control: the subscription delivers events.
        await PublishAsync(scenario);
        (await ReceiveAsync(events, Wait)).ShouldBeTrue();

        var connection = Registry.ForUser(scenario.MemberId).Single();
        var revision = connection.Revision;
        var removed = await factory.ClientFor(TestData.OwnerUserId.Value)
            .DeleteAsync($"/api/v1/projects/{scenario.ProjectId}/members/{scenario.MemberId}", Ct);
        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await WaitUntilAsync(() => connection.Revision > revision);

        await PublishAsync(scenario);
        (await ReceiveAsync(events, Silence)).ShouldBeFalse();
        (await hub.InvokeAsync<bool>("SubscribeTask", scenario.TaskId, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Deactivated_user_is_disconnected()
    {
        var scenario = await ArrangeAsync();
        await using var hub = await ConnectAsync(scenario.MemberId);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };
        (await hub.InvokeAsync<bool>("SubscribeTask", scenario.TaskId, Ct)).ShouldBeTrue();

        using (var scope = factory.Services.CreateScope())
        {
            var user = (await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdAsync(new UserId(scenario.MemberId), Ct))!;
            user.Deactivate(DateTimeOffset.UtcNow);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        }

        await closed.Task.WaitAsync(Wait, Ct);
        hub.State.ShouldBe(HubConnectionState.Disconnected);
    }

    [Fact]
    public async Task Connections_are_closed_once_their_access_token_expires()
    {
        var scenario = await ArrangeAsync();
        await using var hub = await ConnectAsync(scenario.MemberId);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };
        var connection = Registry.ForUser(scenario.MemberId).Single();
        connection.ExpiresAt.ShouldNotBeNull();

        var sweeper = new RealtimeConnectionSweeper(Registry, factory.Services.GetRequiredService<IOptions<RealtimeOptions>>(),
            new FixedClock(connection.ExpiresAt.Value.AddSeconds(1)), NullLogger<RealtimeConnectionSweeper>.Instance);
        sweeper.Sweep().ShouldBeGreaterThanOrEqualTo(1);

        await closed.Task.WaitAsync(Wait, Ct);
    }

    private RealtimeConnectionRegistry Registry => factory.Services.GetRequiredService<RealtimeConnectionRegistry>();

    private async Task<Scenario> ArrangeAsync()
    {
        Assert.SkipUnless(factory.DockerAvailable, "Cần Docker.");
        _ = factory.CreateClient();
        await factory.SeedAsync();

        // Each test gets its own member, project and task so revocations never affect other tests.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
        var now = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var member = User.Create($"rt-{suffix}@devagent.test", "Realtime", SystemRole.User, now);
        member.SetPassword(scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(TestData.Password), mustChange: false, now);
        var project = Project.Create("Realtime", $"rt-{suffix}", TestData.OwnerUserId, now);
        project.AddOrUpdateMember(member.Id, ProjectRole.Viewer, now);
        var repository = CodeRepository.Register(project.Id, 1, "devagent-rt", $"app-{suffix}", "main", RepositoryPolicy.CreateDefault("dotnet build", "dotnet test"), now);
        var task = AgentTask.Create(project.Id, repository.Id, "Theo dõi realtime", "d", new TaskSource(TaskChannel.Api, null),
            new IdempotencyKey($"rt-{suffix}"), TestData.OwnerUserId, now);
        db.AddRange(member, project, repository, task);
        await db.SaveChangesAsync(Ct);
        return new Scenario(member.Id.Value, project.Id.Value, task.Id.Value);
    }

    private async Task<HubConnection> ConnectAsync(Guid userId)
    {
        var token = factory.AccessTokenFor(userId);
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, TasksHub.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
        await hub.StartAsync(Ct);
        await WaitUntilAsync(() => Registry.ForUser(userId).Count == 1);
        return hub;
    }

    private static ChannelReader<TaskRealtimeEvent> ListenForEvents(HubConnection hub)
    {
        var channel = Channel.CreateUnbounded<TaskRealtimeEvent>();
        hub.On<TaskRealtimeEvent>(TasksHub.EventName, e => channel.Writer.TryWrite(e));
        return channel.Reader;
    }

    private async Task PublishAsync(Scenario scenario)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ITaskEventPublisher>().PublishAsync(
            new TaskRealtimeEvent(scenario.TaskId, scenario.ProjectId, "task.status_changed", 1, "PLANNING", null, DateTimeOffset.UtcNow), Ct);
    }

    private static async Task<bool> ReceiveAsync(ChannelReader<TaskRealtimeEvent> events, TimeSpan timeout)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        cts.CancelAfter(timeout);
        try
        {
            await events.ReadAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException) when (!Ct.IsCancellationRequested)
        {
            return false;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow + Wait;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("Điều kiện không xảy ra trong thời gian chờ.");
            }

            await Task.Delay(50, Ct);
        }
    }

    private sealed record Scenario(Guid MemberId, Guid ProjectId, Guid TaskId);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

using System.Globalization;
using System.Security.Claims;
using DevAgent.Api.Auth;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Tasks;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DevAgent.Api.Realtime;

/// <summary>
/// Realtime hub for the dashboard. Subscriptions pass the same authorization as the read API, so outsiders cannot
/// join a task group. Each joined group is recorded with the project that authorized it, so losing access removes
/// the connection from those groups and an expired token closes the connection. Clients connect to /hubs/tasks,
/// call SubscribeTask(taskId) and receive "taskEvent"; after a reconnect they replay with
/// GET /api/v1/tasks/{id}/timeline?after=&lt;sequence&gt;.
/// </summary>
[Authorize]
internal sealed class TasksHub(HttpCurrentUser currentUser, RealtimeConnectionRegistry registry) : Hub
{
    public const string Path = "/hubs/tasks";
    public const string EventName = "taskEvent";

    public static string TaskGroup(Guid taskId) => $"task:{taskId}";

    public static string ProjectGroup(Guid projectId) => $"project:{projectId}";

    public override Task OnConnectedAsync()
    {
        if (!Guid.TryParse(Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
        {
            Context.Abort();
            return Task.CompletedTask;
        }

        registry.Add(new RealtimeConnection(Context.ConnectionId, userId, TokenExpiry(Context.User), Context.Abort));
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        registry.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public async Task<bool> SubscribeTask(Guid taskId, IQueryHandler<AuthorizeTaskSubscriptionQuery, Guid> authorize)
    {
        ArgumentNullException.ThrowIfNull(authorize);
        return await JoinAsync(TaskGroup(taskId), async () =>
        {
            var result = await authorize.HandleAsync(new AuthorizeTaskSubscriptionQuery(taskId), Context.ConnectionAborted);
            return result.IsSuccess ? result.Value : null;
        });
    }

    public Task UnsubscribeTask(Guid taskId) => LeaveAsync(TaskGroup(taskId));

    public async Task<bool> SubscribeProject(Guid projectId, ProjectAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);
        return await JoinAsync(ProjectGroup(projectId), async () =>
        {
            var result = await access.RequireAsync(
                new ProjectId(projectId), ProjectPermission.ViewTasks, "project.subscription", projectId.ToString(), Context.ConnectionAborted);
            return result.IsSuccess ? projectId : null;
        });
    }

    public Task UnsubscribeProject(Guid projectId) => LeaveAsync(ProjectGroup(projectId));

    /// <summary>
    /// Authorizes, records and joins a group. A revocation that lands while this runs bumps the connection revision,
    /// and the join is rolled back so a stale authorization never outlives the revocation.
    /// </summary>
    private async Task<bool> JoinAsync(string group, Func<Task<Guid?>> authorizeProject)
    {
        if (registry.Find(Context.ConnectionId) is not { } connection)
        {
            return false;
        }

        currentUser.UsePrincipal(Context.User);
        var revision = connection.Revision;
        if (await authorizeProject() is not { } projectId)
        {
            return false;
        }

        connection.Track(group, projectId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
        if (connection.Revision == revision)
        {
            return true;
        }

        connection.Untrack(group);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
        return false;
    }

    private async Task LeaveAsync(string group)
    {
        registry.Find(Context.ConnectionId)?.Untrack(group);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
    }

    private static DateTimeOffset? TokenExpiry(ClaimsPrincipal? user) =>
        long.TryParse(user?.FindFirst(JwtRegisteredClaimNames.Exp)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
}

using DevAgent.Api.Auth;
using DevAgent.Application.Abstractions.Realtime;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Tasks;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Infrastructure.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DevAgent.Api.Realtime;

/// <summary>
/// Hub realtime cho dashboard. Đăng ký nhận sự kiện đi qua cùng kiểm tra quyền với API đọc (checklist 1.3):
/// người ngoài project không thể vào group của task.
/// Client: kết nối /hubs/tasks, gọi SubscribeTask(taskId), nhận "taskEvent"; sau khi kết nối lại,
/// gọi GET /api/v1/tasks/{id}/timeline?after=&lt;sequence&gt; để đọc bù.
/// </summary>
[Authorize]
internal sealed class TasksHub(HttpCurrentUser currentUser) : Hub
{
    public const string Path = "/hubs/tasks";
    public const string EventName = "taskEvent";

    public static string TaskGroup(Guid taskId) => $"task:{taskId}";

    public static string ProjectGroup(Guid projectId) => $"project:{projectId}";

    public async Task<bool> SubscribeTask(Guid taskId, IQueryHandler<AuthorizeTaskSubscriptionQuery, Guid> authorize)
    {
        ArgumentNullException.ThrowIfNull(authorize);
        currentUser.UsePrincipal(Context.User);
        var result = await authorize.HandleAsync(new AuthorizeTaskSubscriptionQuery(taskId), Context.ConnectionAborted);
        if (result.IsFailure)
        {
            return false;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, TaskGroup(taskId), Context.ConnectionAborted);
        return true;
    }

    public Task UnsubscribeTask(Guid taskId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, TaskGroup(taskId));

    public async Task<bool> SubscribeProject(Guid projectId, ProjectAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);
        currentUser.UsePrincipal(Context.User);
        var result = await access.RequireAsync(new ProjectId(projectId), ProjectPermission.ViewTasks, "project.subscription", projectId.ToString(), Context.ConnectionAborted);
        if (result.IsFailure)
        {
            return false;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, ProjectGroup(projectId), Context.ConnectionAborted);
        return true;
    }

    public Task UnsubscribeProject(Guid projectId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, ProjectGroup(projectId));
}

/// <summary>Nhận sự kiện từ LISTEN PostgreSQL và đẩy tới group của task và project.</summary>
internal sealed class SignalRTaskEventSink(IHubContext<TasksHub> hub) : ITaskEventSink
{
    public Task DeliverAsync(TaskRealtimeEvent taskEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(taskEvent);
        return hub.Clients
            .Groups(TasksHub.TaskGroup(taskEvent.TaskId), TasksHub.ProjectGroup(taskEvent.ProjectId))
            .SendAsync(TasksHub.EventName, taskEvent, cancellationToken);
    }
}

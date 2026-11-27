using DevAgent.Application.Abstractions.Realtime;
using DevAgent.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace DevAgent.Api.Realtime;

/// <summary>Receives events from the PostgreSQL listener and pushes them to the task and project groups.</summary>
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

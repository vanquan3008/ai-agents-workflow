using DevAgent.Application.Abstractions.Realtime;
using DevAgent.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace DevAgent.Api.Realtime;

/// <summary>
/// Applies a revocation to this process's connections: project revocations leave that project's groups, user-wide
/// revocations close the connection so the client must reconnect with a token that passes the new checks.
/// </summary>
internal sealed class SignalRAccessSink(RealtimeConnectionRegistry registry, IHubContext<TasksHub> hub) : IRealtimeAccessSink
{
    public async Task ApplyAsync(RealtimeAccessRevocation revocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(revocation);
        foreach (var connection in registry.ForUser(revocation.UserId))
        {
            if (revocation.ProjectId is not { } projectId)
            {
                connection.Abort();
                continue;
            }

            foreach (var group in connection.RevokeProject(projectId))
            {
                await hub.Groups.RemoveFromGroupAsync(connection.ConnectionId, group, cancellationToken);
            }
        }
    }
}

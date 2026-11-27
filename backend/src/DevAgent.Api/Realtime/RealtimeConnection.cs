using System.Collections.Concurrent;

namespace DevAgent.Api.Realtime;

/// <summary>
/// One SignalR connection: who opened it, until when its access token is valid and which groups it joined, each with
/// the project that authorized it. Revocations use this to find exactly the groups to leave.
/// </summary>
internal sealed class RealtimeConnection(string connectionId, Guid userId, DateTimeOffset? expiresAt, Action abort)
{
    private readonly ConcurrentDictionary<string, Guid> _groups = new(StringComparer.Ordinal);
    private int _revision;

    public string ConnectionId => connectionId;

    public Guid UserId => userId;

    /// <summary>Expiry of the access token the connection was opened with; SignalR does not re-authenticate on its own.</summary>
    public DateTimeOffset? ExpiresAt => expiresAt;

    /// <summary>Incremented by every revocation, so a subscription that raced with one can be rolled back.</summary>
    public int Revision => Volatile.Read(ref _revision);

    public void Track(string group, Guid projectId) => _groups[group] = projectId;

    public void Untrack(string group) => _groups.TryRemove(group, out _);

    /// <summary>Forgets every group authorized by the project and returns them so the caller can leave them.</summary>
    public IReadOnlyList<string> RevokeProject(Guid projectId)
    {
        Interlocked.Increment(ref _revision);
        var groups = _groups.Where(g => g.Value == projectId).Select(g => g.Key).ToArray();
        foreach (var group in groups)
        {
            _groups.TryRemove(group, out _);
        }

        return groups;
    }

    public void Abort()
    {
        Interlocked.Increment(ref _revision);
        abort();
    }
}

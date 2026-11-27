using System.Collections.Concurrent;

namespace DevAgent.Api.Realtime;

/// <summary>
/// The realtime connections held by this API process. Revocations and expiry sweeps are rare compared with event
/// delivery, which goes straight to SignalR groups, so linear scans here stay off the hot path.
/// </summary>
internal sealed class RealtimeConnectionRegistry
{
    private readonly ConcurrentDictionary<string, RealtimeConnection> _connections = new(StringComparer.Ordinal);

    public int Count => _connections.Count;

    public void Add(RealtimeConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connections[connection.ConnectionId] = connection;
    }

    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    public RealtimeConnection? Find(string connectionId) => _connections.GetValueOrDefault(connectionId);

    public IReadOnlyList<RealtimeConnection> ForUser(Guid userId) =>
        _connections.Values.Where(c => c.UserId == userId).ToArray();

    public IReadOnlyList<RealtimeConnection> ExpiredAt(DateTimeOffset now) =>
        _connections.Values.Where(c => c.ExpiresAt is { } expiresAt && expiresAt <= now).ToArray();
}

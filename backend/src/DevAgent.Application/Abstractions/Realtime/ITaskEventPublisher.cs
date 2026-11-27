namespace DevAgent.Application.Abstractions.Realtime;

/// <summary>Realtime event pushed to the dashboard. Clients use <see cref="Sequence"/> to replay persisted history.</summary>
public sealed record TaskRealtimeEvent(
    Guid TaskId,
    Guid ProjectId,
    string Kind,
    long Sequence,
    string? Status,
    string? Message,
    DateTimeOffset OccurredAt);

/// <summary>
/// Publishes realtime events. The worker and API are separate processes, so the implementation uses PostgreSQL
/// NOTIFY and the API relays to SignalR. Best effort only; the database remains the source of truth.
/// </summary>
public interface ITaskEventPublisher
{
    Task PublishAsync(TaskRealtimeEvent taskEvent, CancellationToken cancellationToken);
}

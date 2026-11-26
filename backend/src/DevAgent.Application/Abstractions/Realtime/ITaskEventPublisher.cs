namespace DevAgent.Application.Abstractions.Realtime;

/// <summary>Sự kiện realtime gửi tới dashboard. Client dùng <see cref="Sequence"/> để đọc bù từ lịch sử đã lưu.</summary>
public sealed record TaskRealtimeEvent(
    Guid TaskId,
    Guid ProjectId,
    string Kind,
    long Sequence,
    string? Status,
    string? Message,
    DateTimeOffset OccurredAt);

/// <summary>
/// Phát sự kiện realtime. Worker và API là hai tiến trình nên hiện thực dùng PostgreSQL NOTIFY,
/// API LISTEN rồi đẩy qua SignalR. Phát sự kiện là best-effort; nguồn sự thật là DB.
/// </summary>
public interface ITaskEventPublisher
{
    Task PublishAsync(TaskRealtimeEvent taskEvent, CancellationToken cancellationToken);
}

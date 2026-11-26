using DevAgent.Domain.Common;

namespace DevAgent.Application.Abstractions.Events;

/// <summary>
/// Xử lý domain event sau khi giao dịch commit. Lỗi ở handler được log, không hoàn tác giao dịch,
/// vì nguồn sự thật (DB) đã được lưu; client đọc bù từ lịch sử.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

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

/// <summary>
/// Xử lý domain event <b>trước khi commit, trong cùng giao dịch</b> (transactional outbox).
/// Chỉ được ghi thêm dữ liệu vào đơn vị giao dịch hiện tại (vd xếp job thông báo), không gọi dịch vụ ngoài.
/// Lỗi ở đây làm hỏng cả giao dịch, nên việc nghiệp vụ và việc kèm theo luôn cùng thành công hoặc cùng thất bại.
/// </summary>
public interface ITransactionalDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

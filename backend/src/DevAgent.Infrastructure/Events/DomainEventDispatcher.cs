using System.Collections.Concurrent;
using System.Reflection;
using DevAgent.Application.Abstractions.Events;
using DevAgent.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevAgent.Infrastructure.Events;

/// <summary>
/// Phát domain event tới các <see cref="IDomainEventHandler{TEvent}"/> trong cùng scope, sau khi commit.
/// Lỗi handler chỉ được log: dữ liệu đã lưu là nguồn sự thật, client đọc bù từ lịch sử.
/// </summary>
internal sealed partial class DomainEventDispatcher(IServiceProvider services, ILogger<DomainEventDispatcher> logger)
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> DispatchMethods = new();

    private static readonly MethodInfo DispatchTypedMethod =
        typeof(DomainEventDispatcher).GetMethod(nameof(DispatchTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in events)
        {
            var method = DispatchMethods.GetOrAdd(domainEvent.GetType(), t => DispatchTypedMethod.MakeGenericMethod(t));
            await (Task)method.Invoke(this, [domainEvent, cancellationToken])!;
        }
    }

    private async Task DispatchTypedAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
        {
            try
            {
                await handler.HandleAsync(domainEvent, cancellationToken);
            }
#pragma warning disable CA1031 // Handler sau commit không được làm hỏng use case đã thành công.
            catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
            {
                LogHandlerFailed(logger, ex, typeof(TEvent).Name, handler.GetType().Name);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Domain event handler {Handler} lỗi khi xử lý {EventType}")]
    private static partial void LogHandlerFailed(ILogger logger, Exception exception, string eventType, string handler);
}

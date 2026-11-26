using System.Collections.Concurrent;
using System.Reflection;
using DevAgent.Application.Abstractions.Events;
using DevAgent.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevAgent.Infrastructure.Events;

/// <summary>
/// Phát domain event tới handler trong cùng scope:
/// - <see cref="ITransactionalDomainEventHandler{TEvent}"/> trước commit; lỗi làm hỏng cả giao dịch (outbox).
/// - <see cref="IDomainEventHandler{TEvent}"/> sau commit; lỗi chỉ được log vì dữ liệu đã lưu là nguồn sự thật.
/// </summary>
internal sealed partial class DomainEventDispatcher(IServiceProvider services, ILogger<DomainEventDispatcher> logger)
{
    private static readonly ConcurrentDictionary<(Type, bool), MethodInfo> Methods = new();

    private static readonly MethodInfo TransactionalMethod =
        typeof(DomainEventDispatcher).GetMethod(nameof(DispatchTransactionalTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly MethodInfo AfterCommitMethod =
        typeof(DomainEventDispatcher).GetMethod(nameof(DispatchAfterCommitTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public Task DispatchTransactionalAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken) =>
        DispatchAsync(events, transactional: true, cancellationToken);

    public Task DispatchAfterCommitAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken) =>
        DispatchAsync(events, transactional: false, cancellationToken);

    private async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, bool transactional, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in events)
        {
            var method = Methods.GetOrAdd(
                (domainEvent.GetType(), transactional),
                key => (key.Item2 ? TransactionalMethod : AfterCommitMethod).MakeGenericMethod(key.Item1));
            await (Task)method.Invoke(this, [domainEvent, cancellationToken])!;
        }
    }

    private async Task DispatchTransactionalTypedAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        foreach (var handler in services.GetServices<ITransactionalDomainEventHandler<TEvent>>())
        {
            await handler.HandleAsync(domainEvent, cancellationToken);
        }
    }

    private async Task DispatchAfterCommitTypedAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken)
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

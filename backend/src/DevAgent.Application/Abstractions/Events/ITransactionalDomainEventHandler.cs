using DevAgent.Domain.Common;

namespace DevAgent.Application.Abstractions.Events;

/// <summary>
/// Handles a domain event <b>before commit, inside the same transaction</b> (transactional outbox).
/// It may only add work to the current unit of work, such as enqueueing a notification job, and must not call
/// external services. A failure aborts the whole transaction so the business change and its side work succeed together.
/// </summary>
public interface ITransactionalDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

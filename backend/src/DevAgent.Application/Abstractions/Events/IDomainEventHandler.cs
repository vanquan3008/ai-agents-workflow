using DevAgent.Domain.Common;

namespace DevAgent.Application.Abstractions.Events;

/// <summary>
/// Handles a domain event after the transaction commits. Failures are logged and never roll back the
/// transaction: the database is already the source of truth and clients replay from history.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

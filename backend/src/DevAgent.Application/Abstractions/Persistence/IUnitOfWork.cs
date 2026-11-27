namespace DevAgent.Application.Abstractions.Persistence;

/// <summary>
/// Transaction boundary of a use case. Aggregate changes, queued jobs and audit entries commit together;
/// domain events are dispatched only after a successful commit.
/// Throws <see cref="Common.DuplicateKeyException"/> or <see cref="Common.ConcurrencyConflictException"/>.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

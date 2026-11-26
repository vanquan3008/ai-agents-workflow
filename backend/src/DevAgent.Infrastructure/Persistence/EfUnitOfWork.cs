using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Common;
using DevAgent.Domain.Common;
using DevAgent.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DevAgent.Infrastructure.Persistence;

/// <summary>
/// Commits in three phases:
/// 1) transactional (outbox) handlers add side work to the same DbContext;
/// 2) <c>SaveChanges</c> runs one transaction for the business change, jobs and audit entries;
/// 3) after-commit handlers run (realtime, best effort).
/// PostgreSQL errors are translated into application exceptions so upper layers stay free of EF/Npgsql.
/// </summary>
internal sealed class EfUnitOfWork(DevAgentDbContext context, DomainEventDispatcher dispatcher) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        var aggregates = context.ChangeTracker.Entries<IHasDomainEvents>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToArray();
        var events = aggregates.SelectMany(a => a.DomainEvents).ToArray();

        await dispatcher.DispatchTransactionalAsync(events, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("Dữ liệu đã bị tiến trình khác cập nhật. Hãy tải lại và thử lại.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new DuplicateKeyException($"Vi phạm ràng buộc duy nhất {pg.ConstraintName}.", ex) { ConstraintName = pg.ConstraintName };
        }

        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        await dispatcher.DispatchAfterCommitAsync(events, cancellationToken);
    }
}

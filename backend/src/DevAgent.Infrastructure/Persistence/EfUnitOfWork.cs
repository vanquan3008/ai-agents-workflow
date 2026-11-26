using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Common;
using DevAgent.Domain.Common;
using DevAgent.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DevAgent.Infrastructure.Persistence;

/// <summary>
/// Commit một giao dịch rồi phát domain event. Dịch lỗi PostgreSQL sang ngoại lệ của Application
/// để tầng trên không phụ thuộc EF/Npgsql.
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

        await dispatcher.DispatchAsync(events, cancellationToken);
    }
}

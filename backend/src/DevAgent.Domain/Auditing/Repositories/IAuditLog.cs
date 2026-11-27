using DevAgent.Domain.Auditing;

namespace DevAgent.Domain.Auditing.Repositories;

/// <summary>Append-only audit sink. Entries are saved in the same transaction as the use case.</summary>
public interface IAuditLog
{
    void Add(AuditEntry entry);
}

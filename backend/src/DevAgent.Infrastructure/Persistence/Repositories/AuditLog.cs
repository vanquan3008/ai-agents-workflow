using DevAgent.Domain.Auditing;
using DevAgent.Domain.Auditing.Repositories;

namespace DevAgent.Infrastructure.Persistence.Repositories;

internal sealed class AuditLog(DevAgentDbContext context) : IAuditLog
{
    public void Add(AuditEntry entry) => context.AuditEntries.Add(entry);
}

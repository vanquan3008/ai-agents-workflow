using DevAgent.Domain.Approvals;
using DevAgent.Domain.Approvals.Repositories;
using DevAgent.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace DevAgent.Infrastructure.Persistence.Repositories;

internal sealed class ApprovalRepository(DevAgentDbContext context) : IApprovalRepository
{
    public Task<Approval?> GetByIdAsync(ApprovalId id, CancellationToken cancellationToken) =>
        context.Approvals.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Approval>> ListOpenByTaskAsync(TaskId taskId, CancellationToken cancellationToken) =>
        await context.Approvals
            .Where(a => a.TaskId == taskId && (a.Status == ApprovalStatus.Pending || a.Status == ApprovalStatus.Approved))
            .ToListAsync(cancellationToken);

    public void Add(Approval approval) => context.Approvals.Add(approval);
}

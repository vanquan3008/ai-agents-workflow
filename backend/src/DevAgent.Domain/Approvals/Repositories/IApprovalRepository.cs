using DevAgent.Domain.Approvals;
using DevAgent.Domain.Common;

namespace DevAgent.Domain.Approvals.Repositories;

public interface IApprovalRepository
{
    Task<Approval?> GetByIdAsync(ApprovalId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Approval>> ListOpenByTaskAsync(TaskId taskId, CancellationToken cancellationToken);

    void Add(Approval approval);
}

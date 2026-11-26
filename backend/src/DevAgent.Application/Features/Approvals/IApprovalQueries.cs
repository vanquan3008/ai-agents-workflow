using DevAgent.Application.Common;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Common;

namespace DevAgent.Application.Features.Approvals;

public interface IApprovalQueries
{
    Task<PagedResult<ApprovalDto>> ListAsync(
        IReadOnlyCollection<ProjectId> projectIds,
        ApprovalStatus? status,
        TaskId? taskId,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<ApprovalDto?> GetAsync(ApprovalId approvalId, CancellationToken cancellationToken);
}

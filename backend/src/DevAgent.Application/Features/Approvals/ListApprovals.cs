using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Common;

namespace DevAgent.Application.Features.Approvals;

public sealed record ListApprovalsQuery(Guid? ProjectId, Guid? TaskId, string? Status, int? Page, int? PageSize);

internal sealed class ListApprovalsHandler(ProjectAccess access, IApprovalQueries approvalQueries)
    : IQueryHandler<ListApprovalsQuery, PagedResult<ApprovalDto>>
{
    public async Task<Result<PagedResult<ApprovalDto>>> HandleAsync(ListApprovalsQuery query, CancellationToken cancellationToken)
    {
        ApprovalStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!ApiNames.TryParse<ApprovalStatus>(query.Status, out var parsed))
            {
                return Error.Validation("approval.status_invalid", $"Trạng thái không hợp lệ: {query.Status}.");
            }

            status = parsed;
        }

        var projectIds = await access.ResolveViewableProjectsAsync(query.ProjectId, cancellationToken);
        if (projectIds.IsFailure)
        {
            return projectIds.Error!;
        }

        return await approvalQueries.ListAsync(
            projectIds.Value,
            status,
            query.TaskId is { } t ? new TaskId(t) : null,
            new PageRequest(query.Page, query.PageSize),
            cancellationToken);
    }
}

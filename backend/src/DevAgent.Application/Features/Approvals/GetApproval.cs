using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Approvals;

public sealed record GetApprovalQuery(Guid ApprovalId);

internal sealed class GetApprovalHandler(ProjectAccess access, IApprovalQueries approvalQueries) : IQueryHandler<GetApprovalQuery, ApprovalDto>
{
    public async Task<Result<ApprovalDto>> HandleAsync(GetApprovalQuery query, CancellationToken cancellationToken)
    {
        var approval = await approvalQueries.GetAsync(new ApprovalId(query.ApprovalId), cancellationToken);
        if (approval is null)
        {
            return Error.NotFound("approval", query.ApprovalId);
        }

        var user = await access.RequireAsync(new ProjectId(approval.ProjectId), ProjectPermission.ViewTasks, "approval", approval.Id.ToString(), cancellationToken);
        return user.IsFailure ? Result<ApprovalDto>.Failure(user.Error!) : Result<ApprovalDto>.Success(approval);
    }
}

using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Application.Features.Departments.Shared;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;

namespace DevAgent.Application.Features.Departments.Queries;

public sealed record GetDepartmentDraftQuery(Guid ProjectId, Guid DepartmentId);

internal sealed class GetDepartmentDraftHandler(DepartmentAccess access, DepartmentDraftProjection projection)
    : IQueryHandler<GetDepartmentDraftQuery, DepartmentDraftDto>
{
    public async Task<Result<DepartmentDraftDto>> HandleAsync(GetDepartmentDraftQuery query, CancellationToken cancellationToken)
    {
        var context = await access.RequireManagedAsync(new ProjectId(query.ProjectId), new ItDepartmentId(query.DepartmentId), cancellationToken);
        if (context.IsFailure)
        {
            return context.Error!;
        }

        return await projection.BuildAsync(context.Value.Department, cancellationToken);
    }
}

using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Application.Features.Departments.Shared;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Departments.Queries;

public sealed record GetPublishedDepartmentQuery(Guid ProjectId, Guid DepartmentId);

/// <summary>The published office both the 2D and 3D views render. Readers keep seeing it until a new publication commits.</summary>
internal sealed class GetPublishedDepartmentHandler(DepartmentAccess access, IDepartmentQueries queries)
    : IQueryHandler<GetPublishedDepartmentQuery, PublishedDepartmentDto>
{
    public async Task<Result<PublishedDepartmentDto>> HandleAsync(GetPublishedDepartmentQuery query, CancellationToken cancellationToken)
    {
        var context = await access.RequirePublishedAsync(new ProjectId(query.ProjectId), new ItDepartmentId(query.DepartmentId), cancellationToken);
        if (context.IsFailure)
        {
            return context.Error!;
        }

        var (authorization, pointer, revision) = context.Value;
        var repositories = await queries.ListProjectRepositoriesAsync(pointer.ProjectId, cancellationToken);
        return new PublishedDepartmentDto(
            pointer.Id.Value,
            pointer.ProjectId.Value,
            pointer.Slug,
            revision.Version,
            revision.Name,
            ApiNames.ToApi(revision.Theme),
            revision.Rooms,
            DepartmentMapping.SelectRepositories(revision.RepositoryIds, repositories),
            revision.PublishedAt,
            authorization.Can(ProjectPermission.ManageDepartments));
    }
}

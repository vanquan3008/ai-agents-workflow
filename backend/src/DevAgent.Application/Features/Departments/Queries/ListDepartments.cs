using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Application.Features.Departments.Shared;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Departments.Queries;

/// <param name="IncludeArchived">Managers only: also list archived departments.</param>
public sealed record ListDepartmentsQuery(Guid ProjectId, bool IncludeArchived);

/// <summary>Viewers see published, active departments; managers also see unpublished drafts.</summary>
internal sealed class ListDepartmentsHandler(DepartmentAccess access, IDepartmentQueries queries)
    : IQueryHandler<ListDepartmentsQuery, DepartmentDirectoryDto>
{
    public async Task<Result<DepartmentDirectoryDto>> HandleAsync(ListDepartmentsQuery query, CancellationToken cancellationToken)
    {
        var projectId = new ProjectId(query.ProjectId);
        var authorization = await access.AuthorizeProjectAsync(projectId, ProjectPermission.ViewTasks, cancellationToken);
        if (authorization.IsFailure)
        {
            return authorization.Error!;
        }

        var canManage = authorization.Value.Can(ProjectPermission.ManageDepartments);
        var items = await queries.ListAsync(projectId, includeUnpublished: canManage, includeArchived: canManage && query.IncludeArchived, cancellationToken);
        return new DepartmentDirectoryDto(items, canManage);
    }
}

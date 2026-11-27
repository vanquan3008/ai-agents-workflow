using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Application.Features.Departments.Shared;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Departments.Queries;

public sealed record ListDepartmentRevisionsQuery(Guid ProjectId, Guid DepartmentId, int? Limit);

/// <summary>Publication history, newest first. Archived departments keep their history visible to managers.</summary>
internal sealed class ListDepartmentRevisionsHandler(DepartmentAccess access, IDepartmentQueries queries)
    : IQueryHandler<ListDepartmentRevisionsQuery, IReadOnlyList<DepartmentRevisionDto>>
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    public async Task<Result<IReadOnlyList<DepartmentRevisionDto>>> HandleAsync(ListDepartmentRevisionsQuery query, CancellationToken cancellationToken)
    {
        var projectId = new ProjectId(query.ProjectId);
        var departmentId = new ItDepartmentId(query.DepartmentId);
        var authorization = await access.AuthorizeProjectAsync(projectId, ProjectPermission.ViewTasks, cancellationToken);
        if (authorization.IsFailure)
        {
            return authorization.Error!;
        }

        var pointer = await queries.GetPointerAsync(projectId, departmentId, cancellationToken);
        var visible = pointer is not null
            && (authorization.Value.Can(ProjectPermission.ManageDepartments) || (!pointer.IsArchived && pointer.PublishedVersion is not null));
        if (!visible)
        {
            return Error.NotFound("department", query.DepartmentId);
        }

        var items = await queries.ListRevisionsAsync(
            projectId,
            departmentId,
            pointer!.PublishedVersion,
            Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit),
            cancellationToken);
        return Result<IReadOnlyList<DepartmentRevisionDto>>.Success(items);
    }
}

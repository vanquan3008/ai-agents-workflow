using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;
using DevAgent.Domain.Departments.Layout;

namespace DevAgent.Application.Features.Departments.Contracts;

/// <summary>The mutable head of a department, read without loading the aggregate.</summary>
public sealed record DepartmentPointer(ItDepartmentId Id, ProjectId ProjectId, string Slug, int? PublishedVersion, bool IsArchived);

/// <summary>An immutable published revision. Safe to cache by (department, version).</summary>
public sealed record DepartmentRevisionSnapshot(
    ItDepartmentId DepartmentId,
    int Version,
    string Name,
    DepartmentTheme Theme,
    IReadOnlyList<DepartmentRoomDto> Rooms,
    IReadOnlyList<RepositoryId> RepositoryIds,
    DateTimeOffset PublishedAt);

/// <summary>Read-side projections for the Departments module. Every method is scoped by project.</summary>
public interface IDepartmentQueries
{
    Task<IReadOnlyList<DepartmentSummaryDto>> ListAsync(ProjectId projectId, bool includeUnpublished, bool includeArchived, CancellationToken cancellationToken);

    Task<DepartmentPointer?> GetPointerAsync(ProjectId projectId, ItDepartmentId departmentId, CancellationToken cancellationToken);

    Task<DepartmentRevisionSnapshot?> GetRevisionSnapshotAsync(ProjectId projectId, ItDepartmentId departmentId, int version, CancellationToken cancellationToken);

    Task<IReadOnlyList<DepartmentRevisionDto>> ListRevisionsAsync(
        ProjectId projectId,
        ItDepartmentId departmentId,
        int? currentVersion,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>The project's repositories, ordered by full name. Projects hold few repositories, so callers filter in memory.</summary>
    Task<IReadOnlyList<DepartmentRepositoryDto>> ListProjectRepositoriesAsync(ProjectId projectId, CancellationToken cancellationToken);
}

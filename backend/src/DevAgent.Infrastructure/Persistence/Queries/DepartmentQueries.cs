using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Application.Features.Departments.Shared;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;
using Microsoft.EntityFrameworkCore;

namespace DevAgent.Infrastructure.Persistence.Queries;

/// <summary>Read side of the Departments module: no-tracking projections, every query filtered by project first.</summary>
internal sealed class DepartmentQueries(DevAgentDbContext context, DepartmentRevisionCache cache) : IDepartmentQueries
{
    public async Task<IReadOnlyList<DepartmentSummaryDto>> ListAsync(
        ProjectId projectId,
        bool includeUnpublished,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        var query = context.Departments.AsNoTracking().Where(d => d.ProjectId == projectId);
        if (!includeArchived)
        {
            query = query.Where(d => d.ArchivedAt == null);
        }

        if (!includeUnpublished)
        {
            query = query.Where(d => d.PublishedVersion != null);
        }

        // One round trip: each row carries the counts of its published revision through correlated subqueries.
        var rows = await query
            .OrderBy(d => d.Name)
            .Select(d => new
            {
                d.Id,
                d.Slug,
                d.Name,
                d.PublishedVersion,
                HasDraft = d.Draft != null,
                d.ArchivedAt,
                d.UpdatedAt,
                Revision = context.DepartmentRevisions
                    .Where(r => r.ProjectId == d.ProjectId && r.DepartmentId == d.Id && r.Version == d.PublishedVersion)
                    .Select(r => new { r.Theme, r.PublishedAt, RoomCount = r.Rooms.Count, RepositoryCount = r.Repositories.Count })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new DepartmentSummaryDto(
            r.Id.Value,
            projectId.Value,
            r.Slug,
            r.Name,
            r.Revision is null ? null : ApiNames.ToApi(r.Revision.Theme),
            r.PublishedVersion,
            r.Revision?.PublishedAt,
            r.HasDraft,
            r.Revision?.RepositoryCount ?? 0,
            r.Revision?.RoomCount ?? 0,
            r.ArchivedAt is not null,
            r.UpdatedAt)).ToArray();
    }

    public Task<DepartmentPointer?> GetPointerAsync(ProjectId projectId, ItDepartmentId departmentId, CancellationToken cancellationToken) =>
        context.Departments.AsNoTracking()
            .Where(d => d.ProjectId == projectId && d.Id == departmentId)
            .Select(d => new DepartmentPointer(d.Id, d.ProjectId, d.Slug, d.PublishedVersion, d.ArchivedAt != null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<DepartmentRevisionSnapshot?> GetRevisionSnapshotAsync(
        ProjectId projectId,
        ItDepartmentId departmentId,
        int version,
        CancellationToken cancellationToken)
    {
        if (cache.TryGet(projectId, departmentId, version, out var cached))
        {
            return cached;
        }

        var revision = await context.DepartmentRevisions.AsNoTracking().AsSplitQuery()
            .FirstOrDefaultAsync(r => r.ProjectId == projectId && r.DepartmentId == departmentId && r.Version == version, cancellationToken);
        if (revision is null)
        {
            return null;
        }

        var snapshot = new DepartmentRevisionSnapshot(
            revision.DepartmentId,
            revision.Version,
            revision.Name,
            revision.Theme,
            revision.Rooms.OrderBy(r => r.SortOrder).Select(DepartmentMapping.ToDto).ToArray(),
            revision.Repositories.Select(r => r.RepositoryId).ToArray(),
            revision.PublishedAt);
        cache.Set(projectId, snapshot);
        return snapshot;
    }

    public async Task<IReadOnlyList<DepartmentRevisionDto>> ListRevisionsAsync(
        ProjectId projectId,
        ItDepartmentId departmentId,
        int? currentVersion,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await context.DepartmentRevisions.AsNoTracking()
            .Where(r => r.ProjectId == projectId && r.DepartmentId == departmentId)
            .OrderByDescending(r => r.Version)
            .Take(limit)
            .Select(r => new
            {
                r.Version,
                r.Name,
                r.Theme,
                RoomCount = r.Rooms.Count,
                RepositoryCount = r.Repositories.Count,
                r.PublishedBy,
                r.PublishedAt,
                PublishedByName = context.Users.Where(u => u.Id == r.PublishedBy).Select(u => u.DisplayName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new DepartmentRevisionDto(
            r.Version,
            r.Name,
            ApiNames.ToApi(r.Theme),
            r.RoomCount,
            r.RepositoryCount,
            r.PublishedBy.Value,
            r.PublishedByName ?? string.Empty,
            r.PublishedAt,
            r.Version == currentVersion)).ToArray();
    }

    public async Task<IReadOnlyList<DepartmentRepositoryDto>> ListProjectRepositoriesAsync(ProjectId projectId, CancellationToken cancellationToken)
    {
        var rows = await context.Repositories.AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.Owner).ThenBy(r => r.Name)
            .Select(r => new { r.Id, r.Owner, r.Name, r.IsActive })
            .ToListAsync(cancellationToken);
        return rows.Select(r => new DepartmentRepositoryDto(r.Id.Value, $"{r.Owner}/{r.Name}", r.IsActive)).ToArray();
    }
}

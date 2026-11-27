using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;
using DevAgent.Domain.Departments.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DevAgent.Infrastructure.Persistence.Repositories;

internal sealed class ItDepartmentRevisionRepository(DevAgentDbContext context) : IItDepartmentRevisionRepository
{
    // Revisions are append-only, so reads never need change tracking.
    public Task<ItDepartmentRevision?> GetAsync(ProjectId projectId, ItDepartmentId departmentId, int version, CancellationToken cancellationToken) =>
        context.DepartmentRevisions.AsNoTracking().AsSplitQuery()
            .FirstOrDefaultAsync(r => r.ProjectId == projectId && r.DepartmentId == departmentId && r.Version == version, cancellationToken);

    public void Add(ItDepartmentRevision revision) => context.DepartmentRevisions.Add(revision);
}

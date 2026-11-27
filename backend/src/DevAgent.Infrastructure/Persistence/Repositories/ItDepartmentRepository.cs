using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;
using DevAgent.Domain.Departments.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DevAgent.Infrastructure.Persistence.Repositories;

internal sealed class ItDepartmentRepository(DevAgentDbContext context) : IItDepartmentRepository
{
    public Task<ItDepartment?> GetAsync(ProjectId projectId, ItDepartmentId departmentId, CancellationToken cancellationToken) =>
        context.Departments.FirstOrDefaultAsync(d => d.ProjectId == projectId && d.Id == departmentId, cancellationToken);

    public Task<bool> SlugExistsAsync(ProjectId projectId, string slug, CancellationToken cancellationToken) =>
        context.Departments.AnyAsync(d => d.ProjectId == projectId && d.Slug == slug, cancellationToken);

    public void Add(ItDepartment department) => context.Departments.Add(department);
}

using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;

namespace DevAgent.Domain.Departments.Repositories;

public interface IItDepartmentRepository
{
    /// <summary>Loads the department for change tracking, scoped to its project so ids from other projects never match.</summary>
    Task<ItDepartment?> GetAsync(ProjectId projectId, ItDepartmentId departmentId, CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(ProjectId projectId, string slug, CancellationToken cancellationToken);

    void Add(ItDepartment department);
}

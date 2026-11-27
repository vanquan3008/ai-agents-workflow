using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;

namespace DevAgent.Domain.Departments.Repositories;

/// <summary>Append-only store of published department snapshots.</summary>
public interface IItDepartmentRevisionRepository
{
    Task<ItDepartmentRevision?> GetAsync(ProjectId projectId, ItDepartmentId departmentId, int version, CancellationToken cancellationToken);

    void Add(ItDepartmentRevision revision);
}

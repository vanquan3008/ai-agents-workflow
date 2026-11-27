using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;
using DevAgent.Domain.Departments.Repositories;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Departments.Shared;

/// <summary>The caller's authorization plus the published revision a department currently points to.</summary>
internal sealed record PublishedDepartmentContext(ProjectAuthorization Authorization, DepartmentPointer Pointer, DepartmentRevisionSnapshot Revision);

/// <summary>The caller's authorization plus the tracked aggregate, for manager commands.</summary>
internal sealed record ManagedDepartmentContext(ProjectAuthorization Authorization, ItDepartment Department);

/// <summary>
/// Shared authorization and loading for department use cases. Checks the project permission first, so callers
/// without access learn nothing about which departments exist, then loads within the project scope only.
/// </summary>
internal sealed class DepartmentAccess(ProjectAccess access, IDepartmentQueries queries, IItDepartmentRepository departments)
{
    private const string ResourceType = "department";

    public Task<Result<ProjectAuthorization>> AuthorizeProjectAsync(ProjectId projectId, ProjectPermission permission, CancellationToken cancellationToken) =>
        access.AuthorizeAsync(projectId, permission, ResourceType, projectId.ToString(), cancellationToken);

    /// <summary>A published, active department for anyone who can view the project's tasks.</summary>
    public async Task<Result<PublishedDepartmentContext>> RequirePublishedAsync(ProjectId projectId, ItDepartmentId departmentId, CancellationToken cancellationToken)
    {
        var authorization = await access.AuthorizeAsync(projectId, ProjectPermission.ViewTasks, ResourceType, departmentId.ToString(), cancellationToken);
        if (authorization.IsFailure)
        {
            return authorization.Error!;
        }

        var pointer = await queries.GetPointerAsync(projectId, departmentId, cancellationToken);
        if (pointer is not { IsArchived: false, PublishedVersion: { } version })
        {
            return Error.NotFound("department", departmentId.Value);
        }

        var revision = await queries.GetRevisionSnapshotAsync(projectId, departmentId, version, cancellationToken);
        return revision is null
            ? Error.NotFound("department", departmentId.Value)
            : new PublishedDepartmentContext(authorization.Value, pointer, revision);
    }

    /// <summary>The department aggregate, tracked for changes, for callers holding <see cref="ProjectPermission.ManageDepartments"/>.</summary>
    public async Task<Result<ManagedDepartmentContext>> RequireManagedAsync(ProjectId projectId, ItDepartmentId departmentId, CancellationToken cancellationToken)
    {
        var authorization = await access.AuthorizeAsync(projectId, ProjectPermission.ManageDepartments, ResourceType, departmentId.ToString(), cancellationToken);
        if (authorization.IsFailure)
        {
            return authorization.Error!;
        }

        var department = await departments.GetAsync(projectId, departmentId, cancellationToken);
        return department is null
            ? Error.NotFound("department", departmentId.Value)
            : new ManagedDepartmentContext(authorization.Value, department);
    }

    /// <summary>
    /// Answers stale edits and archived departments with 409 before the aggregate is touched,
    /// so the editor can keep its local draft and offer a reload.
    /// </summary>
    public static Error? CheckEditable(ItDepartment department, int expectedEditVersion)
    {
        if (department.IsArchived)
        {
            return Error.Conflict("department.archived", "Phòng ban đã được lưu trữ và không thể chỉnh sửa.");
        }

        return expectedEditVersion != department.EditVersion
            ? Error.Conflict(
                "department.edit_conflict",
                $"Phòng ban đã được cập nhật (phiên bản chỉnh sửa {department.EditVersion}). Tải lại rồi áp dụng thay đổi của bạn.")
            : null;
    }
}

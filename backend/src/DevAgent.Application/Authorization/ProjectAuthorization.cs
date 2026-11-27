using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Authorization;

/// <summary>
/// The outcome of one successful project permission check. Lets a use case ask about further permissions
/// (for example whether to show editing controls) without reloading the project or auditing a denial.
/// </summary>
public sealed record ProjectAuthorization(UserId UserId, ProjectId ProjectId, bool IsSuperAdmin, ProjectRole? Role)
{
    public bool Can(ProjectPermission permission) =>
        IsSuperAdmin || (Role is { } role && ProjectRolePermissions.Grants(role, permission));
}

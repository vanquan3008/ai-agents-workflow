namespace DevAgent.Domain.Projects;

public enum ProjectRole
{
    Viewer = 1,
    Requester = 2,
    Approver = 3,
    Owner = 4,
}

public enum ProjectPermission
{
    ViewTasks = 1,
    CreateTasks = 2,
    CancelTasks = 3,
    ApproveChanges = 4,
    ManageRepositories = 5,
    ManageMembers = 6,
}

/// <summary>Bảng quyền theo vai trò. Đây là nguồn sự thật duy nhất cho phân quyền theo project.</summary>
public static class ProjectRolePermissions
{
    private static readonly Dictionary<ProjectRole, ProjectPermission[]> Map = new()
    {
        [ProjectRole.Viewer] = [ProjectPermission.ViewTasks],
        [ProjectRole.Requester] = [ProjectPermission.ViewTasks, ProjectPermission.CreateTasks, ProjectPermission.CancelTasks],
        [ProjectRole.Approver] =
        [
            ProjectPermission.ViewTasks, ProjectPermission.CreateTasks, ProjectPermission.CancelTasks,
            ProjectPermission.ApproveChanges,
        ],
        [ProjectRole.Owner] = Enum.GetValues<ProjectPermission>(),
    };

    public static bool Grants(ProjectRole role, ProjectPermission permission) =>
        Map.TryGetValue(role, out var permissions) && permissions.Contains(permission);
}

namespace DevAgent.Domain.Projects;

/// <summary>Role-to-permission matrix; the single source of truth for project authorization.</summary>
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

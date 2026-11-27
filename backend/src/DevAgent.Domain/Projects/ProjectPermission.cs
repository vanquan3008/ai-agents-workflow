namespace DevAgent.Domain.Projects;

public enum ProjectPermission
{
    ViewTasks = 1,
    CreateTasks = 2,
    CancelTasks = 3,
    ApproveChanges = 4,
    ManageRepositories = 5,
    ManageMembers = 6,

    /// <summary>Create, edit, publish, restore and archive IT department offices. Owner only.</summary>
    ManageDepartments = 7,
}

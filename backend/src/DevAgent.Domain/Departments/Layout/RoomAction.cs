namespace DevAgent.Domain.Departments.Layout;

/// <summary>Approved in-app destinations for informational rooms. Rooms never carry URLs or executable content.</summary>
public enum RoomAction
{
    OpenTasks = 1,
    OpenApprovals = 2,
    OpenProject = 3,
    OpenWorkflow = 4,
}

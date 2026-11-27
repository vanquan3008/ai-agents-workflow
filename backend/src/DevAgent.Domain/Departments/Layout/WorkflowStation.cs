namespace DevAgent.Domain.Departments.Layout;

/// <summary>
/// The six responsibilities of the fixed task pipeline that a published office must place in rooms.
/// Display only: execution order stays owned by the workflow and the Worker.
/// </summary>
public enum WorkflowStation
{
    Planning = 1,
    Development = 2,
    Testing = 3,
    Review = 4,
    Approval = 5,
    Publishing = 6,
}

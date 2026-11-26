namespace DevAgent.Domain.Executions;

/// <summary>Steps of the sequential orchestration pipeline.</summary>
public enum AgentStep
{
    Planner = 1,
    Coding = 2,
    Testing = 3,
    Reviewer = 4,
    Publisher = 5,
}

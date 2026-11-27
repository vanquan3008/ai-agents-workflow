using DevAgent.Domain.Common;

namespace DevAgent.Application.AgentRuntime.Steps;

internal static class StepActors
{
    public static readonly Actor Orchestrator = Actor.ForAgent("orchestrator");
    public static readonly Actor Planner = Actor.ForAgent("planner");
}

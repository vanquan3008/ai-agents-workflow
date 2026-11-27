using DevAgent.Application.Abstractions.Ai;
using DevAgent.Application.Abstractions.Integrations;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Tasks;

namespace DevAgent.Application.AgentRuntime.Planner;

public sealed record PlannerInput(AgentTask Task, CodeRepository Repository, IReadOnlyList<RepositoryFile> Files);

public sealed record PlannerOutput(
    bool NeedsClarification,
    string? ClarificationQuestion,
    string Summary,
    IReadOnlyList<string> Steps,
    IReadOnlyList<string> AcceptanceCriteria,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> AffectedPaths);

public sealed record PlannerResult(PlannerOutput? Output, ModelResponse Response, string? ParseError);

/// <summary>
/// Two separate phases so the orchestrator can check the budget in between: build the request (known input size),
/// then call the model with the granted output cap.
/// </summary>
public interface IPlannerAgent
{
    /// <summary>The system default model for the Planner step.</summary>
    string DefaultModel();

    ModelRequest BuildRequest(PlannerInput input, string model, int maxOutputTokens);

    Task<PlannerResult> ExecuteAsync(ModelRequest request, CancellationToken cancellationToken);
}

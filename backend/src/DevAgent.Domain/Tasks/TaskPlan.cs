using DevAgent.Domain.Common;
using DevAgent.Domain.Policies;

namespace DevAgent.Domain.Tasks;

/// <summary>
/// One plan version produced by the Planner. Approvals are bound to <see cref="Version"/>,
/// so a changed plan must be approved again.
/// </summary>
public sealed class TaskPlan : Entity<TaskPlanId>
{
    private TaskPlan()
    {
        Summary = string.Empty;
        Steps = [];
        AcceptanceCriteria = [];
        Risks = [];
        AffectedPaths = [];
    }

    internal TaskPlan(
        TaskPlanId id,
        int version,
        string summary,
        IReadOnlyList<string> steps,
        IReadOnlyList<string> acceptanceCriteria,
        IReadOnlyList<string> risks,
        IReadOnlyList<string> affectedPaths,
        PolicyDecision policyDecision,
        DateTimeOffset createdAt)
        : base(id)
    {
        Version = version;
        Summary = summary;
        Steps = [.. steps];
        AcceptanceCriteria = [.. acceptanceCriteria];
        Risks = [.. risks];
        AffectedPaths = [.. affectedPaths];
        PolicyOutcome = policyDecision.Outcome;
        SensitiveAreas = policyDecision.Areas;
        PolicyVersion = policyDecision.PolicyVersion;
        PolicyReasons = [.. policyDecision.Reasons];
        CreatedAt = createdAt;
    }

    public int Version { get; private set; }

    public string Summary { get; private set; }

    public List<string> Steps { get; private set; }

    public List<string> AcceptanceCriteria { get; private set; }

    public List<string> Risks { get; private set; }

    public List<string> AffectedPaths { get; private set; }

    public PolicyOutcome PolicyOutcome { get; private set; }

    public SensitiveArea SensitiveAreas { get; private set; }

    public int PolicyVersion { get; private set; }

    public List<string> PolicyReasons { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }
}

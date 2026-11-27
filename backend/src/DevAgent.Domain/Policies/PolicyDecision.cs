namespace DevAgent.Domain.Policies;

public sealed record PolicyDecision(PolicyOutcome Outcome, SensitiveArea Areas, int PolicyVersion, IReadOnlyList<string> Reasons)
{
    public bool CanProceedAutomatically => Outcome == PolicyOutcome.AutoApproved;
}

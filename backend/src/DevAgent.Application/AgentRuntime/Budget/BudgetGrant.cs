namespace DevAgent.Application.AgentRuntime.Budget;

/// <summary>Outcome of reserving budget for the next model call.</summary>
public sealed record BudgetGrant(bool Allowed, int MaxOutputTokens, string? Reason)
{
    public static BudgetGrant Deny(string reason) => new(false, 0, reason);
}

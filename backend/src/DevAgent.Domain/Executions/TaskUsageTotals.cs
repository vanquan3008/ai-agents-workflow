namespace DevAgent.Domain.Executions;

/// <summary>Accumulated usage across every execution of one task, used for budget checks.</summary>
public sealed record TaskUsageTotals(TokenUsage Usage, int ModelCalls)
{
    public static TaskUsageTotals Zero { get; } = new(TokenUsage.Zero, 0);
}

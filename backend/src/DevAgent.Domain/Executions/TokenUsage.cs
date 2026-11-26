namespace DevAgent.Domain.Executions;

/// <summary>Token counts and estimated cost. Cost uses decimal USD to avoid rounding drift.</summary>
public sealed record TokenUsage(long InputTokens, long OutputTokens, long CacheReadTokens, decimal EstimatedCostUsd)
{
    public static TokenUsage Zero { get; } = new(0, 0, 0, 0m);

    public long TotalTokens => InputTokens + OutputTokens;

    public TokenUsage Add(TokenUsage other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new(
            InputTokens + other.InputTokens,
            OutputTokens + other.OutputTokens,
            CacheReadTokens + other.CacheReadTokens,
            EstimatedCostUsd + other.EstimatedCostUsd);
    }
}

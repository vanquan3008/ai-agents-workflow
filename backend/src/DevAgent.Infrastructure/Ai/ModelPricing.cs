using DevAgent.Infrastructure.Options;

namespace DevAgent.Infrastructure.Ai;

internal static class ModelPricing
{
    private const decimal Million = 1_000_000m;

    /// <summary>Estimates cost from configured prices. Unpriced models count as zero; the validator rejects them outside fake mode.</summary>
    public static decimal Estimate(AiOptions options, string model, long inputTokens, long outputTokens, long cacheReadTokens)
    {
        if (!options.Pricing.TryGetValue(model, out var price))
        {
            return 0m;
        }

        return (inputTokens * price.InputPerMillion
                + outputTokens * price.OutputPerMillion
                + cacheReadTokens * price.CacheReadPerMillion) / Million;
    }
}

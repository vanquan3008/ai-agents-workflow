namespace DevAgent.Application.Abstractions.Ai;

public enum ModelRole
{
    User = 1,
    Assistant = 2,
}

public sealed record ModelMessage(ModelRole Role, string Content);

/// <summary>Provider-neutral model call. Adapters map it to a vendor SDK; the provider is selected by <c>Ai:Provider</c>.</summary>
/// <param name="Purpose">Call purpose (planner/coding/reviewer), used for tracing and default model selection.</param>
/// <param name="SystemPrompt">System instructions. Repository or issue content is never placed here.</param>
/// <param name="Messages">Conversation; untrusted content is wrapped and marked as data.</param>
/// <param name="MaxOutputTokens">Output cap for this call.</param>
/// <param name="ModelOverride">Explicit model id; null uses the default for <paramref name="Purpose"/>.</param>
public sealed record ModelRequest(
    string Purpose,
    string SystemPrompt,
    IReadOnlyList<ModelMessage> Messages,
    int MaxOutputTokens,
    string? ModelOverride = null);

public enum ModelStopReason
{
    Completed = 1,
    MaxTokens = 2,
    Refusal = 3,
    Other = 4,
}

public sealed record ModelUsage(long InputTokens, long OutputTokens, long CacheReadTokens, decimal EstimatedCostUsd);

public sealed record ModelResponse(string Text, ModelStopReason StopReason, string? StopDetail, ModelUsage Usage, string Model);

/// <summary>Price per million tokens in USD, used to cap output against the remaining budget.</summary>
public sealed record ModelPrice(decimal InputPerMillion, decimal OutputPerMillion, decimal CacheReadPerMillion)
{
    public static ModelPrice Free { get; } = new(0m, 0m, 0m);
}

public interface IModelClient
{
    string ProviderName { get; }

    /// <summary>The model that will serve a purpose, recorded on the execution before the call.</summary>
    string ResolveModel(string purpose);

    /// <summary>Null when unpriced; callers then refuse the call because the budget cannot be guaranteed.</summary>
    ModelPrice? GetPrice(string model);

    Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken);
}

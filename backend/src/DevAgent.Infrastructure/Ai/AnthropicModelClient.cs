using Anthropic;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using DevAgent.Application.Abstractions.Ai;
using DevAgent.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace DevAgent.Infrastructure.Ai;

/// <summary>
/// Adapter cho Claude qua SDK chính thức của Anthropic. Chỉ lớp này biết SDK; Application dùng <see cref="IModelClient"/>.
/// API key nằm trong client, không bao giờ đi vào prompt hay log.
/// </summary>
internal sealed class AnthropicModelClient : IModelClient, IDisposable
{
    private readonly AnthropicClient _client;
    private readonly AiOptions _options;

    public AnthropicModelClient(IOptions<AiOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        var timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);
        _client = string.IsNullOrWhiteSpace(_options.Anthropic.BaseUrl)
            ? new AnthropicClient { ApiKey = _options.Anthropic.ApiKey, MaxRetries = _options.MaxRetries, Timeout = timeout }
            : new AnthropicClient { ApiKey = _options.Anthropic.ApiKey, MaxRetries = _options.MaxRetries, Timeout = timeout, BaseUrl = _options.Anthropic.BaseUrl };
    }

    public string ProviderName => "anthropic";

    public string ResolveModel(string purpose) => _options.ModelFor(purpose);

    public ModelPrice? GetPrice(string model) =>
        _options.Pricing.TryGetValue(model, out var p) ? new ModelPrice(p.InputPerMillion, p.OutputPerMillion, p.CacheReadPerMillion) : null;

    public async Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var model = request.ModelOverride ?? ResolveModel(request.Purpose);

        var parameters = new MessageCreateParams
        {
            Model = model,
            MaxTokens = request.MaxOutputTokens,
            System = request.SystemPrompt,
            Messages = [.. request.Messages.Select(ToParam)],
            OutputConfig = new BetaOutputConfig { Effort = ParseEffort(_options.Effort) },
        };

        if (_options.Anthropic.EnableServerSideFallback)
        {
            // Khi bị từ chối (refusal), server tự định tuyến sang model dự phòng phù hợp trong cùng lần gọi.
            parameters = parameters with
            {
                Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
                Fallbacks = new Default(),
            };
        }

        var response = await _client.Beta.Messages.Create(parameters, cancellationToken);

        var text = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
        var stopReason = MapStopReason(response.StopReason?.ToString());
        var stopDetail = stopReason == ModelStopReason.Refusal && response.StopDetails is { } details
            ? $"{details.Category}: {details.Explanation}"
            : null;

        var usage = response.Usage;
        var input = usage.InputTokens;
        var output = usage.OutputTokens;
        var cacheRead = usage.CacheReadInputTokens ?? 0;
        var cost = ModelPricing.Estimate(_options, model, input, output, cacheRead);

        return new ModelResponse(text, stopReason, stopDetail, new ModelUsage(input, output, cacheRead, cost), model);
    }

    public void Dispose() => _client.Dispose();

    private static BetaMessageParam ToParam(ModelMessage message) => message.Role switch
    {
        ModelRole.User => new BetaUserMessageParam { Content = message.Content },
        ModelRole.Assistant => new BetaAssistantMessageParam { Content = message.Content },
        _ => throw new ArgumentOutOfRangeException(nameof(message), message.Role, "Vai trò không hỗ trợ."),
    };

    private static Effort ParseEffort(string effort) => effort switch
    {
        "low" => Effort.Low,
        "medium" => Effort.Medium,
        "xhigh" => Effort.Xhigh,
        "max" => Effort.Max,
        _ => Effort.High,
    };

    private static ModelStopReason MapStopReason(string? stopReason) => stopReason switch
    {
        "end_turn" or "stop_sequence" => ModelStopReason.Completed,
        "max_tokens" => ModelStopReason.MaxTokens,
        "refusal" => ModelStopReason.Refusal,
        _ => ModelStopReason.Other,
    };
}

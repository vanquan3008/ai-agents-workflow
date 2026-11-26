namespace DevAgent.Application.Abstractions.Ai;

public enum ModelRole
{
    User = 1,
    Assistant = 2,
}

public sealed record ModelMessage(ModelRole Role, string Content);

/// <summary>
/// Hợp đồng gọi model độc lập nhà cung cấp (checklist 0.5). Application chỉ biết hợp đồng này;
/// adapter trong Infrastructure ánh xạ sang SDK cụ thể. Đổi nhà cung cấp qua cấu hình <c>Ai:Provider</c>.
/// </summary>
/// <param name="Purpose">Mục đích gọi (planner/coding/reviewer) để ghi trace và chọn model theo cấu hình.</param>
/// <param name="SystemPrompt">Chỉ dẫn của hệ thống. Nội dung repo/issue không bao giờ đặt ở đây.</param>
/// <param name="Messages">Hội thoại; nội dung không tin cậy được bọc và đánh dấu là dữ liệu.</param>
/// <param name="MaxOutputTokens">Trần đầu ra cho lần gọi này.</param>
/// <param name="ModelOverride">Model id cụ thể; null dùng model mặc định của <paramref name="Purpose"/>.</param>
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

/// <summary>Đơn giá theo model (USD / 1 triệu token), dùng để giới hạn trần đầu ra theo ngân sách còn lại.</summary>
public sealed record ModelPrice(decimal InputPerMillion, decimal OutputPerMillion, decimal CacheReadPerMillion)
{
    public static ModelPrice Free { get; } = new(0m, 0m, 0m);
}

public interface IModelClient
{
    string ProviderName { get; }

    /// <summary>Model sẽ dùng cho một mục đích, để ghi vào AgentExecution trước khi gọi.</summary>
    string ResolveModel(string purpose);

    /// <summary>Đơn giá của model; null nếu chưa cấu hình (khi đó hệ thống từ chối gọi để không vượt ngân sách).</summary>
    ModelPrice? GetPrice(string model);

    Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken);
}

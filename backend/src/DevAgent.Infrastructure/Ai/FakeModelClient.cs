using System.Text.Json;
using DevAgent.Application.Abstractions.Ai;
using DevAgent.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace DevAgent.Infrastructure.Ai;

/// <summary>
/// Nhà cung cấp AI giả lập, kết quả cố định theo đầu vào (checklist 1.5: CI dùng dữ liệu xác định).
/// Không gọi mạng, không tốn chi phí. Bị validator chặn ở Production.
/// </summary>
internal sealed class FakeModelClient(IOptions<AiOptions> options) : IModelClient
{
    private static readonly string[] Steps =
    [
        "Viết test hồi quy tái hiện lỗi bộ lọc trả về sai trạng thái.",
        "Sửa điều kiện lọc trong truy vấn.",
        "Chạy build và toàn bộ test.",
    ];

    private static readonly string[] AcceptanceCriteria =
    [
        "Test hồi quy thất bại trên bản gốc và thành công trên bản sửa.",
        "Toàn bộ test hiện có vẫn đạt.",
    ];

    private static readonly string[] Risks = ["Thay đổi có thể ảnh hưởng tới các truy vấn dùng chung bộ lọc."];
    private static readonly string[] SafePaths = ["src/Tasks/TaskFilter.cs", "tests/Tasks/TaskFilterTests.cs"];
    private static readonly string[] SensitivePaths = ["src/Auth/PermissionService.cs", "tests/Auth/PermissionServiceTests.cs"];

    public string ProviderName => "fake";

    public string ResolveModel(string purpose) => $"fake-{options.Value.ModelFor(purpose)}";

    /// <summary>Giả lập không tốn chi phí; trần token và số lần gọi vẫn được áp dụng.</summary>
    public ModelPrice? GetPrice(string model) => ModelPrice.Free;

    public Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var input = string.Join('\n', request.Messages.Select(m => m.Content));
        var text = request.Purpose switch
        {
            "planner" => PlannerResponse(input),
            _ => "{}",
        };

        var inputTokens = Math.Max(1, (request.SystemPrompt.Length + input.Length) / 4);
        var outputTokens = Math.Max(1, text.Length / 4);
        var usage = new ModelUsage(inputTokens, outputTokens, 0, 0m);
        return Task.FromResult(new ModelResponse(text, ModelStopReason.Completed, null, usage, ResolveModel(request.Purpose)));
    }

    private static string PlannerResponse(string input)
    {
        // Quy ước cho test: mô tả chứa "[clarify]" thì Planner hỏi lại; chứa "[sensitive]" thì kế hoạch chạm vùng nhạy cảm.
        if (input.Contains("[clarify]", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                needs_clarification = true,
                clarification_question = "Hãy mô tả các bước tái hiện và kết quả mong đợi.",
                summary = string.Empty,
            });
        }

        var sensitive = input.Contains("[sensitive]", StringComparison.OrdinalIgnoreCase);
        return JsonSerializer.Serialize(new
        {
            needs_clarification = false,
            clarification_question = (string?)null,
            summary = "Sửa bộ lọc trạng thái task và thêm test hồi quy.",
            steps = Steps,
            acceptance_criteria = AcceptanceCriteria,
            risks = Risks,
            affected_paths = sensitive ? SensitivePaths : SafePaths,
        });
    }
}

using System.ComponentModel.DataAnnotations;

namespace DevAgent.Application.Options;

/// <summary>
/// Hạn mức cho mỗi task (checklist 0.7). Giá trị mặc định là đề xuất cho pilot;
/// chủ dự án chốt con số cuối cùng trong appsettings.
/// </summary>
public sealed class AgentLimitsOptions
{
    public const string SectionName = "AgentLimits";

    /// <summary>Ngân sách chi phí model tối đa cho một task (USD).</summary>
    [Range(typeof(decimal), "0.01", "1000")]
    public decimal MaxCostUsdPerTask { get; set; } = 2.00m;

    /// <summary>Tổng token (vào + ra) tối đa cho một task.</summary>
    [Range(1_000, 50_000_000)]
    public long MaxTokensPerTask { get; set; } = 2_000_000;

    /// <summary>Số lần gọi model tối đa cho một task.</summary>
    [Range(1, 1_000)]
    public int MaxModelCallsPerTask { get; set; } = 40;

    /// <summary>Trần token đầu ra cho một lần gọi model.</summary>
    [Range(256, 128_000)]
    public int MaxOutputTokensPerCall { get; set; } = 16_000;

    /// <summary>Thời gian tối đa từ lúc tạo task đến khi hoàn tất (không tính thời gian chờ duyệt).</summary>
    [Range(1, 24 * 60)]
    public int TaskTimeoutMinutes { get; set; } = 60;

    /// <summary>Số vòng sửa–test tối đa trước khi dừng.</summary>
    [Range(0, 20)]
    public int MaxFixIterations { get; set; } = 3;

    /// <summary>Số lần người vận hành được thử lại một task lỗi.</summary>
    [Range(0, 20)]
    public int MaxRetries { get; set; } = 2;

    /// <summary>Số task xử lý đồng thời trên toàn hệ thống pilot.</summary>
    [Range(1, 100)]
    public int MaxConcurrentTasks { get; set; } = 2;

    /// <summary>Thời hạn của một yêu cầu duyệt trước khi hết hạn.</summary>
    [Range(1, 24 * 30)]
    public int ApprovalTtlHours { get; set; } = 72;

    /// <summary>Số file tối đa đưa vào ngữ cảnh Planner.</summary>
    [Range(10, 20_000)]
    public int PlannerMaxFiles { get; set; } = 500;
}

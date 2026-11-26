using System.ComponentModel.DataAnnotations;

namespace DevAgent.Worker;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    /// <summary>Định danh Worker; để trống thì dùng {hostname}-{pid}. Phải duy nhất giữa các replica.</summary>
    public string? WorkerId { get; set; }

    /// <summary>Số job một Worker chạy song song (trần toàn hệ thống nằm ở AgentLimits:MaxConcurrentTasks).</summary>
    [Range(1, 32)]
    public int MaxConcurrentJobs { get; set; } = 2;

    [Range(1, 300)]
    public int PollIntervalSeconds { get; set; } = 2;

    /// <summary>Thời hạn lease. Worker chết thì job quay lại hàng đợi sau khoảng này.</summary>
    [Range(15, 3600)]
    public int LeaseDurationSeconds { get; set; } = 120;

    /// <summary>Chu kỳ gia hạn lease; phải nhỏ hơn LeaseDurationSeconds.</summary>
    [Range(5, 1800)]
    public int LeaseRenewIntervalSeconds { get; set; } = 40;

    [Range(5, 600)]
    public int HeartbeatIntervalSeconds { get; set; } = 15;

    /// <summary>File được chạm định kỳ để Docker healthcheck kiểm tra Worker còn sống.</summary>
    public string? HeartbeatFile { get; set; }

    /// <summary>Thời gian chờ các job đang chạy kết thúc khi tắt Worker.</summary>
    [Range(1, 600)]
    public int ShutdownGraceSeconds { get; set; } = 30;
}

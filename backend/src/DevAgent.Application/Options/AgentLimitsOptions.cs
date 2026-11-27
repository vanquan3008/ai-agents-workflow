using System.ComponentModel.DataAnnotations;

namespace DevAgent.Application.Options;

/// <summary>Per-task limits. Defaults are pilot suggestions; the final values belong in appsettings.</summary>
public sealed class AgentLimitsOptions
{
    public const string SectionName = "AgentLimits";

    /// <summary>Maximum model spend per task, in USD.</summary>
    [Range(typeof(decimal), "0.01", "1000")]
    public decimal MaxCostUsdPerTask { get; set; } = 2.00m;

    /// <summary>Maximum input plus output tokens per task.</summary>
    [Range(1_000, 50_000_000)]
    public long MaxTokensPerTask { get; set; } = 2_000_000;

    [Range(1, 1_000)]
    public int MaxModelCallsPerTask { get; set; } = 40;

    [Range(256, 128_000)]
    public int MaxOutputTokensPerCall { get; set; } = 16_000;

    /// <summary>Maximum active processing time per task, excluding time spent waiting for approval.</summary>
    [Range(1, 24 * 60)]
    public int TaskTimeoutMinutes { get; set; } = 60;

    /// <summary>Maximum fix-and-test iterations before stopping.</summary>
    [Range(0, 20)]
    public int MaxFixIterations { get; set; } = 3;

    /// <summary>How many times an operator may retry a failed task.</summary>
    [Range(0, 20)]
    public int MaxRetries { get; set; } = 2;

    /// <summary>Tasks processed concurrently across the whole system.</summary>
    [Range(1, 100)]
    public int MaxConcurrentTasks { get; set; } = 2;

    /// <summary>Lifetime of an approval request before it expires.</summary>
    [Range(1, 24 * 30)]
    public int ApprovalTtlHours { get; set; } = 72;

    /// <summary>Maximum number of files listed in the Planner context.</summary>
    [Range(10, 20_000)]
    public int PlannerMaxFiles { get; set; } = 500;
}

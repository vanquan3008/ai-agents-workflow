using System.ComponentModel.DataAnnotations;

namespace DevAgent.Worker.Hosting;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    /// <summary>Worker id; defaults to {hostname}-{pid}. Must be unique across replicas.</summary>
    public string? WorkerId { get; set; }

    /// <summary>Concurrent jobs per worker (the system-wide cap is AgentLimits:MaxConcurrentTasks).</summary>
    [Range(1, 32)]
    public int MaxConcurrentJobs { get; set; } = 2;

    [Range(1, 300)]
    public int PollIntervalSeconds { get; set; } = 2;

    /// <summary>Lease duration; a dead worker's job returns to the queue after this long.</summary>
    [Range(15, 3600)]
    public int LeaseDurationSeconds { get; set; } = 120;

    /// <summary>Lease renewal interval; must be shorter than LeaseDurationSeconds.</summary>
    [Range(5, 1800)]
    public int LeaseRenewIntervalSeconds { get; set; } = 40;

    [Range(5, 600)]
    public int HeartbeatIntervalSeconds { get; set; } = 15;

    /// <summary>File touched periodically so the Docker healthcheck can tell the worker is alive.</summary>
    public string? HeartbeatFile { get; set; }

    /// <summary>Interval of the dead-letter reconciliation sweep.</summary>
    [Range(5, 3600)]
    public int DeadLetterScanIntervalSeconds { get; set; } = 15;

    /// <summary>How long shutdown waits for running jobs to finish.</summary>
    [Range(1, 600)]
    public int ShutdownGraceSeconds { get; set; } = 30;
}

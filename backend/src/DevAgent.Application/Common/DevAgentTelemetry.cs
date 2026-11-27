using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DevAgent.Application.Common;

/// <summary>
/// Shared trace and metric sources (BCL only, no OpenTelemetry dependency); infrastructure registers this name with OTel.
/// Standard tags <c>devagent.task_id</c>, <c>devagent.execution_id</c> and <c>devagent.job_id</c> correlate logs and traces.
/// </summary>
public static class DevAgentTelemetry
{
    public const string SourceName = "DevAgent";
    public const string TaskIdTag = "devagent.task_id";
    public const string ExecutionIdTag = "devagent.execution_id";
    public const string JobIdTag = "devagent.job_id";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    public static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> JobsProcessed = Meter.CreateCounter<long>("devagent.jobs.processed", description: "Processed jobs by outcome.");

    public static readonly Counter<long> ModelTokens = Meter.CreateCounter<long>("devagent.model.tokens", unit: "{token}", description: "Model tokens consumed.");

    public static readonly Histogram<double> JobDuration = Meter.CreateHistogram<double>("devagent.jobs.duration", unit: "s", description: "Job processing duration.");
}

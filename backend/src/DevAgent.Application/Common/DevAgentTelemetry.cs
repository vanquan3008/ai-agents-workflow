using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DevAgent.Application.Common;

/// <summary>
/// Nguồn trace/metric dùng chung (BCL, không phụ thuộc OpenTelemetry). Hạ tầng đăng ký tên này với OTel.
/// Tag chuẩn: <c>devagent.task_id</c>, <c>devagent.execution_id</c>, <c>devagent.job_id</c> để liên kết log–trace.
/// </summary>
public static class DevAgentTelemetry
{
    public const string SourceName = "DevAgent";
    public const string TaskIdTag = "devagent.task_id";
    public const string ExecutionIdTag = "devagent.execution_id";
    public const string JobIdTag = "devagent.job_id";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    public static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> JobsProcessed = Meter.CreateCounter<long>("devagent.jobs.processed", description: "Số job đã xử lý theo kết quả.");

    public static readonly Counter<long> ModelTokens = Meter.CreateCounter<long>("devagent.model.tokens", unit: "{token}", description: "Token model đã dùng.");

    public static readonly Histogram<double> JobDuration = Meter.CreateHistogram<double>("devagent.jobs.duration", unit: "s", description: "Thời lượng xử lý job.");
}

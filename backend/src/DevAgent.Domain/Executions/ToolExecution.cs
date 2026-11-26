using DevAgent.Domain.Common;

namespace DevAgent.Domain.Executions;

public enum ToolExecutionStatus
{
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Denied = 4,
}

/// <summary>
/// Một lần agent gọi công cụ (đọc/sửa file, chạy build/test...). Lưu đầu vào, đầu ra có cấu trúc,
/// mã thoát và lý do từ chối nếu chính sách chặn. Đầu vào/đầu ra phải được che dữ liệu nhạy cảm
/// trước khi gán vào đây.
/// </summary>
public sealed class ToolExecution : Entity<ToolExecutionId>
{
    private ToolExecution()
    {
        ToolName = string.Empty;
        InputJson = "{}";
    }

    internal ToolExecution(ToolExecutionId id, ExecutionId executionId, int sequence, string toolName, string inputJson, DateTimeOffset startedAt)
        : base(id)
    {
        ExecutionId = executionId;
        Sequence = sequence;
        ToolName = toolName;
        InputJson = string.IsNullOrWhiteSpace(inputJson) ? "{}" : inputJson;
        StartedAt = startedAt;
        Status = ToolExecutionStatus.Running;
    }

    public ExecutionId ExecutionId { get; private set; }

    public int Sequence { get; private set; }

    public string ToolName { get; private set; }

    public string InputJson { get; private set; }

    public string? OutputJson { get; private set; }

    public int? ExitCode { get; private set; }

    public ToolExecutionStatus Status { get; private set; }

    public string? DenialReason { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public void Succeed(string outputJson, int? exitCode, DateTimeOffset now) =>
        Finish(ToolExecutionStatus.Succeeded, outputJson, exitCode, null, now);

    public void Fail(string outputJson, int? exitCode, DateTimeOffset now) =>
        Finish(ToolExecutionStatus.Failed, outputJson, exitCode, null, now);

    public void Deny(string reason, DateTimeOffset now) =>
        Finish(ToolExecutionStatus.Denied, null, null, Guard.NotEmpty(reason, "denial_reason", 2000), now);

    private void Finish(ToolExecutionStatus status, string? outputJson, int? exitCode, string? denialReason, DateTimeOffset now)
    {
        if (Status != ToolExecutionStatus.Running)
        {
            throw new DomainException("tool_execution.finished", "Lần gọi công cụ đã kết thúc.");
        }

        Status = status;
        OutputJson = outputJson;
        ExitCode = exitCode;
        DenialReason = denialReason;
        FinishedAt = Guard.Utc(now, "tool_finished_at");
    }
}

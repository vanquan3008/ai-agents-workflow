using System.Text.Json;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Features.Tasks;
using DevAgent.Domain.Common;
using Microsoft.Extensions.Logging;

namespace DevAgent.Application.Orchestration;

/// <summary>Nhận job <see cref="JobTypes.ProcessTask"/> và giao cho Orchestrator. Idempotent theo trạng thái task.</summary>
internal sealed partial class ProcessTaskJobHandler(
    IAgentOrchestrator orchestrator,
    IWorkerIdentity worker,
    ILogger<ProcessTaskJobHandler> logger) : IJobHandler
{
    public string JobType => JobTypes.ProcessTask;

    public async Task HandleAsync(LeasedJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        var taskId = ParseTaskId(job.PayloadJson, job.Id);
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["TaskId"] = taskId.Value });
        await orchestrator.AdvanceAsync(taskId, worker.WorkerId, cancellationToken);
    }

    /// <summary>
    /// Đối soát khi job hết lượt thử — gồm cả trường hợp Worker chết ở lần thử cuối (chỉ bảng job được cập nhật):
    /// chuyển task sang FAILED và đóng execution còn treo, kèm lý do lỗi cuối cùng.
    /// </summary>
    public async Task OnDeadLetterAsync(DeadLetterJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        var taskId = ParseTaskId(job.PayloadJson, job.Id);
        LogDeadLetter(logger, taskId, job.Attempt, job.LastError);
        await orchestrator.MarkFailedAsync(
            taskId,
            $"Xử lý thất bại sau {job.Attempt}/{job.MaxAttempts} lần thử: {job.LastError ?? "không rõ lỗi"}",
            cancellationToken);
    }

    private static TaskId ParseTaskId(string payload, Guid jobId) =>
        new((JsonSerializer.Deserialize<ProcessTaskPayload>(payload)
            ?? throw new InvalidOperationException($"Payload job {jobId} không hợp lệ.")).TaskId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Task {TaskId} hết lượt thử ({Attempt}); đánh dấu thất bại. Lỗi cuối: {LastError}")]
    private static partial void LogDeadLetter(ILogger logger, TaskId taskId, int attempt, string? lastError);
}

/// <summary>Định danh tiến trình Worker hiện tại (ghi vào lease, execution và AgentInstance).</summary>
public interface IWorkerIdentity
{
    string WorkerId { get; }
}

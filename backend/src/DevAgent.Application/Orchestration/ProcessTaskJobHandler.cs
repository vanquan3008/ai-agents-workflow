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
        var payload = JsonSerializer.Deserialize<ProcessTaskPayload>(job.PayloadJson)
            ?? throw new InvalidOperationException($"Payload job {job.Id} không hợp lệ.");
        var taskId = new TaskId(payload.TaskId);

        try
        {
            await orchestrator.AdvanceAsync(taskId, worker.WorkerId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && job.Attempt >= job.MaxAttempts)
        {
            // Lần thử cuối: ghi lý do lên task để người vận hành thấy, rồi để job vào dead-letter.
            LogFinalFailure(logger, ex, taskId, job.Attempt);
            await orchestrator.MarkFailedAsync(taskId, $"Xử lý thất bại sau {job.Attempt} lần thử: {ex.Message}", CancellationToken.None);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Task {TaskId} thất bại ở lần thử cuối {Attempt}")]
    private static partial void LogFinalFailure(ILogger logger, Exception exception, TaskId taskId, int attempt);
}

/// <summary>Định danh tiến trình Worker hiện tại (ghi vào lease, execution và AgentInstance).</summary>
public interface IWorkerIdentity
{
    string WorkerId { get; }
}

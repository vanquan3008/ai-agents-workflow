using System.Text.Json;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.AgentRuntime.Orchestration;
using DevAgent.Application.Features.Tasks;
using DevAgent.Domain.Common;
using Microsoft.Extensions.Logging;

namespace DevAgent.Application.AgentRuntime.Jobs;

/// <summary>Hands <see cref="JobTypes.ProcessTask"/> jobs to the orchestrator. Idempotent with respect to task status.</summary>
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
    /// Reconciles a job that ran out of attempts, including when the worker died on its last attempt and only the job
    /// row was updated: the task becomes FAILED and dangling executions are closed with the last error.
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
            ?? throw new InvalidOperationException($"Payload of job {jobId} is invalid.")).TaskId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Task {TaskId} ran out of attempts ({Attempt}); marking it failed. Last error: {LastError}")]
    private static partial void LogDeadLetter(ILogger logger, TaskId taskId, int attempt, string? lastError);
}

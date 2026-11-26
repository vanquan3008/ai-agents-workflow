using System.Text.Json;
using DevAgent.Application.Abstractions.Integrations;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Approvals;
using DevAgent.Application.Features.Tasks;
using DevAgent.Application.Options;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Common;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Repositories.Repositories;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Tasks.Repositories;
using DevAgent.Domain.Workflows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.Features.Notifications;

/// <summary>
/// Delivers outbox notifications. Reads the latest data at send time (title, PR link, approval link) and filters by the
/// task's workflow. Transient failures thrown by <see cref="IChatNotifier"/> are retried by the queue with backoff/Retry-After.
/// </summary>
internal sealed partial class NotificationJobHandler(
    ITaskRepository tasks,
    ICodeRepositoryRepository repositories,
    ITaskQueries taskQueries,
    IApprovalQueries approvalQueries,
    IChatNotifier notifier,
    IOptions<PublicUrlsOptions> urls,
    ILogger<NotificationJobHandler> logger) : IJobHandler
{
    public string JobType => JobTypes.SendNotification;

    public async Task HandleAsync(LeasedJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        var payload = JsonSerializer.Deserialize<NotificationPayload>(job.PayloadJson)
            ?? throw new PermanentJobException($"Notification payload of job {job.Id} is invalid.");

        var taskId = new TaskId(payload.TaskId);
        var task = await tasks.GetByIdAsync(taskId, cancellationToken);
        var detail = await taskQueries.GetAsync(taskId, cancellationToken);
        if (task is null || detail is null)
        {
            LogSkipped(logger, payload.TaskId, "task no longer exists");
            return;
        }

        var workflow = task.Workflow
            ?? (await repositories.GetByIdAsync(task.RepositoryId, cancellationToken))?.Workflow
            ?? WorkflowDefinition.Default;
        if (!workflow.ShouldNotify(NotificationMapping.ToEvent(payload.Kind)))
        {
            LogSkipped(logger, payload.TaskId, $"workflow disables {payload.Kind} notifications");
            return;
        }

        var link = payload.Kind switch
        {
            ChatNotificationKind.Completed when detail.PullRequest is not null => detail.PullRequest.Url,
            ChatNotificationKind.ApprovalRequired => await ApprovalLinkAsync(task, cancellationToken) ?? urls.Value.TaskUrl(detail.Id),
            _ => urls.Value.TaskUrl(detail.Id),
        };

        await notifier.NotifyAsync(
            new ChatNotification(
                payload.Kind,
                detail.Id,
                detail.ProjectId,
                detail.Title,
                payload.Reason ?? detail.Status,
                link,
                detail.SourceReference,
                NotificationMapping.DeduplicationKey(payload.TaskId, payload.Sequence)),
            cancellationToken);
    }

    public Task OnDeadLetterAsync(DeadLetterJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        // There is no other channel left to report on; log for operators. Task state never depends on notifications.
        LogDeadLetter(logger, job.Id, job.Attempt, job.LastError);
        return Task.CompletedTask;
    }

    private async Task<string?> ApprovalLinkAsync(AgentTask task, CancellationToken cancellationToken)
    {
        var pending = await approvalQueries.ListAsync([task.ProjectId], ApprovalStatus.Pending, task.Id, new PageRequest(1, 1), cancellationToken);
        return pending.Items.Count > 0 ? urls.Value.ApprovalUrl(pending.Items[0].Id) : null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipping notification for task {TaskId}: {Reason}")]
    private static partial void LogSkipped(ILogger logger, Guid taskId, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Notification job {JobId} failed after {Attempt} attempts: {LastError}")]
    private static partial void LogDeadLetter(ILogger logger, Guid jobId, int attempt, string? lastError);
}

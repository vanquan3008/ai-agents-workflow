using System.Text.Json;
using DevAgent.Application.Abstractions.Events;
using DevAgent.Application.Abstractions.Integrations;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Approvals;
using DevAgent.Application.Features.Tasks;
using DevAgent.Application.Options;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Workflows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.Notifications;

/// <summary>Payload của job <see cref="JobTypes.SendNotification"/>.</summary>
public sealed record NotificationPayload(Guid TaskId, ChatNotificationKind Kind, long Sequence, string? Reason);

internal static class NotificationMapping
{
    public static ChatNotificationKind? KindFor(TaskStatusChanged change) => change.To switch
    {
        AgentTaskStatus.Pending when change.From is null => ChatNotificationKind.TaskAccepted,
        AgentTaskStatus.WaitingForApproval => ChatNotificationKind.ApprovalRequired,
        AgentTaskStatus.NeedsClarification => ChatNotificationKind.ClarificationNeeded,
        AgentTaskStatus.Completed => ChatNotificationKind.Completed,
        AgentTaskStatus.Failed or AgentTaskStatus.TimedOut or AgentTaskStatus.Rejected => ChatNotificationKind.Failed,
        AgentTaskStatus.Blocked => ChatNotificationKind.Blocked,
        AgentTaskStatus.Cancelled => ChatNotificationKind.Cancelled,
        _ => null,
    };

    public static NotificationEvent ToEvent(ChatNotificationKind kind) => kind switch
    {
        ChatNotificationKind.TaskAccepted => NotificationEvent.TaskAccepted,
        ChatNotificationKind.ApprovalRequired => NotificationEvent.ApprovalRequired,
        ChatNotificationKind.ClarificationNeeded => NotificationEvent.ClarificationNeeded,
        ChatNotificationKind.Completed => NotificationEvent.Completed,
        ChatNotificationKind.Failed => NotificationEvent.Failed,
        ChatNotificationKind.Blocked => NotificationEvent.Blocked,
        ChatNotificationKind.Cancelled => NotificationEvent.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static string DeduplicationKey(Guid taskId, long sequence) => $"{JobTypes.SendNotification}:{taskId}:{sequence}";
}

/// <summary>
/// Outbox: khi trạng thái task đổi tới mốc cần thông báo, xếp job gửi thông báo <b>trong cùng giao dịch</b>.
/// Tiến trình dừng ngay sau commit hay Discord lỗi đều không làm mất thông báo — job còn đó và được thử lại.
/// Khóa chống trùng theo (task, số thứ tự chuyển trạng thái) nên mỗi mốc chỉ có một thông báo.
/// </summary>
internal sealed class TaskNotificationOutbox(IJobQueue jobQueue) : ITransactionalDomainEventHandler<TaskStatusChanged>
{
    public Task HandleAsync(TaskStatusChanged domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (NotificationMapping.KindFor(domainEvent) is not { } kind)
        {
            return Task.CompletedTask;
        }

        var payload = new NotificationPayload(domainEvent.TaskId.Value, kind, domainEvent.Sequence, domainEvent.Reason);
        return jobQueue.EnqueueAsync(
            new JobRequest(
                JobTypes.SendNotification,
                JsonSerializer.Serialize(payload),
                NotificationMapping.DeduplicationKey(domainEvent.TaskId.Value, domainEvent.Sequence)),
            cancellationToken);
    }
}

/// <summary>
/// Gửi thông báo từ outbox. Đọc dữ liệu mới nhất lúc gửi (tiêu đề, link PR, link màn hình duyệt) và lọc theo
/// quy trình của task. Lỗi tạm thời do <see cref="IChatNotifier"/> ném ra được hàng đợi thử lại với backoff/Retry-After.
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
            ?? throw new PermanentJobException($"Payload thông báo {job.Id} không hợp lệ.");

        var taskId = new TaskId(payload.TaskId);
        var task = await tasks.GetByIdAsync(taskId, cancellationToken);
        var detail = await taskQueries.GetAsync(taskId, cancellationToken);
        if (task is null || detail is null)
        {
            LogSkipped(logger, payload.TaskId, "task không còn tồn tại");
            return;
        }

        var workflow = task.Workflow
            ?? (await repositories.GetByIdAsync(task.RepositoryId, cancellationToken))?.Workflow
            ?? WorkflowDefinition.Default;
        if (!workflow.ShouldNotify(NotificationMapping.ToEvent(payload.Kind)))
        {
            LogSkipped(logger, payload.TaskId, $"quy trình tắt thông báo {payload.Kind}");
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

        // Không còn kênh nào khác để báo; log lỗi để cảnh báo vận hành. Trạng thái task không phụ thuộc thông báo.
        LogDeadLetter(logger, job.Id, job.Attempt, job.LastError);
        return Task.CompletedTask;
    }

    private async Task<string?> ApprovalLinkAsync(AgentTask task, CancellationToken cancellationToken)
    {
        var pending = await approvalQueries.ListAsync([task.ProjectId], ApprovalStatus.Pending, task.Id, new PageRequest(1, 1), cancellationToken);
        return pending.Items.Count > 0 ? urls.Value.ApprovalUrl(pending.Items[0].Id) : null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Bỏ qua thông báo cho task {TaskId}: {Reason}")]
    private static partial void LogSkipped(ILogger logger, Guid taskId, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Không gửi được thông báo (job {JobId}) sau {Attempt} lần thử: {LastError}")]
    private static partial void LogDeadLetter(ILogger logger, Guid jobId, int attempt, string? lastError);
}

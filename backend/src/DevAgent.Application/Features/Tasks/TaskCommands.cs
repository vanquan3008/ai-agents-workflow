using System.Text.Json;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Options;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.Features.Tasks;

/// <summary>Payload của job <see cref="JobTypes.ProcessTask"/>.</summary>
public sealed record ProcessTaskPayload(Guid TaskId);

internal static class TaskJobs
{
    /// <summary>
    /// Khóa chống trùng gắn với số thứ tự chuyển trạng thái: mỗi trạng thái chỉ sinh một job xử lý,
    /// dù use case chạy lặp hay Worker xử lý lại.
    /// </summary>
    public static JobRequest ProcessTask(AgentTask task) => new(
        JobTypes.ProcessTask,
        JsonSerializer.Serialize(new ProcessTaskPayload(task.Id.Value)),
        $"{JobTypes.ProcessTask}:{task.Id}:{task.Transitions.Max(t => t.Sequence)}");
}

// ---------- Tạo task ----------

public sealed record CreateTaskCommand(
    Guid RepositoryId,
    string Title,
    string Description,
    TaskChannel Channel,
    string? SourceReference,
    string IdempotencyKey);

internal sealed partial class CreateTaskHandler(
    ProjectAccess access,
    ICodeRepositoryRepository repositories,
    ITaskRepository tasks,
    IJobQueue jobQueue,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<CreateTaskHandler> logger) : ICommandHandler<CreateTaskCommand, TaskCreatedDto>
{
    public async Task<Result<TaskCreatedDto>> HandleAsync(CreateTaskCommand command, CancellationToken cancellationToken)
    {
        var validation = Validate(command);
        if (validation is not null)
        {
            return validation;
        }

        var repository = await repositories.GetByIdAsync(new RepositoryId(command.RepositoryId), cancellationToken);
        if (repository is null || !repository.IsActive)
        {
            return Error.NotFound("repository", command.RepositoryId);
        }

        var userId = await access.RequireAsync(repository.ProjectId, ProjectPermission.CreateTasks, "repository", repository.Id.ToString(), cancellationToken);
        if (userId.IsFailure)
        {
            return userId.Error!;
        }

        var key = new IdempotencyKey(command.IdempotencyKey);
        var existing = await tasks.FindByIdempotencyKeyAsync(repository.ProjectId, key, cancellationToken);
        if (existing is not null)
        {
            return new TaskCreatedDto(existing.Id.Value, existing.Status.Api(), Created: false);
        }

        var now = clock.GetUtcNow();
        var task = AgentTask.Create(
            repository.ProjectId,
            repository.Id,
            command.Title,
            command.Description,
            new TaskSource(command.Channel, command.SourceReference),
            key,
            userId.Value,
            now);

        tasks.Add(task);
        await jobQueue.EnqueueAsync(TaskJobs.ProcessTask(task), cancellationToken);
        auditLog.Add(AuditEntry.Record(
            Actor.ForUser(userId.Value), "task.create", "task", task.Id.ToString(), AuditOutcome.Allowed, now, task.ProjectId, task.Id,
            reason: $"channel={command.Channel}"));

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // Hai request cùng khóa chạy song song: request thua trả về task của request thắng.
            LogDuplicate(logger, key.Value);
            var winner = await tasks.FindByIdempotencyKeyAsync(repository.ProjectId, key, cancellationToken);
            return winner is null
                ? Error.Conflict("task.duplicate", "Yêu cầu trùng đang được xử lý, hãy thử lại.")
                : new TaskCreatedDto(winner.Id.Value, winner.Status.Api(), Created: false);
        }

        return new TaskCreatedDto(task.Id.Value, task.Status.Api(), Created: true);
    }

    private static Error? Validate(CreateTaskCommand command)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.Title) || command.Title.Length > AgentTask.TitleMaxLength)
        {
            errors["title"] = [$"Tiêu đề bắt buộc, tối đa {AgentTask.TitleMaxLength} ký tự."];
        }

        if (string.IsNullOrWhiteSpace(command.Description) || command.Description.Length > AgentTask.DescriptionMaxLength)
        {
            errors["description"] = [$"Mô tả bắt buộc, tối đa {AgentTask.DescriptionMaxLength} ký tự."];
        }

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > IdempotencyKey.MaxLength)
        {
            errors["idempotencyKey"] = ["Thiếu header Idempotency-Key hoặc quá dài."];
        }

        return errors.Count == 0 ? null : Error.Validation("task.invalid", "Dữ liệu tạo task không hợp lệ.", errors);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Phát hiện tạo task trùng với idempotency key {Key}")]
    private static partial void LogDuplicate(ILogger logger, string key);
}

// ---------- Hủy task ----------

public sealed record CancelTaskCommand(Guid TaskId, string? Reason);

internal sealed class CancelTaskHandler(
    ProjectAccess access,
    ITaskRepository tasks,
    IApprovalRepository approvals,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CancelTaskCommand, string>
{
    public async Task<Result<string>> HandleAsync(CancelTaskCommand command, CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(new TaskId(command.TaskId), cancellationToken);
        if (task is null)
        {
            return Error.NotFound("task", command.TaskId);
        }

        var userId = await access.RequireAsync(task.ProjectId, ProjectPermission.CancelTasks, "task", task.Id.ToString(), cancellationToken);
        if (userId.IsFailure)
        {
            return userId.Error!;
        }

        if (task.Status != AgentTaskStatus.Cancelled && !AgentTaskStateMachine.CanTransition(task.Status, AgentTaskStatus.Cancelled))
        {
            return Error.Conflict("task.cannot_cancel", $"Không thể hủy task ở trạng thái {task.Status.Api()}.");
        }

        var now = clock.GetUtcNow();
        task.Cancel(userId.Value, command.Reason, now);

        // Yêu cầu duyệt còn mở không còn ý nghĩa sau khi hủy.
        foreach (var approval in await approvals.ListOpenByTaskAsync(task.Id, cancellationToken))
        {
            approval.Supersede(now);
        }

        auditLog.Add(AuditEntry.Record(
            Actor.ForUser(userId.Value), "task.cancel", "task", task.Id.ToString(), AuditOutcome.Allowed, now, task.ProjectId, task.Id, command.Reason));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Worker đang chạy kiểm tra trạng thái trước mỗi bước/lần gọi model và tự dừng.
        return task.Status.Api();
    }
}

// ---------- Bổ sung thông tin khi NEEDS_CLARIFICATION ----------

public sealed record ProvideClarificationCommand(Guid TaskId, string AdditionalInfo);

internal sealed class ProvideClarificationHandler(
    ProjectAccess access,
    ITaskRepository tasks,
    IJobQueue jobQueue,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<ProvideClarificationCommand, string>
{
    public async Task<Result<string>> HandleAsync(ProvideClarificationCommand command, CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(new TaskId(command.TaskId), cancellationToken);
        if (task is null)
        {
            return Error.NotFound("task", command.TaskId);
        }

        var userId = await access.RequireAsync(task.ProjectId, ProjectPermission.CreateTasks, "task", task.Id.ToString(), cancellationToken);
        if (userId.IsFailure)
        {
            return userId.Error!;
        }

        if (task.Status != AgentTaskStatus.NeedsClarification)
        {
            return Error.Conflict("task.not_waiting_clarification", "Task không ở trạng thái cần bổ sung thông tin.");
        }

        task.ProvideClarification(command.AdditionalInfo, userId.Value, clock.GetUtcNow());
        await jobQueue.EnqueueAsync(TaskJobs.ProcessTask(task), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return task.Status.Api();
    }
}

// ---------- Thử lại task lỗi ----------

public sealed record RetryTaskCommand(Guid TaskId);

internal sealed class RetryTaskHandler(
    ProjectAccess access,
    ITaskRepository tasks,
    IJobQueue jobQueue,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    IOptions<AgentLimitsOptions> limits,
    TimeProvider clock) : ICommandHandler<RetryTaskCommand, string>
{
    public async Task<Result<string>> HandleAsync(RetryTaskCommand command, CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(new TaskId(command.TaskId), cancellationToken);
        if (task is null)
        {
            return Error.NotFound("task", command.TaskId);
        }

        var userId = await access.RequireAsync(task.ProjectId, ProjectPermission.CreateTasks, "task", task.Id.ToString(), cancellationToken);
        if (userId.IsFailure)
        {
            return userId.Error!;
        }

        if (!AgentTaskStateMachine.CanTransition(task.Status, AgentTaskStatus.Pending))
        {
            return Error.Conflict("task.cannot_retry", $"Không thể thử lại task ở trạng thái {task.Status.Api()}.");
        }

        if (task.RetryCount >= limits.Value.MaxRetries)
        {
            return Error.Conflict("task.retry_exhausted", $"Đã dùng hết {limits.Value.MaxRetries} lần thử lại.");
        }

        var now = clock.GetUtcNow();
        task.Retry(userId.Value, limits.Value.MaxRetries, now);
        await jobQueue.EnqueueAsync(TaskJobs.ProcessTask(task), cancellationToken);
        auditLog.Add(AuditEntry.Record(
            Actor.ForUser(userId.Value), "task.retry", "task", task.Id.ToString(), AuditOutcome.Allowed, now, task.ProjectId, task.Id));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return task.Status.Api();
    }
}

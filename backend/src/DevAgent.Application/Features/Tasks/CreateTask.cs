using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Auditing.Repositories;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Repositories.Repositories;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Tasks.Repositories;
using Microsoft.Extensions.Logging;

namespace DevAgent.Application.Features.Tasks;

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
            // Two concurrent requests with the same key: the loser returns the winner's task.
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Duplicate task creation detected for idempotency key {Key}")]
    private static partial void LogDuplicate(ILogger logger, string key);
}

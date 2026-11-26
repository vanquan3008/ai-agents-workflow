using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Options;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Auditing.Repositories;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Tasks.Repositories;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.Features.Tasks;

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

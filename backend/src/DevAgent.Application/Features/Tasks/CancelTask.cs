using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Approvals.Repositories;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Auditing.Repositories;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Tasks.Repositories;

namespace DevAgent.Application.Features.Tasks;

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

        foreach (var approval in await approvals.ListOpenByTaskAsync(task.Id, cancellationToken))
        {
            approval.Supersede(now);
        }

        auditLog.Add(AuditEntry.Record(
            Actor.ForUser(userId.Value), "task.cancel", "task", task.Id.ToString(), AuditOutcome.Allowed, now, task.ProjectId, task.Id, command.Reason));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // A running worker re-checks the status before every step and model call, then stops on its own.
        return task.Status.Api();
    }
}

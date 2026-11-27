using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Tasks;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Approvals.Repositories;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Auditing.Repositories;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Tasks.Repositories;

namespace DevAgent.Application.Features.Approvals;

public enum ApprovalDecision
{
    Approve = 1,
    Reject = 2,
}

public sealed record DecideApprovalCommand(Guid ApprovalId, ApprovalDecision Decision, string? Comment);

/// <summary>
/// An approver approves or rejects. Agents cannot approve their own work because this use case requires an
/// authenticated user and the worker has no user identity.
/// </summary>
internal sealed class DecideApprovalHandler(
    ProjectAccess access,
    IApprovalRepository approvals,
    ITaskRepository tasks,
    IJobQueue jobQueue,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<DecideApprovalCommand, ApprovalDto>
{
    public async Task<Result<ApprovalDto>> HandleAsync(DecideApprovalCommand command, CancellationToken cancellationToken)
    {
        var approval = await approvals.GetByIdAsync(new ApprovalId(command.ApprovalId), cancellationToken);
        if (approval is null)
        {
            return Error.NotFound("approval", command.ApprovalId);
        }

        var task = await tasks.GetByIdAsync(approval.TaskId, cancellationToken);
        if (task is null)
        {
            return Error.NotFound("task", approval.TaskId);
        }

        var userId = await access.RequireAsync(task.ProjectId, ProjectPermission.ApproveChanges, "approval", approval.Id.ToString(), cancellationToken);
        if (userId.IsFailure)
        {
            return userId.Error!;
        }

        if (command.Decision == ApprovalDecision.Reject && string.IsNullOrWhiteSpace(command.Comment))
        {
            return Error.Validation("approval.reason_required", "Từ chối cần nêu lý do.");
        }

        if (task.Status != AgentTaskStatus.WaitingForApproval)
        {
            return Error.Conflict("approval.task_not_waiting", $"Task đang ở trạng thái {task.Status.Api()}, không chờ duyệt.");
        }

        // A decision must target the exact subject: the current plan version or the verified commit.
        if (approval.Kind == ApprovalKind.Plan && approval.PlanVersion != task.CurrentPlan?.Version)
        {
            approval.Supersede(clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Error.Conflict("approval.superseded", "Kế hoạch đã thay đổi; yêu cầu duyệt này không còn hiệu lực.");
        }

        var now = clock.GetUtcNow();
        var approver = Actor.ForUser(userId.Value);
        if (command.Decision == ApprovalDecision.Approve)
        {
            approval.Approve(userId.Value, command.Comment, now);
            if (approval.Kind == ApprovalKind.Plan)
            {
                task.StartCoding(approver, $"Kế hoạch v{approval.PlanVersion} đã được duyệt.", now);
            }
            else
            {
                task.StartPublishing(approver, $"Diff tại commit {approval.CommitSha} đã được duyệt.", now);
            }

            await jobQueue.EnqueueAsync(TaskJobs.ProcessTask(task), cancellationToken);
        }
        else
        {
            approval.Reject(userId.Value, command.Comment!, now);
            task.Reject(userId.Value, $"Bị từ chối: {command.Comment}", now);
        }

        auditLog.Add(AuditEntry.Record(
            approver,
            $"approval.{command.Decision.ToString().ToLowerInvariant()}",
            "approval",
            approval.Id.ToString(),
            AuditOutcome.Allowed,
            now,
            task.ProjectId,
            task.Id,
            reason: command.Comment));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ApprovalDto.From(approval, task);
    }
}

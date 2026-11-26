using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Projects;
using DevAgent.Application.Features.Tasks;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Tasks;

namespace DevAgent.Application.Features.Approvals;

public sealed record ApprovalDto(
    Guid Id,
    Guid TaskId,
    Guid ProjectId,
    string TaskTitle,
    string Kind,
    string Status,
    int? PlanVersion,
    string? CommitSha,
    IReadOnlyList<string> SensitiveAreas,
    int PolicyVersion,
    string Scope,
    DateTimeOffset RequestedAt,
    DateTimeOffset ExpiresAt,
    Guid? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? Comment);

public interface IApprovalQueries
{
    Task<PagedResult<ApprovalDto>> ListAsync(
        IReadOnlyCollection<ProjectId> projectIds,
        ApprovalStatus? status,
        TaskId? taskId,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<ApprovalDto?> GetAsync(ApprovalId approvalId, CancellationToken cancellationToken);
}

// ---------- Duyệt / từ chối ----------

public enum ApprovalDecision
{
    Approve = 1,
    Reject = 2,
}

public sealed record DecideApprovalCommand(Guid ApprovalId, ApprovalDecision Decision, string? Comment);

/// <summary>
/// Người có quyền Approver duyệt/từ chối. Agent không thể tự duyệt vì use case yêu cầu người dùng
/// đã xác thực, và Worker không có danh tính người dùng.
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

        // Quyết định phải gắn với đúng đối tượng: kế hoạch hiện tại hoặc commit đã kiểm tra.
        if (approval.Kind == ApprovalKind.Plan && approval.PlanVersion != task.CurrentPlan?.Version)
        {
            approval.Supersede(clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Error.Conflict("approval.superseded", "Kế hoạch đã thay đổi; yêu cầu duyệt này không còn hiệu lực.");
        }

        var now = clock.GetUtcNow();
        if (command.Decision == ApprovalDecision.Approve)
        {
            approval.Approve(userId.Value, command.Comment, now);
            if (approval.Kind == ApprovalKind.Plan)
            {
                task.StartCoding(Actor.ForUser(userId.Value), $"Kế hoạch v{approval.PlanVersion} đã được duyệt.", now);
            }
            else
            {
                task.StartPublishing(Actor.ForUser(userId.Value), $"Diff tại commit {approval.CommitSha} đã được duyệt.", now);
            }

            await jobQueue.EnqueueAsync(TaskJobs.ProcessTask(task), cancellationToken);
        }
        else
        {
            approval.Reject(userId.Value, command.Comment!, now);
            task.Reject(userId.Value, $"Bị từ chối: {command.Comment}", now);
        }

        auditLog.Add(AuditEntry.Record(
            Actor.ForUser(userId.Value),
            $"approval.{command.Decision.ToString().ToLowerInvariant()}",
            "approval",
            approval.Id.ToString(),
            AuditOutcome.Allowed,
            now,
            task.ProjectId,
            task.Id,
            reason: command.Comment));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ApprovalDto(
            approval.Id.Value,
            task.Id.Value,
            task.ProjectId.Value,
            task.Title,
            ApiNames.ToApi(approval.Kind),
            ApiNames.ToApi(approval.Status),
            approval.PlanVersion,
            approval.CommitSha,
            ApiNames.FlagsToApi(approval.SensitiveAreas),
            approval.PolicyVersion,
            approval.Scope,
            approval.RequestedAt,
            approval.ExpiresAt,
            approval.DecidedBy?.Value,
            approval.DecidedAt,
            approval.Comment);
    }
}

// ---------- Truy vấn ----------

public sealed record ListApprovalsQuery(Guid? ProjectId, Guid? TaskId, string? Status, int? Page, int? PageSize);

internal sealed class ListApprovalsHandler(ProjectAccess access, IApprovalQueries approvalQueries)
    : IQueryHandler<ListApprovalsQuery, PagedResult<ApprovalDto>>
{
    public async Task<Result<PagedResult<ApprovalDto>>> HandleAsync(ListApprovalsQuery query, CancellationToken cancellationToken)
    {
        ApprovalStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!ApiNames.TryParse<ApprovalStatus>(query.Status, out var parsed))
            {
                return Error.Validation("approval.status_invalid", $"Trạng thái không hợp lệ: {query.Status}.");
            }

            status = parsed;
        }

        IReadOnlyCollection<ProjectId> projectIds;
        if (query.ProjectId is { } projectGuid)
        {
            var projectId = new ProjectId(projectGuid);
            var user = await access.RequireAsync(projectId, ProjectPermission.ViewTasks, "project", projectId.ToString(), cancellationToken);
            if (user.IsFailure)
            {
                return user.Error!;
            }

            projectIds = [projectId];
        }
        else
        {
            // Không truyền project: chỉ project người dùng được xem (Super Admin: mọi project).
            var allowed = await access.ProjectsWithPermissionAsync(ProjectPermission.ViewTasks, cancellationToken);
            if (allowed.IsFailure)
            {
                return allowed.Error!;
            }

            projectIds = allowed.Value;
        }

        return await approvalQueries.ListAsync(
            projectIds,
            status,
            query.TaskId is { } t ? new TaskId(t) : null,
            new PageRequest(query.Page, query.PageSize),
            cancellationToken);
    }
}

public sealed record GetApprovalQuery(Guid ApprovalId);

internal sealed class GetApprovalHandler(ProjectAccess access, IApprovalQueries approvalQueries) : IQueryHandler<GetApprovalQuery, ApprovalDto>
{
    public async Task<Result<ApprovalDto>> HandleAsync(GetApprovalQuery query, CancellationToken cancellationToken)
    {
        var approval = await approvalQueries.GetAsync(new ApprovalId(query.ApprovalId), cancellationToken);
        if (approval is null)
        {
            return Error.NotFound("approval", query.ApprovalId);
        }

        var user = await access.RequireAsync(new ProjectId(approval.ProjectId), ProjectPermission.ViewTasks, "approval", approval.Id.ToString(), cancellationToken);
        return user.IsFailure ? Result<ApprovalDto>.Failure(user.Error!) : Result<ApprovalDto>.Success(approval);
    }
}

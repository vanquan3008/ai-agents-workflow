using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.Common;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Authorization;

/// <summary>
/// Kiểm tra quyền theo project và hành động (checklist 1.3). Mọi use case đọc/ghi đều đi qua đây,
/// kể cả đăng ký nhận sự kiện SignalR. Lần bị từ chối được ghi audit kèm lý do.
/// </summary>
public sealed class ProjectAccess(
    ICurrentUser currentUser,
    IProjectRepository projects,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Result<UserId>> RequireAsync(
        ProjectId projectId,
        ProjectPermission permission,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized();
        }

        var project = await projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return Error.NotFound("project", projectId);
        }

        if (project.HasPermission(userId, permission))
        {
            return userId;
        }

        auditLog.Add(AuditEntry.Record(
            Actor.ForUser(userId),
            $"authorize.{permission}",
            resourceType,
            resourceId,
            AuditOutcome.Denied,
            clock.GetUtcNow(),
            projectId: projectId,
            reason: project.RoleOf(userId) is { } role
                ? $"Vai trò {role} không có quyền {permission}."
                : "Người dùng không thuộc project."));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Error.Forbidden();
    }

    public Result<UserId> RequireAuthenticated() =>
        currentUser.UserId is { } userId ? userId : Error.Unauthorized();
}

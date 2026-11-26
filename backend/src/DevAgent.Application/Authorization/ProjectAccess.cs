using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Projects;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Users;

namespace DevAgent.Application.Authorization;

/// <summary>
/// Kiểm tra quyền theo project và hành động (checklist 1.3). Mọi use case đọc/ghi đều đi qua đây,
/// kể cả đăng ký nhận sự kiện SignalR. Super Admin có toàn quyền trên mọi project.
/// Quyền luôn đọc từ DB (không tin claim trong token) để thu hồi quyền có hiệu lực ngay. Lần bị từ chối được ghi audit.
/// </summary>
public sealed class ProjectAccess(
    ICurrentUser currentUser,
    IUserRepository users,
    IProjectRepository projects,
    IProjectQueries projectQueries,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    private User? _cachedUser;

    public async Task<Result<UserId>> RequireAsync(
        ProjectId projectId,
        ProjectPermission permission,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken)
    {
        var user = await CurrentActiveUserAsync(cancellationToken);
        if (user is null)
        {
            return Error.Unauthorized();
        }

        var project = await projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return Error.NotFound("project", projectId);
        }

        if (user.IsSuperAdmin || project.HasPermission(user.Id, permission))
        {
            return user.Id;
        }

        await DenyAsync(
            user.Id,
            $"authorize.{permission}",
            resourceType,
            resourceId,
            projectId,
            project.RoleOf(user.Id) is { } role ? $"Vai trò {role} không có quyền {permission}." : "Người dùng không thuộc project.",
            cancellationToken);
        return Error.Forbidden();
    }

    public async Task<Result<UserId>> RequireAuthenticatedAsync(CancellationToken cancellationToken)
    {
        var user = await CurrentActiveUserAsync(cancellationToken);
        return user is null ? Error.Unauthorized() : user.Id;
    }

    /// <summary>Chỉ Super Admin (quản trị nền tảng).</summary>
    public async Task<Result<User>> RequireSuperAdminAsync(string action, CancellationToken cancellationToken)
    {
        var user = await CurrentActiveUserAsync(cancellationToken);
        if (user is null)
        {
            return Error.Unauthorized();
        }

        if (user.IsSuperAdmin)
        {
            return user;
        }

        await DenyAsync(user.Id, action, "system", "platform", null, "Thao tác chỉ dành cho Super Admin.", cancellationToken);
        return Error.Forbidden("Thao tác chỉ dành cho Super Admin.");
    }

    /// <summary>Các project người dùng hiện tại có quyền <paramref name="permission"/>; Super Admin: mọi project.</summary>
    public async Task<Result<IReadOnlyCollection<ProjectId>>> ProjectsWithPermissionAsync(ProjectPermission permission, CancellationToken cancellationToken)
    {
        var user = await CurrentActiveUserAsync(cancellationToken);
        if (user is null)
        {
            return Error.Unauthorized();
        }

        IReadOnlyCollection<ProjectId> ids = user.IsSuperAdmin
            ? await projectQueries.ListAllProjectIdsAsync(cancellationToken)
            : await projectQueries.ListProjectIdsWithPermissionAsync(user.Id, permission, cancellationToken);
        return Result<IReadOnlyCollection<ProjectId>>.Success(ids);
    }

    /// <summary>Người dùng hiện tại nếu đã đăng nhập và tài khoản còn hoạt động.</summary>
    public async Task<User?> CurrentActiveUserAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return null;
        }

        // Cache trong scope, nhưng chỉ cho đúng người dùng hiện tại (hub có thể gán principal khác trong cùng scope).
        if (_cachedUser?.Id != userId)
        {
            _cachedUser = await users.GetByIdAsync(userId, cancellationToken);
        }

        return _cachedUser is { IsActive: true } ? _cachedUser : null;
    }

    private async Task DenyAsync(UserId userId, string action, string resourceType, string resourceId, ProjectId? projectId, string reason, CancellationToken cancellationToken)
    {
        auditLog.Add(AuditEntry.Record(
            Actor.ForUser(userId),
            action,
            resourceType,
            resourceId,
            AuditOutcome.Denied,
            clock.GetUtcNow(),
            projectId: projectId,
            reason: reason));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

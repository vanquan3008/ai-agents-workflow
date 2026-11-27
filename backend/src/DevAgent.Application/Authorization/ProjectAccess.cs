using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Projects;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Auditing.Repositories;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Projects.Repositories;
using DevAgent.Domain.Users;
using DevAgent.Domain.Users.Repositories;

namespace DevAgent.Application.Authorization;

/// <summary>
/// Project- and action-level authorization. Every read and write use case goes through here, including SignalR
/// subscriptions. Super admins may do everything. Permissions are always read from the database rather than token
/// claims so revocations apply immediately, and every denial is audited.
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
        var authorization = await AuthorizeAsync(projectId, permission, resourceType, resourceId, cancellationToken);
        return authorization.IsSuccess ? authorization.Value.UserId : authorization.Error!;
    }

    /// <summary>
    /// Like <see cref="RequireAsync"/>, but returns the caller's role so further permissions can be checked in memory.
    /// </summary>
    public async Task<Result<ProjectAuthorization>> AuthorizeAsync(
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

        var authorization = new ProjectAuthorization(user.Id, projectId, user.IsSuperAdmin, project.RoleOf(user.Id));
        if (authorization.Can(permission))
        {
            return authorization;
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

    /// <summary>Projects where the current user holds <paramref name="permission"/>; every project for super admins.</summary>
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

    /// <summary>
    /// Scope for list queries: the requested project when given (after a permission check),
    /// otherwise every project the user may view.
    /// </summary>
    public async Task<Result<IReadOnlyCollection<ProjectId>>> ResolveViewableProjectsAsync(Guid? requestedProjectId, CancellationToken cancellationToken)
    {
        if (requestedProjectId is not { } projectGuid)
        {
            return await ProjectsWithPermissionAsync(ProjectPermission.ViewTasks, cancellationToken);
        }

        var projectId = new ProjectId(projectGuid);
        var user = await RequireAsync(projectId, ProjectPermission.ViewTasks, "project", projectId.ToString(), cancellationToken);
        return user.IsFailure
            ? Result<IReadOnlyCollection<ProjectId>>.Failure(user.Error!)
            : Result<IReadOnlyCollection<ProjectId>>.Success([projectId]);
    }

    /// <summary>The signed-in user when the account is still active.</summary>
    public async Task<User?> CurrentActiveUserAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return null;
        }

        // Cached per scope, but only for the same user: a hub can switch principals within one scope.
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

using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Auditing.Repositories;
using DevAgent.Domain.Common;
using DevAgent.Domain.Users;
using DevAgent.Domain.Users.Repositories;

namespace DevAgent.Application.Features.Admin;

public sealed record UpdateUserCommand(Guid UserId, string? DisplayName, string? SystemRole, bool? IsActive);

internal sealed class UpdateUserHandler(
    ProjectAccess access,
    IUserRepository users,
    IUserQueries queries,
    IRefreshTokenRepository refreshTokens,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<UpdateUserCommand, UserAdminDto>
{
    public async Task<Result<UserAdminDto>> HandleAsync(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var admin = await access.RequireSuperAdminAsync("admin.users.update", cancellationToken);
        if (admin.IsFailure)
        {
            return admin.Error!;
        }

        var user = await users.GetByIdAsync(new UserId(command.UserId), cancellationToken);
        if (user is null)
        {
            return Error.NotFound("user", command.UserId);
        }

        SystemRole? newRole = null;
        if (command.SystemRole is not null)
        {
            if (!ApiNames.TryParse<SystemRole>(command.SystemRole, out var parsed))
            {
                return Error.Field("user.role_invalid", "systemRole", "Vai trò hợp lệ: USER, SUPER_ADMIN.");
            }

            newRole = parsed;
        }

        var removesSuperAdmin = user.IsSuperAdmin && user.IsActive && (newRole == SystemRole.User || command.IsActive == false);
        if (removesSuperAdmin && user.Id == admin.Value.Id)
        {
            return Error.Conflict("user.self_demotion", "Không thể tự hạ quyền hoặc vô hiệu hóa chính mình.");
        }

        if (removesSuperAdmin && await users.CountActiveSuperAdminsAsync(cancellationToken) <= 1)
        {
            return Error.Conflict("user.last_super_admin", "Hệ thống phải còn ít nhất một Super Admin đang hoạt động.");
        }

        var now = clock.GetUtcNow();
        if (!string.IsNullOrWhiteSpace(command.DisplayName))
        {
            user.Rename(command.DisplayName);
        }

        if (newRole is { } role)
        {
            user.ChangeRole(role);
        }

        if (command.IsActive is { } active && active != user.IsActive)
        {
            if (active)
            {
                user.Activate();
            }
            else
            {
                user.Deactivate();
                foreach (var token in await refreshTokens.ListActiveByUserAsync(user.Id, cancellationToken))
                {
                    token.Revoke("user_deactivated", now);
                }
            }
        }

        auditLog.Add(AuditEntry.Record(Actor.ForUser(admin.Value.Id), "admin.users.update", "user", user.Id.ToString(), AuditOutcome.Allowed, now,
            reason: $"role={command.SystemRole ?? "-"}, active={command.IsActive?.ToString() ?? "-"}"));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await queries.GetAsync(user.Id, cancellationToken))!;
    }
}

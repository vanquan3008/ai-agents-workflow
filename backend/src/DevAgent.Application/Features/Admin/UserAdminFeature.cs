using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Auth;
using DevAgent.Application.Options;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Common;
using DevAgent.Domain.Users;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.Features.Admin;

public sealed record UserAdminDto(
    Guid Id,
    string Email,
    string DisplayName,
    string SystemRole,
    bool IsActive,
    bool MustChangePassword,
    DateTimeOffset? LockedUntil,
    DateTimeOffset? LastLoginAt,
    int ProjectCount,
    DateTimeOffset CreatedAt);

public interface IUserQueries
{
    Task<PagedResult<UserAdminDto>> ListAsync(string? search, PageRequest page, CancellationToken cancellationToken);

    Task<UserAdminDto?> GetAsync(UserId userId, CancellationToken cancellationToken);
}

internal static class UserErrors
{
    public static Error Field(string code, string field, string message) =>
        Error.Validation(code, message, new Dictionary<string, string[]> { [field] = [message] });
}

// ---------- Danh sách ----------

public sealed record ListUsersQuery(string? Search, int? Page, int? PageSize);

internal sealed class ListUsersHandler(ProjectAccess access, IUserQueries queries) : IQueryHandler<ListUsersQuery, PagedResult<UserAdminDto>>
{
    public async Task<Result<PagedResult<UserAdminDto>>> HandleAsync(ListUsersQuery query, CancellationToken cancellationToken)
    {
        var admin = await access.RequireSuperAdminAsync("admin.users.list", cancellationToken);
        return admin.IsFailure
            ? Result<PagedResult<UserAdminDto>>.Failure(admin.Error!)
            : await queries.ListAsync(query.Search?.Trim(), new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}

// ---------- Tạo ----------

/// <param name="TemporaryPassword">Mật khẩu tạm; người dùng buộc đổi ở lần đăng nhập đầu.</param>
public sealed record CreateUserCommand(string Email, string DisplayName, string SystemRole, string TemporaryPassword);

internal sealed class CreateUserHandler(
    ProjectAccess access,
    IUserRepository users,
    IUserQueries queries,
    IPasswordHasher hasher,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    IOptions<AuthPolicyOptions> policy,
    TimeProvider clock) : ICommandHandler<CreateUserCommand, UserAdminDto>
{
    public async Task<Result<UserAdminDto>> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var admin = await access.RequireSuperAdminAsync("admin.users.create", cancellationToken);
        if (admin.IsFailure)
        {
            return admin.Error!;
        }

        if (!ApiNames.TryParse<SystemRole>(command.SystemRole, out var role))
        {
            return UserErrors.Field("user.role_invalid", "systemRole", "Vai trò hợp lệ: USER, SUPER_ADMIN.");
        }

        string email;
        try
        {
            email = User.NormalizeEmail(command.Email);
        }
        catch (DomainException)
        {
            return UserErrors.Field("user.email_invalid", "email", "Email không hợp lệ.");
        }

        if (await users.FindByEmailAsync(email, cancellationToken) is not null)
        {
            return UserErrors.Field("user.email_taken", "email", "Email đã được dùng.");
        }

        if (string.IsNullOrWhiteSpace(command.DisplayName))
        {
            return UserErrors.Field("user.display_name_required", "displayName", "Nhập tên hiển thị.");
        }

        var passwordErrors = PasswordPolicy.Validate(command.TemporaryPassword, email, policy.Value);
        if (passwordErrors.Length > 0)
        {
            return Error.Validation("auth.password_weak", "Mật khẩu tạm chưa đạt yêu cầu.", new Dictionary<string, string[]> { ["temporaryPassword"] = passwordErrors });
        }

        var now = clock.GetUtcNow();
        var user = User.Create(email, command.DisplayName, role, now);
        user.SetPassword(hasher.Hash(command.TemporaryPassword), mustChange: true);
        users.Add(user);
        auditLog.Add(AuditEntry.Record(Actor.ForUser(admin.Value.Id), "admin.users.create", "user", user.Id.ToString(), AuditOutcome.Allowed, now,
            reason: $"{email} ({role})"));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await queries.GetAsync(user.Id, cancellationToken))!;
    }
}

// ---------- Cập nhật (tên, vai trò, kích hoạt) ----------

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
                return UserErrors.Field("user.role_invalid", "systemRole", "Vai trò hợp lệ: USER, SUPER_ADMIN.");
            }

            newRole = parsed;
        }

        var removesAdmin = user.IsSuperAdmin && user.IsActive && (newRole == SystemRole.User || command.IsActive == false);
        if (removesAdmin && user.Id == admin.Value.Id)
        {
            return Error.Conflict("user.self_demotion", "Không thể tự hạ quyền hoặc vô hiệu hóa chính mình.");
        }

        if (removesAdmin && await users.CountActiveSuperAdminsAsync(cancellationToken) <= 1)
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

// ---------- Đặt lại mật khẩu ----------

public sealed record ResetUserPasswordCommand(Guid UserId, string TemporaryPassword);

internal sealed class ResetUserPasswordHandler(
    ProjectAccess access,
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher hasher,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    IOptions<AuthPolicyOptions> policy,
    TimeProvider clock) : ICommandHandler<ResetUserPasswordCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(ResetUserPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var admin = await access.RequireSuperAdminAsync("admin.users.reset_password", cancellationToken);
        if (admin.IsFailure)
        {
            return admin.Error!;
        }

        var user = await users.GetByIdAsync(new UserId(command.UserId), cancellationToken);
        if (user is null)
        {
            return Error.NotFound("user", command.UserId);
        }

        var errors = PasswordPolicy.Validate(command.TemporaryPassword, user.Email, policy.Value);
        if (errors.Length > 0)
        {
            return Error.Validation("auth.password_weak", "Mật khẩu tạm chưa đạt yêu cầu.", new Dictionary<string, string[]> { ["temporaryPassword"] = errors });
        }

        var now = clock.GetUtcNow();
        user.SetPassword(hasher.Hash(command.TemporaryPassword), mustChange: true);
        foreach (var token in await refreshTokens.ListActiveByUserAsync(user.Id, cancellationToken))
        {
            token.Revoke("password_reset", now);
        }

        auditLog.Add(AuditEntry.Record(Actor.ForUser(admin.Value.Id), "admin.users.reset_password", "user", user.Id.ToString(), AuditOutcome.Allowed, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

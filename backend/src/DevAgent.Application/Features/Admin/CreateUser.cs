using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Auth;
using DevAgent.Application.Options;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Auditing.Repositories;
using DevAgent.Domain.Common;
using DevAgent.Domain.Users;
using DevAgent.Domain.Users.Repositories;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.Features.Admin;

/// <param name="TemporaryPassword">Temporary password; the user must change it on first sign-in.</param>
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
            return Error.Field("user.role_invalid", "systemRole", "Vai trò hợp lệ: USER, SUPER_ADMIN.");
        }

        string email;
        try
        {
            email = User.NormalizeEmail(command.Email);
        }
        catch (DomainException)
        {
            return Error.Field("user.email_invalid", "email", "Email không hợp lệ.");
        }

        if (await users.FindByEmailAsync(email, cancellationToken) is not null)
        {
            return Error.Field("user.email_taken", "email", "Email đã được dùng.");
        }

        if (string.IsNullOrWhiteSpace(command.DisplayName))
        {
            return Error.Field("user.display_name_required", "displayName", "Nhập tên hiển thị.");
        }

        var passwordErrors = PasswordPolicy.Validate(command.TemporaryPassword, email, policy.Value);
        if (passwordErrors.Length > 0)
        {
            return Error.Validation("auth.password_weak", "Mật khẩu tạm chưa đạt yêu cầu.", new Dictionary<string, string[]> { ["temporaryPassword"] = passwordErrors });
        }

        var now = clock.GetUtcNow();
        var user = User.Create(email, command.DisplayName, role, now);
        user.SetPassword(hasher.Hash(command.TemporaryPassword), mustChange: true, now);
        users.Add(user);
        auditLog.Add(AuditEntry.Record(Actor.ForUser(admin.Value.Id), "admin.users.create", "user", user.Id.ToString(), AuditOutcome.Allowed, now,
            reason: $"{email} ({role})"));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await queries.GetAsync(user.Id, cancellationToken))!;
    }
}

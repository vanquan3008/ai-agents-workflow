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

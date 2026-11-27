using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Options;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Auditing.Repositories;
using DevAgent.Domain.Common;
using DevAgent.Domain.Users;
using DevAgent.Domain.Users.Repositories;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.Features.Auth;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword, ClientInfo Client);

/// <summary>
/// Rotates the security stamp (old access tokens stop working), revokes every other session,
/// then issues a fresh session for the current device.
/// </summary>
internal sealed class ChangePasswordHandler(
    ProjectAccess access,
    IPasswordHasher hasher,
    IRefreshTokenRepository refreshTokens,
    SessionIssuer sessions,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    IOptions<AuthPolicyOptions> policy,
    TimeProvider clock) : ICommandHandler<ChangePasswordCommand, AuthSession>
{
    public async Task<Result<AuthSession>> HandleAsync(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var user = await access.CurrentActiveUserAsync(cancellationToken);
        if (user?.PasswordHash is null)
        {
            return Error.Unauthorized();
        }

        if (hasher.Verify(user.PasswordHash, command.CurrentPassword ?? string.Empty) == PasswordVerification.Failed)
        {
            return Error.Field("auth.current_password_invalid", "currentPassword", "Mật khẩu hiện tại không đúng.");
        }

        var errors = PasswordPolicy.Validate(command.NewPassword, user.Email, policy.Value).ToList();
        if (command.NewPassword == command.CurrentPassword)
        {
            errors.Add("Mật khẩu mới phải khác mật khẩu hiện tại.");
        }

        if (errors.Count > 0)
        {
            return Error.Validation("auth.password_weak", "Mật khẩu mới chưa đạt yêu cầu.", new Dictionary<string, string[]> { ["newPassword"] = [.. errors] });
        }

        var now = clock.GetUtcNow();
        user.SetPassword(hasher.Hash(command.NewPassword), mustChange: false, now);
        foreach (var token in await refreshTokens.ListActiveByUserAsync(user.Id, cancellationToken))
        {
            token.Revoke("password_changed", now);
        }

        var session = sessions.Issue(user, familyId: null, command.Client, out _);
        auditLog.Add(AuditEntry.Record(Actor.ForUser(user.Id), "auth.password.change", "user", user.Id.ToString(), AuditOutcome.Allowed, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session;
    }
}

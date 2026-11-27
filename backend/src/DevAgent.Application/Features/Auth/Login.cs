using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.Common;
using DevAgent.Application.Options;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Auditing.Repositories;
using DevAgent.Domain.Common;
using DevAgent.Domain.Users;
using DevAgent.Domain.Users.Repositories;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.Features.Auth;

public sealed record LoginCommand(string Email, string Password, ClientInfo Client);

internal sealed class LoginHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    SessionIssuer sessions,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    IOptions<AuthPolicyOptions> policy,
    TimeProvider clock) : ICommandHandler<LoginCommand, AuthSession>
{
    // Hash computed once and verified against for unknown emails, so response time does not reveal whether an email exists.
    private static string? _timingEqualizerHash;

    private static readonly Error InvalidCredentials =
        new("auth.invalid_credentials", "Email hoáº·c máº­t kháº©u khÃ´ng Ä‘Ãºng.", ErrorType.Unauthorized);

    public async Task<Result<AuthSession>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = clock.GetUtcNow();
        var email = (command.Email ?? string.Empty).Trim().ToLowerInvariant();
        var user = email.Length == 0 ? null : await users.FindByEmailAsync(email, cancellationToken);

        if (user?.PasswordHash is null || !user.IsActive)
        {
            _timingEqualizerHash ??= hasher.Hash("timing-equalizer-0");
            _ = hasher.Verify(_timingEqualizerHash, command.Password ?? string.Empty);
            auditLog.Add(AuditEntry.Record(Actor.System, "auth.login", "user", email.Length == 0 ? "unknown" : email, AuditOutcome.Denied, now,
                reason: user is null ? "Email khÃ´ng tá»“n táº¡i." : "TÃ i khoáº£n bá»‹ vÃ´ hiá»‡u hÃ³a hoáº·c chÆ°a cÃ³ máº­t kháº©u."));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return InvalidCredentials;
        }

        if (user.IsLockedOut(now))
        {
            return new Error("auth.locked_out", $"TÃ i khoáº£n táº¡m khÃ³a do Ä‘Äƒng nháº­p sai nhiá»u láº§n. Thá»­ láº¡i sau {user.LockoutEndsAt:HH:mm} (UTC).", ErrorType.Unauthorized);
        }

        var verification = hasher.Verify(user.PasswordHash, command.Password ?? string.Empty);
        if (verification == PasswordVerification.Failed)
        {
            user.RecordFailedLogin(now, policy.Value.MaxFailedAttempts, TimeSpan.FromMinutes(policy.Value.LockoutMinutes));
            auditLog.Add(AuditEntry.Record(Actor.ForUser(user.Id), "auth.login", "user", user.Id.ToString(), AuditOutcome.Denied, now,
                reason: user.IsLockedOut(now) ? "Sai máº­t kháº©u; tÃ i khoáº£n bá»‹ khÃ³a táº¡m thá»i." : "Sai máº­t kháº©u."));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return InvalidCredentials;
        }

        if (verification == PasswordVerification.SuccessRehashNeeded)
        {
            user.UpgradePasswordHash(hasher.Hash(command.Password!));
        }

        user.RecordSuccessfulLogin(now);
        var session = sessions.Issue(user, familyId: null, command.Client, out _);
        auditLog.Add(AuditEntry.Record(Actor.ForUser(user.Id), "auth.login", "user", user.Id.ToString(), AuditOutcome.Allowed, now,
            reason: command.Client.IpAddress));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session;
    }
}

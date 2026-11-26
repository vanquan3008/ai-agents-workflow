using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.Common;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Auditing.Repositories;
using DevAgent.Domain.Common;
using DevAgent.Domain.Users;
using DevAgent.Domain.Users.Repositories;

namespace DevAgent.Application.Features.Auth;

public sealed record RefreshSessionCommand(string RefreshToken, ClientInfo Client);

internal sealed class RefreshSessionHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ITokenService tokens,
    SessionIssuer sessions,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<RefreshSessionCommand, AuthSession>
{
    private static readonly Error SessionExpired = new("auth.session_expired", "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.", ErrorType.Unauthorized);

    public async Task<Result<AuthSession>> HandleAsync(RefreshSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return SessionExpired;
        }

        var now = clock.GetUtcNow();
        var token = await refreshTokens.FindByHashAsync(tokens.HashRefreshToken(command.RefreshToken), cancellationToken);
        if (token is null)
        {
            return SessionExpired;
        }

        if (!token.IsActive(now))
        {
            if (token.WasRotated)
            {
                // A rotated token was presented again, which suggests theft: revoke the whole family.
                foreach (var sibling in await refreshTokens.ListActiveByFamilyAsync(token.FamilyId, cancellationToken))
                {
                    sibling.Revoke("reuse_detected", now);
                }

                auditLog.Add(AuditEntry.Record(Actor.ForUser(token.UserId), "auth.refresh.reuse_detected", "user", token.UserId.ToString(),
                    AuditOutcome.Denied, now, reason: command.Client.IpAddress));
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return SessionExpired;
        }

        var user = await users.GetByIdAsync(token.UserId, cancellationToken);
        if (user is not { IsActive: true })
        {
            token.Revoke("user_inactive", now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return SessionExpired;
        }

        var session = sessions.Issue(user, token.FamilyId, command.Client, out var next);
        token.RotateTo(next.Id, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session;
    }
}

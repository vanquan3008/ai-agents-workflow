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

public sealed record RefreshSessionCommand(string RefreshToken, ClientInfo Client)
{
    public const string SupersededErrorCode = "auth.refresh_superseded";
}

internal sealed class RefreshSessionHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ITokenService tokens,
    SessionIssuer sessions,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    IOptions<AuthPolicyOptions> policy,
    TimeProvider clock) : ICommandHandler<RefreshSessionCommand, AuthSession>
{
    private static readonly Error SessionExpired = new("auth.session_expired", "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.", ErrorType.Unauthorized);

    /// <summary>Another request rotated this token first; the browser already holds the successor cookie and may retry.</summary>
    private static readonly Error Superseded = new(RefreshSessionCommand.SupersededErrorCode, "Phiên vừa được làm mới bởi một yêu cầu khác. Thử lại.", ErrorType.Unauthorized);

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
            var parallelRefreshWindow = TimeSpan.FromSeconds(policy.Value.ParallelRefreshWindowSeconds);
            if (token.WasRotatedSince(now - parallelRefreshWindow) && await IsParallelRefreshAsync(token, command.Client, cancellationToken))
            {
                return Superseded;
            }

            if (token.WasRotated)
            {
                // A rotated token was presented again, which suggests theft: revoke the whole family.
                foreach (var sibling in await refreshTokens.ListActiveByFamilyAsync(token.FamilyId, cancellationToken))
                {
                    sibling.Revoke("reuse_detected", now);
                }

                auditLog.Add(AuditEntry.Record(Actor.ForUser(token.UserId), "auth.refresh.reuse_detected", "user", token.UserId.ToString(),
                    AuditOutcome.Denied, now, reason: command.Client.IpAddress));
                await SaveIgnoringRaceAsync(cancellationToken);
            }

            return SessionExpired;
        }

        var user = await users.GetByIdAsync(token.UserId, cancellationToken);
        if (user is not { IsActive: true })
        {
            token.Revoke("user_inactive", now);
            await SaveIgnoringRaceAsync(cancellationToken);
            return SessionExpired;
        }

        var session = sessions.Issue(user, token.FamilyId, command.Client, out var next);
        token.RotateTo(next.Id, now);

        // The successor insert and the rotation commit together, guarded by the token's xmin: when a parallel request
        // rotated the same token first, nothing is written and no second session exists.
        return await SaveIgnoringRaceAsync(cancellationToken) ? session : Superseded;
    }

    /// <summary>
    /// A parallel refresh comes from the browser that won the rotation, so its user agent matches the successor's.
    /// A different client inside the window is treated as theft: otherwise a thief who rotated first would leave the
    /// victim only "superseded" answers and the family would never be revoked. IP is not compared because one browser
    /// can reach the API over IPv4 and IPv6 at once, and the proxy may hide client addresses.
    /// </summary>
    private async Task<bool> IsParallelRefreshAsync(RefreshToken token, ClientInfo client, CancellationToken cancellationToken) =>
        token.ReplacedBy is { } successorId
        && await refreshTokens.GetByIdAsync(successorId, cancellationToken) is { } successor
        && successor.WasIssuedTo(client.UserAgent);

    private async Task<bool> SaveIgnoringRaceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (ConcurrencyConflictException)
        {
            return false;
        }
    }
}

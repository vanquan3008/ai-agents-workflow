using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Options;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Common;
using DevAgent.Domain.Users;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.Features.Auth;

public sealed record CurrentUserDto(Guid Id, string Email, string DisplayName, string SystemRole, bool MustChangePassword)
{
    public static CurrentUserDto From(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new(user.Id.Value, user.Email, user.DisplayName, ApiNames.ToApi(user.SystemRole), user.MustChangePassword);
    }
}

/// <summary>Kết quả đăng nhập/refresh. Refresh token thô chỉ để API đặt vào cookie httpOnly, không trả trong body.</summary>
public sealed record AuthSession(AccessToken AccessToken, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, CurrentUserDto User);

/// <summary>Ngữ cảnh client để ghi vào refresh token và audit.</summary>
public sealed record ClientInfo(string? IpAddress, string? UserAgent);

/// <summary>Chính sách mật khẩu dùng chung cho đổi mật khẩu, tạo người dùng, đặt lại mật khẩu và seed.</summary>
public static class PasswordPolicy
{
    public static string[] Validate(string? password, string? email, AuthPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();
        if (string.IsNullOrEmpty(password) || password.Length < options.PasswordMinLength)
        {
            errors.Add($"Mật khẩu tối thiểu {options.PasswordMinLength} ký tự.");
        }
        else
        {
            if (password.Length > 128)
            {
                errors.Add("Mật khẩu tối đa 128 ký tự.");
            }

            if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            {
                errors.Add("Mật khẩu phải có cả chữ và số.");
            }

            if (email is not null && password.Contains(email.Split('@')[0], StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Mật khẩu không được chứa tên email.");
            }
        }

        return [.. errors];
    }
}

/// <summary>Phát hành phiên đăng nhập (access token + refresh token mới) và ghi refresh token.</summary>
internal sealed class SessionIssuer(ITokenService tokens, IRefreshTokenRepository refreshTokens, IOptions<AuthPolicyOptions> policy, TimeProvider clock)
{
    public AuthSession Issue(User user, Guid? familyId, ClientInfo client, out RefreshToken stored)
    {
        var now = clock.GetUtcNow();
        var refresh = tokens.CreateRefreshToken();
        stored = RefreshToken.Issue(user.Id, refresh.Hash, familyId, now, TimeSpan.FromDays(policy.Value.RefreshTokenDays), client.IpAddress, client.UserAgent);
        refreshTokens.Add(stored);
        return new AuthSession(tokens.CreateAccessToken(user), refresh.Raw, stored.ExpiresAt, CurrentUserDto.From(user));
    }
}

// ---------- Đăng nhập ----------

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
    // Băm giả (tính một lần) để thời gian phản hồi không lộ email có tồn tại hay không.
    private static string? s_dummyHash;

    private static readonly Error InvalidCredentials =
        new("auth.invalid_credentials", "Email hoặc mật khẩu không đúng.", ErrorType.Unauthorized);

    public async Task<Result<AuthSession>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = clock.GetUtcNow();
        var email = (command.Email ?? string.Empty).Trim().ToLowerInvariant();
        var user = email.Length == 0 ? null : await users.FindByEmailAsync(email, cancellationToken);

        if (user?.PasswordHash is null || !user.IsActive)
        {
            s_dummyHash ??= hasher.Hash("timing-equalizer-0");
            _ = hasher.Verify(s_dummyHash, command.Password ?? string.Empty);
            auditLog.Add(AuditEntry.Record(Actor.System, "auth.login", "user", email.Length == 0 ? "unknown" : email, AuditOutcome.Denied, now,
                reason: user is null ? "Email không tồn tại." : "Tài khoản bị vô hiệu hóa hoặc chưa có mật khẩu."));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return InvalidCredentials;
        }

        if (user.IsLockedOut(now))
        {
            return new Error("auth.locked_out", $"Tài khoản tạm khóa do đăng nhập sai nhiều lần. Thử lại sau {user.LockoutEndsAt:HH:mm} (UTC).", ErrorType.Unauthorized);
        }

        var verification = hasher.Verify(user.PasswordHash, command.Password ?? string.Empty);
        if (verification == PasswordVerification.Failed)
        {
            user.RecordFailedLogin(now, policy.Value.MaxFailedAttempts, TimeSpan.FromMinutes(policy.Value.LockoutMinutes));
            auditLog.Add(AuditEntry.Record(Actor.ForUser(user.Id), "auth.login", "user", user.Id.ToString(), AuditOutcome.Denied, now,
                reason: user.IsLockedOut(now) ? "Sai mật khẩu; tài khoản bị khóa tạm thời." : "Sai mật khẩu."));
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

// ---------- Refresh (xoay vòng) ----------

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
                // Token đã thay thế mà vẫn bị dùng lại: có thể bị đánh cắp. Thu hồi cả họ token.
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

// ---------- Đăng xuất ----------

public sealed record LogoutCommand(string? RefreshToken);

internal sealed class LogoutHandler(IRefreshTokenRepository refreshTokens, ITokenService tokens, IUnitOfWork unitOfWork, TimeProvider clock)
    : ICommandHandler<LogoutCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(LogoutCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return true;
        }

        var token = await refreshTokens.FindByHashAsync(tokens.HashRefreshToken(command.RefreshToken), cancellationToken);
        if (token is not null)
        {
            var now = clock.GetUtcNow();
            foreach (var sibling in await refreshTokens.ListActiveByFamilyAsync(token.FamilyId, cancellationToken))
            {
                sibling.Revoke("logout", now);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}

// ---------- Thông tin người dùng hiện tại ----------

public sealed record GetCurrentUserQuery;

internal sealed class GetCurrentUserHandler(ProjectAccess access) : IQueryHandler<GetCurrentUserQuery, CurrentUserDto>
{
    public async Task<Result<CurrentUserDto>> HandleAsync(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        var user = await access.CurrentActiveUserAsync(cancellationToken);
        return user is null ? Error.Unauthorized() : CurrentUserDto.From(user);
    }
}

// ---------- Đổi mật khẩu ----------

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword, ClientInfo Client);

/// <summary>
/// Đổi mật khẩu: đổi security stamp (access token cũ mất hiệu lực), thu hồi mọi phiên khác,
/// rồi cấp phiên mới cho thiết bị đang dùng.
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
            return Error.Validation("auth.current_password_invalid", "Mật khẩu hiện tại không đúng.",
                new Dictionary<string, string[]> { ["currentPassword"] = ["Mật khẩu hiện tại không đúng."] });
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
        user.SetPassword(hasher.Hash(command.NewPassword), mustChange: false);
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

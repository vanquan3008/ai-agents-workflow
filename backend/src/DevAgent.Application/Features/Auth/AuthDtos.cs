using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.Common;
using DevAgent.Domain.Users;

namespace DevAgent.Application.Features.Auth;

public sealed record CurrentUserDto(Guid Id, string Email, string DisplayName, string SystemRole, bool MustChangePassword)
{
    public static CurrentUserDto From(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new(user.Id.Value, user.Email, user.DisplayName, ApiNames.ToApi(user.SystemRole), user.MustChangePassword);
    }
}

/// <summary>Result of sign-in or refresh. The raw refresh token is only for the API to set as an HttpOnly cookie.</summary>
public sealed record AuthSession(AccessToken AccessToken, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, CurrentUserDto User);

/// <summary>Client context recorded on refresh tokens and audit entries.</summary>
public sealed record ClientInfo(string? IpAddress, string? UserAgent);

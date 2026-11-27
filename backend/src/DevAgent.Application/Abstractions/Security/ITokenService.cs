using DevAgent.Domain.Users;

namespace DevAgent.Application.Abstractions.Security;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Raw value (returned to the client only) and hash (stored only).</summary>
public sealed record RefreshTokenValue(string Raw, string Hash);

/// <summary>Issues tokens. Implemented by the host (JWT) so the application layer stays free of token libraries.</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    RefreshTokenValue CreateRefreshToken();

    string HashRefreshToken(string raw);
}

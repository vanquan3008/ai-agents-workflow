using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.Options;
using DevAgent.Domain.Users;
using DevAgent.Domain.Users.Repositories;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.Features.Auth;

/// <summary>Issues a session (access token plus a new refresh token) and stores the refresh token.</summary>
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

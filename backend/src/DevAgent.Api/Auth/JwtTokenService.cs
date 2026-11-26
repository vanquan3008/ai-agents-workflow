using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.Common;
using DevAgent.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DevAgent.Api.Auth;

internal static class DevAgentClaims
{
    public const string SecurityStamp = "stamp";
    public const string MustChangePassword = "pwd_change";
    public const string Role = "role";
}

/// <summary>
/// Cấp access token JWT ký HS256 và refresh token ngẫu nhiên 256 bit (chỉ lưu SHA-256 của nó).
/// Token mang security stamp; đổi mật khẩu/vai trò/khóa tài khoản làm token cũ bị từ chối ở bước xác thực.
/// </summary>
internal sealed class JwtTokenService(IOptions<AuthOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();

    public static SymmetricSecurityKey SigningKey(JwtOptions jwt) => new(Encoding.UTF8.GetBytes(jwt.SigningKey!));

    public AccessToken CreateAccessToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var jwt = options.Value.Jwt;
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(jwt.AccessTokenMinutes);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(SigningKey(jwt), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
                new Claim(DevAgentClaims.Role, ApiNames.ToApi(user.SystemRole)),
                new Claim(DevAgentClaims.SecurityStamp, user.SecurityStamp),
                new Claim(DevAgentClaims.MustChangePassword, user.MustChangePassword ? "true" : "false"),
            ]),
        });

        return new AccessToken(token, expires);
    }

    public RefreshTokenValue CreateRefreshToken()
    {
        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return new RefreshTokenValue(raw, HashRefreshToken(raw));
    }

    public string HashRefreshToken(string raw) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw ?? string.Empty)));
}

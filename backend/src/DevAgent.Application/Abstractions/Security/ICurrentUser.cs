using DevAgent.Domain.Common;
using DevAgent.Domain.Users;

namespace DevAgent.Application.Abstractions.Security;

/// <summary>
/// Người dùng của request hiện tại. Danh tính đến từ access token đã xác thực, không bao giờ từ payload.
/// Trong Worker, giá trị là null (hành động mang danh nghĩa hệ thống/agent).
/// </summary>
public interface ICurrentUser
{
    UserId? UserId { get; }

    bool IsAuthenticated => UserId.HasValue;
}

/// <summary>Mã hóa đối xứng cho credential lưu trong DB (checklist 2A: lưu credential có mã hóa).</summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}

public enum PasswordVerification
{
    Failed = 0,
    Success = 1,

    /// <summary>Đúng mật khẩu nhưng chuỗi băm dùng tham số cũ: nên băm lại.</summary>
    SuccessRehashNeeded = 2,
}

/// <summary>Băm và kiểm tra mật khẩu. Hiện thực dùng thuật toán chậm có muối (PBKDF2) và so sánh thời gian hằng.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerification Verify(string passwordHash, string password);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Refresh token: giá trị thô (chỉ trả cho client) và băm (chỉ lưu DB).</summary>
public sealed record RefreshTokenValue(string Raw, string Hash);

/// <summary>Phát hành token. Hiện thực ở tầng host (JWT) để Application không phụ thuộc thư viện token.</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    RefreshTokenValue CreateRefreshToken();

    string HashRefreshToken(string raw);
}

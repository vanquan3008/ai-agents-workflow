using System.ComponentModel.DataAnnotations;

namespace DevAgent.Application.Options;

/// <summary>Chính sách đăng nhập: mật khẩu, khóa tài khoản và thời hạn phiên (refresh token).</summary>
public sealed class AuthPolicyOptions
{
    public const string SectionName = "Auth:Policy";

    [Range(8, 128)]
    public int PasswordMinLength { get; set; } = 10;

    /// <summary>Số lần đăng nhập sai liên tiếp trước khi khóa tạm thời.</summary>
    [Range(3, 50)]
    public int MaxFailedAttempts { get; set; } = 5;

    [Range(1, 24 * 60)]
    public int LockoutMinutes { get; set; } = 15;

    /// <summary>Thời hạn phiên đăng nhập (refresh token xoay vòng).</summary>
    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 7;
}

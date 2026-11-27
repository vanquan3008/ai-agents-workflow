using System.ComponentModel.DataAnnotations;

namespace DevAgent.Application.Options;

/// <summary>Sign-in policy: password rules, lockout and session (refresh token) lifetime.</summary>
public sealed class AuthPolicyOptions
{
    public const string SectionName = "Auth:Policy";

    [Range(8, 128)]
    public int PasswordMinLength { get; set; } = 10;

    /// <summary>Consecutive failed sign-ins before a temporary lockout.</summary>
    [Range(3, 50)]
    public int MaxFailedAttempts { get; set; } = 5;

    [Range(1, 24 * 60)]
    public int LockoutMinutes { get; set; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 7;
}

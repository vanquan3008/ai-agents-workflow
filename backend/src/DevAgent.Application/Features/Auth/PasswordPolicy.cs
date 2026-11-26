using DevAgent.Application.Options;

namespace DevAgent.Application.Features.Auth;

/// <summary>Password rules shared by password change, user creation, password reset and seeding.</summary>
public static class PasswordPolicy
{
    public const int MaxLength = 128;

    public static string[] Validate(string? password, string? email, AuthPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();
        if (string.IsNullOrEmpty(password) || password.Length < options.PasswordMinLength)
        {
            errors.Add($"Mật khẩu tối thiểu {options.PasswordMinLength} ký tự.");
            return [.. errors];
        }

        if (password.Length > MaxLength)
        {
            errors.Add($"Mật khẩu tối đa {MaxLength} ký tự.");
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            errors.Add("Mật khẩu phải có cả chữ và số.");
        }

        if (email is not null && password.Contains(email.Split('@')[0], StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Mật khẩu không được chứa tên email.");
        }

        return [.. errors];
    }
}

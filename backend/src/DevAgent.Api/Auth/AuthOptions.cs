using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.Extensions.Options;

namespace DevAgent.Api.Auth;

/// <summary>Phát hành và kiểm tra access token JWT (HS256) do chính DevAgent cấp.</summary>
public sealed class JwtOptions
{
    [Required]
    public string Issuer { get; set; } = "devagent";

    [Required]
    public string Audience { get; set; } = "devagent-api";

    /// <summary>Secret ký token, tối thiểu 32 byte. Cấp qua biến môi trường <c>Auth__Jwt__SigningKey</c>.</summary>
    public string? SigningKey { get; set; }

    /// <summary>Access token ngắn hạn; phiên dài hơn do refresh token xoay vòng đảm nhận.</summary>
    [Range(1, 120)]
    public int AccessTokenMinutes { get; set; } = 15;
}

public sealed class RefreshCookieOptions
{
    [Required]
    public string Name { get; set; } = "devagent_rt";

    /// <summary>Chỉ gửi qua HTTPS. Tắt ở môi trường phát triển chạy http://localhost.</summary>
    public bool Secure { get; set; } = true;
}

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public JwtOptions Jwt { get; set; } = new();

    public RefreshCookieOptions RefreshCookie { get; set; } = new();
}

internal sealed class AuthOptionsValidator(IHostEnvironment environment) : IValidateOptions<AuthOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Jwt.SigningKey) || Encoding.UTF8.GetByteCount(options.Jwt.SigningKey) < 32)
        {
            failures.Add("Auth:Jwt:SigningKey là bắt buộc và tối thiểu 32 byte (tạo: openssl rand -base64 48; cấp qua env Auth__Jwt__SigningKey).");
        }

        if (!options.RefreshCookie.Secure && !environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            failures.Add("Auth:RefreshCookie:Secure chỉ được tắt ở Development/Testing.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

public sealed class CorsOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "dashboard";

    public string[] AllowedOrigins { get; set; } = [];
}

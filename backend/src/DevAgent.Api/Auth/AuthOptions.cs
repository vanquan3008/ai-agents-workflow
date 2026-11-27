using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.Extensions.Options;

namespace DevAgent.Api.Auth;

/// <summary>Settings for issuing and validating DevAgent's own HS256 JWT access tokens.</summary>
public sealed class JwtOptions
{
    [Required]
    public string Issuer { get; set; } = "devagent";

    [Required]
    public string Audience { get; set; } = "devagent-api";

    /// <summary>Signing secret, at least 32 bytes, supplied through <c>Auth__Jwt__SigningKey</c>.</summary>
    public string? SigningKey { get; set; }

    /// <summary>Short-lived access token; longer sessions rely on rotating refresh tokens.</summary>
    [Range(1, 120)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(0, 60)]
    public int ClockSkewSeconds { get; set; } = 30;
}

public sealed class RefreshCookieOptions
{
    [Required]
    public string Name { get; set; } = "devagent_rt";

    /// <summary>HTTPS-only cookie. Disable for local development over http://localhost.</summary>
    public bool Secure { get; set; } = true;
}

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    [ValidateObjectMembers]
    public JwtOptions Jwt { get; set; } = new();

    [ValidateObjectMembers]
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

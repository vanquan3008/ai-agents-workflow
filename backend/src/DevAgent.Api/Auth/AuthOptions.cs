using System.ComponentModel.DataAnnotations;

namespace DevAgent.Api.Auth;

public sealed class JwtOptions
{
    /// <summary>OIDC authority (vd https://login.example.com/realms/devagent). Bắt buộc khi không dùng xác thực Development.</summary>
    public string? Authority { get; set; }

    [Required]
    public string Audience { get; set; } = "devagent-api";

    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>Claim chứa DevAgent user id (Guid). IdP ánh xạ danh tính về user DevAgent.</summary>
    [Required]
    public string UserIdClaim { get; set; } = "devagent_uid";
}

public sealed class DevelopmentAuthOptions
{
    /// <summary>
    /// Xác thực giả lập bằng header <c>X-DevAgent-User</c> (hoặc query <c>dev_user</c> cho SignalR).
    /// Chỉ được bật ở Development/Testing — ứng dụng từ chối khởi động nếu bật ở môi trường khác.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>User dùng khi request không gửi header. Mặc định là Owner trong dữ liệu seed.</summary>
    public Guid? DefaultUserId { get; set; }
}

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public JwtOptions Jwt { get; set; } = new();

    public DevelopmentAuthOptions Development { get; set; } = new();
}

public sealed class CorsOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "dashboard";

    public string[] AllowedOrigins { get; set; } = [];
}

using System.ComponentModel.DataAnnotations;

namespace DevAgent.Infrastructure.Options;

/// <summary>Cấu hình PostgreSQL. Chuỗi kết nối nằm ở <c>ConnectionStrings:Postgres</c> (secret, cấp qua env/user-secrets).</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public const string ConnectionStringName = "Postgres";

    [Range(5, 600)]
    public int CommandTimeoutSeconds { get; set; } = 30;

    [Range(0, 10)]
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>Áp migration khi API khởi động. Bật ở dev/compose; production chạy migration bundle riêng.</summary>
    public bool ApplyMigrationsOnStartup { get; set; }

    /// <summary>Seed user/project/repo mẫu. Chỉ có hiệu lực ở môi trường Development.</summary>
    public bool SeedDevelopmentData { get; set; }

    /// <summary>Log tham số SQL. Không bao giờ bật ở production.</summary>
    public bool EnableSensitiveDataLogging { get; set; }
}

/// <summary>Hàng đợi job bền vững trên PostgreSQL (checklist 0.8, 1.4).</summary>
public sealed class JobQueueOptions
{
    public const string SectionName = "JobQueue";

    [Range(1, 50)]
    public int MaxAttempts { get; set; } = 5;

    [Range(1, 3600)]
    public int BaseRetryDelaySeconds { get; set; } = 10;

    [Range(1, 86_400)]
    public int MaxRetryDelaySeconds { get; set; } = 600;
}

public enum AiProvider
{
    /// <summary>Kết quả cố định, không gọi mạng. Dùng cho CI và phát triển (checklist 1.5).</summary>
    Fake = 1,
    Anthropic = 2,
}

public sealed class ModelPrice
{
    [Range(0, 10_000)]
    public decimal InputPerMillion { get; set; }

    [Range(0, 10_000)]
    public decimal OutputPerMillion { get; set; }

    [Range(0, 10_000)]
    public decimal CacheReadPerMillion { get; set; }
}

public sealed class AnthropicProviderOptions
{
    /// <summary>Secret. Cấp qua biến môi trường <c>Ai__Anthropic__ApiKey</c> hoặc user-secrets.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Để trống dùng endpoint mặc định của SDK.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Bật fallback phía server khi model từ chối (refusal) — beta server-side-fallback.</summary>
    public bool EnableServerSideFallback { get; set; } = true;
}

/// <summary>
/// Cấu hình nhà cung cấp AI. Đổi nhà cung cấp chỉ bằng <see cref="Provider"/> và thêm adapter;
/// Application không đổi (checklist 0.5).
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public AiProvider Provider { get; set; } = AiProvider.Fake;

    [Required]
    public string DefaultModel { get; set; } = "claude-opus-5-5";

    /// <summary>Model theo mục đích (planner, coding, reviewer...). Thiếu thì dùng <see cref="DefaultModel"/>.</summary>
    public Dictionary<string, string> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Mức effort: low | medium | high | xhigh | max.</summary>
    [RegularExpression("^(low|medium|high|xhigh|max)$")]
    public string Effort { get; set; } = "high";

    [Range(10, 3600)]
    public int RequestTimeoutSeconds { get; set; } = 600;

    [Range(0, 10)]
    public int MaxRetries { get; set; } = 2;

    /// <summary>Đơn giá theo model (USD / 1 triệu token) để ước tính chi phí và kiểm tra ngân sách.</summary>
    public Dictionary<string, ModelPrice> Pricing { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public AnthropicProviderOptions Anthropic { get; set; } = new();

    public string ModelFor(string purpose) =>
        Models.TryGetValue(purpose, out var model) && !string.IsNullOrWhiteSpace(model) ? model : DefaultModel;
}

public enum GitHubMode
{
    /// <summary>Chưa cấu hình: mọi thao tác báo lỗi rõ ràng và task bị chặn.</summary>
    Disabled = 1,

    /// <summary>Dữ liệu giả lập xác định, chỉ cho Development/Testing.</summary>
    Fake = 2,

    /// <summary>GitHub App thật (Phase 2A).</summary>
    App = 3,
}

public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    public GitHubMode Mode { get; set; } = GitHubMode.Disabled;

    public long? AppId { get; set; }

    /// <summary>Đường dẫn file private key (.pem) được mount dạng secret; không đặt nội dung key trong appsettings.</summary>
    public string? PrivateKeyPath { get; set; }

    /// <summary>Secret xác minh chữ ký webhook (X-Hub-Signature-256).</summary>
    public string? WebhookSecret { get; set; }

    [Url]
    public string ApiBaseUrl { get; set; } = "https://api.github.com";

    /// <summary>Nhãn issue kích hoạt DevAgent (checklist 2D: điều kiện kích hoạt).</summary>
    public string IssueTriggerLabel { get; set; } = "devagent";

    [Range(60, 3600)]
    public int InstallationTokenLifetimeSeconds { get; set; } = 3000;
}

public sealed class DiscordOptions
{
    public const string SectionName = "Discord";

    public bool Enabled { get; set; }

    /// <summary>Secret. Cấp qua <c>Discord__BotToken</c>.</summary>
    public string? BotToken { get; set; }

    public string? ApplicationId { get; set; }

    public string? GuildId { get; set; }

    /// <summary>Kênh nhận thông báo cần duyệt/hoàn tất/lỗi.</summary>
    public string? NotificationChannelId { get; set; }

    [Url]
    public string ApiBaseUrl { get; set; } = "https://discord.com/api/v10";
}

/// <summary>Giới hạn sandbox (checklist 2B, 0.7). Container non-root, không mount host, không Docker socket.</summary>
public sealed class SandboxOptions
{
    public const string SectionName = "Sandbox";

    public bool Enabled { get; set; }

    [Required]
    public string Image { get; set; } = "mcr.microsoft.com/dotnet/sdk:10.0";

    [Range(0.25, 16)]
    public double CpuLimit { get; set; } = 2;

    [Range(256, 65_536)]
    public int MemoryLimitMb { get; set; } = 4096;

    [Range(10, 7200)]
    public int CommandTimeoutSeconds { get; set; } = 600;

    [Range(60, 86_400)]
    public int MaxLifetimeSeconds { get; set; } = 3600;

    /// <summary>none | allowlist. Mặc định chặn mạng; allowlist cho phép các host trong <see cref="AllowedHosts"/>.</summary>
    [RegularExpression("^(none|allowlist)$")]
    public string NetworkPolicy { get; set; } = "none";

    public List<string> AllowedHosts { get; set; } = [];

    [Range(1000, 65_534)]
    public int RunAsUserId { get; set; } = 10001;
}

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>Khóa AES-256 dạng base64 (32 byte) để mã hóa credential lưu trong DB. Secret.</summary>
    public string? EncryptionKey { get; set; }
}

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    [Required]
    public string ServiceNamespace { get; set; } = "devagent";

    /// <summary>Endpoint OTLP (vd http://otel-collector:4317). Để trống thì không xuất trace/metric.</summary>
    public string? OtlpEndpoint { get; set; }

    [RegularExpression("^(grpc|http/protobuf)$")]
    public string OtlpProtocol { get; set; } = "grpc";

    /// <summary>json: một dòng JSON/sự kiện cho máy đọc (mặc định); text: dễ đọc khi phát triển.</summary>
    [RegularExpression("^(json|text)$")]
    public string ConsoleFormat { get; set; } = "json";
}

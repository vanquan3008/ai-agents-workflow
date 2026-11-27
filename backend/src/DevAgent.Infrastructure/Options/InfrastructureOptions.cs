using System.ComponentModel.DataAnnotations;

namespace DevAgent.Infrastructure.Options;

/// <summary>PostgreSQL settings. The connection string is the secret <c>ConnectionStrings:Postgres</c> (env or user-secrets).</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public const string ConnectionStringName = "Postgres";

    [Range(5, 600)]
    public int CommandTimeoutSeconds { get; set; } = 30;

    [Range(5, 600)]
    public int DesignTimeCommandTimeoutSeconds { get; set; } = 120;

    [Range(0, 10)]
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>Apply migrations on API startup. Enabled in dev/compose; production runs a migration bundle.</summary>
    public bool ApplyMigrationsOnStartup { get; set; }

    /// <summary>Logs SQL parameters. Never enable in production.</summary>
    public bool EnableSensitiveDataLogging { get; set; }
}

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

public sealed class RealtimeOptions
{
    public const string SectionName = "Realtime";

    [Range(1, 60)]
    public int ExpirySweepIntervalSeconds { get; set; } = 15;

    [Range(1, 60)]
    public int ReconnectInitialDelaySeconds { get; set; } = 1;

    [Range(1, 300)]
    public int ReconnectMaxDelaySeconds { get; set; } = 30;
}

public sealed class DepartmentRevisionCacheOptions
{
    public const string SectionName = "Departments:RevisionCache";

    [Range(1, 100_000)]
    public int MaxEntries { get; set; } = 1024;

    [Range(1, 1440)]
    public int SlidingExpirationMinutes { get; set; } = 30;
}

public enum AiProvider
{
    /// <summary>Deterministic, offline. For CI and development.</summary>
    Fake = 1,
    Anthropic = 2,
}

public sealed class ModelPriceOptions
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
    /// <summary>Secret, supplied through <c>Ai__Anthropic__ApiKey</c> or user-secrets.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Empty uses the SDK's default endpoint.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Enables the server-side fallback beta when the model refuses.</summary>
    public bool EnableServerSideFallback { get; set; } = true;
}

/// <summary>AI provider settings. Switching providers only needs <see cref="Provider"/> and an adapter; the application is unchanged.</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public AiProvider Provider { get; set; } = AiProvider.Fake;

    [Required]
    public string DefaultModel { get; set; } = "claude-opus-5-5";

    /// <summary>Model per purpose (planner, coding, reviewer...); falls back to <see cref="DefaultModel"/>.</summary>
    public Dictionary<string, string> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>low | medium | high | xhigh | max.</summary>
    [RegularExpression("^(low|medium|high|xhigh|max)$")]
    public string Effort { get; set; } = "high";

    [Range(10, 3600)]
    public int RequestTimeoutSeconds { get; set; } = 600;

    [Range(0, 10)]
    public int MaxRetries { get; set; } = 2;

    /// <summary>USD per million tokens by model, used for cost estimates and budget checks.</summary>
    public Dictionary<string, ModelPriceOptions> Pricing { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public AnthropicProviderOptions Anthropic { get; set; } = new();

    public string ModelFor(string purpose) =>
        Models.TryGetValue(purpose, out var model) && !string.IsNullOrWhiteSpace(model) ? model : DefaultModel;
}

public enum GitHubMode
{
    /// <summary>Not configured: every call fails explicitly and tasks are blocked.</summary>
    Disabled = 1,

    /// <summary>Deterministic stand-in, Development/Testing only.</summary>
    Fake = 2,

    /// <summary>Real GitHub App.</summary>
    App = 3,
}

public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    public GitHubMode Mode { get; set; } = GitHubMode.Disabled;

    public long? AppId { get; set; }

    /// <summary>Path to the mounted private key (.pem); never put key material in appsettings.</summary>
    public string? PrivateKeyPath { get; set; }

    /// <summary>Secret for verifying webhook signatures (X-Hub-Signature-256).</summary>
    public string? WebhookSecret { get; set; }

    [Url]
    public string ApiBaseUrl { get; set; } = "https://api.github.com";

    /// <summary>Issue label that triggers DevAgent.</summary>
    public string IssueTriggerLabel { get; set; } = "devagent";

    [Range(60, 3600)]
    public int InstallationTokenLifetimeSeconds { get; set; } = 3000;
}

public sealed class DiscordOptions
{
    public const string SectionName = "Discord";

    public bool Enabled { get; set; }

    /// <summary>Secret, supplied through <c>Discord__BotToken</c>.</summary>
    public string? BotToken { get; set; }

    public string? ApplicationId { get; set; }

    public string? GuildId { get; set; }

    /// <summary>Channel receiving approval, completion and failure notifications.</summary>
    public string? NotificationChannelId { get; set; }

    [Url]
    public string ApiBaseUrl { get; set; } = "https://discord.com/api/v10";

    [Range(1, 300)]
    public int RequestTimeoutSeconds { get; set; } = 15;

    [Range(1, 600)]
    public int RetryAfterFallbackSeconds { get; set; } = 5;
}

/// <summary>Sandbox limits: non-root container, no host mounts, no Docker socket.</summary>
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

    /// <summary>none | allowlist. Network is blocked by default; allowlist permits <see cref="AllowedHosts"/>.</summary>
    [RegularExpression("^(none|allowlist)$")]
    public string NetworkPolicy { get; set; } = "none";

    public List<string> AllowedHosts { get; set; } = [];

    [Range(1000, 65_534)]
    public int RunAsUserId { get; set; } = 10001;
}

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>Secret base64 AES-256 key (32 bytes) used to encrypt stored credentials.</summary>
    public string? EncryptionKey { get; set; }
}

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    [Required]
    public string ServiceName { get; set; } = string.Empty;

    [Required]
    public string ServiceNamespace { get; set; } = "devagent";

    /// <summary>OTLP endpoint (e.g. http://otel-collector:4317). Empty disables trace/metric export.</summary>
    public string? OtlpEndpoint { get; set; }

    [RegularExpression("^(grpc|http/protobuf)$")]
    public string OtlpProtocol { get; set; } = "grpc";

    /// <summary>json: one machine-readable line per event (default); text: human-friendly for development.</summary>
    [RegularExpression("^(json|text)$")]
    public string ConsoleFormat { get; set; } = "json";
}

public sealed class SuperAdminBootstrap
{
    public string? Email { get; set; }

    /// <summary>Secret temporary password from <c>Bootstrap__SuperAdmin__Password</c>; must be changed on first sign-in.</summary>
    public string? Password { get; set; }

    [Required]
    public string DisplayName { get; set; } = "Super Admin";
}

/// <summary>System bootstrap data: only the first super admin account.</summary>
public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public SuperAdminBootstrap SuperAdmin { get; set; } = new();
}

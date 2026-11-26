using DevAgent.Domain.Common;

namespace DevAgent.Domain.Policies;

/// <summary>
/// Change policy configured by the repository owner. Immutable value object; every change produces a new
/// <see cref="Version"/> so approval decisions stay bound to the policy they were evaluated against.
/// </summary>
public sealed record RepositoryPolicy
{
    public RepositoryPolicy(
        int version,
        IReadOnlyList<string> autoFixPaths,
        IReadOnlyList<SensitivePathRule> sensitivePaths,
        string buildCommand,
        string testCommand,
        int maxChangedFiles)
    {
        ArgumentNullException.ThrowIfNull(autoFixPaths);
        ArgumentNullException.ThrowIfNull(sensitivePaths);

        if (version < 1)
        {
            throw new DomainException("policy.version_invalid", "Phiên bản chính sách phải >= 1.");
        }

        if (maxChangedFiles is < 1 or > 500)
        {
            throw new DomainException("policy.max_changed_files_invalid", "Giới hạn số file thay đổi phải trong khoảng 1..500.");
        }

        Version = version;
        AutoFixPaths = autoFixPaths.Select(p => new PathPattern(p).Pattern).ToArray();
        SensitivePaths = sensitivePaths.Select(r => r with { Pattern = new PathPattern(r.Pattern).Pattern }).ToArray();
        BuildCommand = Guard.NotEmpty(buildCommand, "build_command", 500);
        TestCommand = Guard.NotEmpty(testCommand, "test_command", 500);
        MaxChangedFiles = maxChangedFiles;
    }

    public int Version { get; }

    /// <summary>Paths that may be changed without approval. Anything outside always needs a human.</summary>
    public IReadOnlyList<string> AutoFixPaths { get; }

    public IReadOnlyList<SensitivePathRule> SensitivePaths { get; }

    public string BuildCommand { get; }

    public string TestCommand { get; }

    public int MaxChangedFiles { get; }

    /// <summary>Safe default: every common sensitive area requires approval.</summary>
    public static RepositoryPolicy CreateDefault(string buildCommand, string testCommand) => new(
        1,
        ["src/**", "tests/**", "test/**"],
        [
            new("**/auth/**", SensitiveArea.Authentication),
            new("**/*Auth*.*", SensitiveArea.Authentication),
            new("**/*Permission*.*", SensitiveArea.Authorization),
            new("**/*Policy*.*", SensitiveArea.Authorization),
            new("**/Migrations/**", SensitiveArea.Migration),
            new("**/migrations/**", SensitiveArea.Migration),
            new("**/.env*", SensitiveArea.Credentials),
            new("**/appsettings*.json", SensitiveArea.Credentials),
            new("**/secrets/**", SensitiveArea.Credentials),
            new(".github/**", SensitiveArea.CiCd),
            new("**/Dockerfile*", SensitiveArea.CiCd),
            new("**/docker-compose*.yml", SensitiveArea.CiCd),
        ],
        buildCommand,
        testCommand,
        maxChangedFiles: 20);

    public RepositoryPolicy NextVersion(
        IReadOnlyList<string> autoFixPaths,
        IReadOnlyList<SensitivePathRule> sensitivePaths,
        string buildCommand,
        string testCommand,
        int maxChangedFiles) =>
        new(Version + 1, autoFixPaths, sensitivePaths, buildCommand, testCommand, maxChangedFiles);
}

using DevAgent.Domain.Common;

namespace DevAgent.Domain.Policies;

/// <summary>Quy tắc vùng nhạy cảm: các đường dẫn khớp mẫu thuộc về một vùng.</summary>
public sealed record SensitivePathRule(string Pattern, SensitiveArea Area);

/// <summary>
/// Chính sách do chủ repo cấu hình trước (checklist: "Chính sách cho pilot").
/// Value object bất biến; thay đổi chính sách tạo bản mới với <see cref="Version"/> tăng dần
/// để quyết định duyệt có thể gắn với đúng phiên bản chính sách.
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

    /// <summary>Phạm vi được sửa tự động. Ngoài phạm vi này luôn cần người duyệt.</summary>
    public IReadOnlyList<string> AutoFixPaths { get; }

    public IReadOnlyList<SensitivePathRule> SensitivePaths { get; }

    public string BuildCommand { get; }

    public string TestCommand { get; }

    public int MaxChangedFiles { get; }

    /// <summary>Chính sách mặc định an toàn: mọi vùng nhạy cảm phổ biến đều cần duyệt.</summary>
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

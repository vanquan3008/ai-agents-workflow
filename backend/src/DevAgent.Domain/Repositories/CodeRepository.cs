using DevAgent.Domain.Common;
using DevAgent.Domain.Policies;

namespace DevAgent.Domain.Repositories;

public enum SourceControlProvider
{
    GitHub = 1,
}

/// <summary>
/// Repo đã đăng ký vào một project qua GitHub App installation.
/// Đặt tên CodeRepository để không nhầm với pattern Repository của DDD.
/// Credential không nằm ở đây: token ngắn hạn do thành phần tích hợp cấp khi cần.
/// </summary>
public sealed class CodeRepository : AggregateRoot<RepositoryId>
{
    private CodeRepository()
    {
        Owner = string.Empty;
        Name = string.Empty;
        DefaultBranch = string.Empty;
        Policy = null!;
    }

    private CodeRepository(
        RepositoryId id,
        ProjectId projectId,
        long installationId,
        string owner,
        string name,
        string defaultBranch,
        RepositoryPolicy policy,
        DateTimeOffset createdAt)
        : base(id)
    {
        ProjectId = projectId;
        Provider = SourceControlProvider.GitHub;
        InstallationId = installationId;
        Owner = owner;
        Name = name;
        DefaultBranch = defaultBranch;
        Policy = policy;
        CreatedAt = createdAt;
    }

    public ProjectId ProjectId { get; private set; }

    public SourceControlProvider Provider { get; private set; }

    public long InstallationId { get; private set; }

    public string Owner { get; private set; }

    public string Name { get; private set; }

    public string FullName => $"{Owner}/{Name}";

    public string DefaultBranch { get; private set; }

    public RepositoryPolicy Policy { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedAt { get; private set; }

    public static CodeRepository Register(
        ProjectId projectId,
        long installationId,
        string owner,
        string name,
        string defaultBranch,
        RepositoryPolicy policy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (installationId <= 0)
        {
            throw new DomainException("repository.installation_invalid", "GitHub App installation id không hợp lệ.");
        }

        return new CodeRepository(
            RepositoryId.New(),
            Guard.NotDefault(projectId, "project_id"),
            installationId,
            Guard.NotEmpty(owner, "owner", 100),
            Guard.NotEmpty(name, "name", 100),
            Guard.NotEmpty(defaultBranch, "default_branch", 255),
            policy,
            Guard.Utc(now, "created_at"));
    }

    public void UpdatePolicy(RepositoryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.Version <= Policy.Version)
        {
            throw new DomainException("repository.policy_version_stale", "Phiên bản chính sách mới phải lớn hơn phiên bản hiện tại.");
        }

        Policy = policy;
    }

    public void Deactivate() => IsActive = false;
}

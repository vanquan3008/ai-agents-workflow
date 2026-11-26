using DevAgent.Domain.Common;
using DevAgent.Domain.Policies;
using DevAgent.Domain.Workflows;

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
        Workflow = WorkflowDefinition.Default;
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
        Workflow = WorkflowDefinition.Default;
        WorkflowVersion = 0;
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

    /// <summary>Quy trình hiện hành. Phiên bản 0 là quy trình mặc định của hệ thống.</summary>
    public WorkflowDefinition Workflow { get; private set; }

    public int WorkflowVersion { get; private set; }

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

    /// <summary>
    /// Đổi quy trình với kiểm tra phiên bản (optimistic): người sửa phải dựa trên phiên bản hiện hành,
    /// tránh hai người ghi đè nhau. Trả về bản ghi lịch sử để lưu append-only.
    /// </summary>
    public WorkflowRevision UpdateWorkflow(WorkflowDefinition definition, int expectedVersion, UserId changedBy, string? comment, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (expectedVersion != WorkflowVersion)
        {
            throw new DomainException(
                "workflow.version_conflict",
                $"Quy trình đã được đổi sang phiên bản {WorkflowVersion}; bạn đang sửa phiên bản {expectedVersion}.");
        }

        if (definition == Workflow)
        {
            throw new DomainException("workflow.unchanged", "Quy trình không có thay đổi nào.");
        }

        Workflow = definition;
        WorkflowVersion++;
        return new WorkflowRevision(
            WorkflowRevisionId.New(),
            Id,
            WorkflowVersion,
            definition,
            Guard.NotDefault(changedBy, "changed_by"),
            Guard.Optional(comment, "comment", 1000),
            Guard.Utc(now, "changed_at"));
    }

    public void Deactivate() => IsActive = false;
}

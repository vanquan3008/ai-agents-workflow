using DevAgent.Application.Common;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;

namespace DevAgent.Application.Features.Projects;

public sealed record ProjectDto(Guid Id, string Name, string Slug, string MyRole, DateTimeOffset CreatedAt);

public sealed record ProjectMemberDto(Guid UserId, string DisplayName, string Role, DateTimeOffset AddedAt);

public sealed record SensitivePathDto(string Pattern, string Area);

public sealed record RepositoryPolicyDto(
    int Version,
    IReadOnlyList<string> AutoFixPaths,
    IReadOnlyList<SensitivePathDto> SensitivePaths,
    string BuildCommand,
    string TestCommand,
    int MaxChangedFiles);

public sealed record RepositoryDto(
    Guid Id,
    Guid ProjectId,
    string Owner,
    string Name,
    string FullName,
    string DefaultBranch,
    long InstallationId,
    RepositoryPolicyDto Policy,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    public static RepositoryDto From(CodeRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var policy = repository.Policy;
        return new RepositoryDto(
            repository.Id.Value,
            repository.ProjectId.Value,
            repository.Owner,
            repository.Name,
            repository.FullName,
            repository.DefaultBranch,
            repository.InstallationId,
            new RepositoryPolicyDto(
                policy.Version,
                policy.AutoFixPaths,
                policy.SensitivePaths.Select(r => new SensitivePathDto(r.Pattern, ApiNames.ToApi(r.Area))).ToArray(),
                policy.BuildCommand,
                policy.TestCommand,
                policy.MaxChangedFiles),
            repository.IsActive,
            repository.CreatedAt);
    }
}

/// <summary>Truy vấn đọc cho module Projects (hiện thực bằng projection EF, không tải aggregate).</summary>
public interface IProjectQueries
{
    Task<IReadOnlyList<ProjectDto>> ListForUserAsync(UserId userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectMemberDto>> ListMembersAsync(ProjectId projectId, CancellationToken cancellationToken);

    /// <summary>Các project mà người dùng có quyền <paramref name="permission"/>; dùng để lọc danh sách.</summary>
    Task<IReadOnlyList<ProjectId>> ListProjectIdsWithPermissionAsync(UserId userId, ProjectPermission permission, CancellationToken cancellationToken);

    Task<IReadOnlyList<RepositoryDto>> ListRepositoriesAsync(ProjectId projectId, CancellationToken cancellationToken);
}

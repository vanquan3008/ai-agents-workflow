using DevAgent.Application.Common;
using DevAgent.Domain.Repositories;

namespace DevAgent.Application.Features.Projects;

/// <param name="MyRole">The viewer's role in the project; SUPER_ADMIN when a super admin is not a member.</param>
public sealed record ProjectDto(Guid Id, string Name, string Slug, string MyRole, int MemberCount, int RepositoryCount, DateTimeOffset CreatedAt);

public sealed record ProjectMemberDto(Guid UserId, string DisplayName, string Email, string Role, DateTimeOffset AddedAt);

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
    int WorkflowVersion,
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
            repository.WorkflowVersion,
            repository.IsActive,
            repository.CreatedAt);
    }
}

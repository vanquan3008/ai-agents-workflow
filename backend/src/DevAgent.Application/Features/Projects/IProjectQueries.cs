using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Projects;

/// <summary>Read-side projections for the Projects module; implemented with EF projections, not aggregate loads.</summary>
public interface IProjectQueries
{
    Task<IReadOnlyList<ProjectDto>> ListForUserAsync(UserId userId, CancellationToken cancellationToken);

    /// <summary>Every project, for super admins; <paramref name="viewer"/> fills in the role when they are a member.</summary>
    Task<IReadOnlyList<ProjectDto>> ListAllAsync(UserId viewer, CancellationToken cancellationToken);

    Task<ProjectDto?> GetAsync(ProjectId projectId, UserId viewer, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectId>> ListAllProjectIdsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectMemberDto>> ListMembersAsync(ProjectId projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectId>> ListProjectIdsWithPermissionAsync(UserId userId, ProjectPermission permission, CancellationToken cancellationToken);

    Task<IReadOnlyList<RepositoryDto>> ListRepositoriesAsync(ProjectId projectId, CancellationToken cancellationToken);
}

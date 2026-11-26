using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Projects;

public sealed record ListMyProjectsQuery;

internal sealed class ListMyProjectsHandler(ProjectAccess access, IProjectQueries queries)
    : IQueryHandler<ListMyProjectsQuery, IReadOnlyList<ProjectDto>>
{
    public async Task<Result<IReadOnlyList<ProjectDto>>> HandleAsync(ListMyProjectsQuery query, CancellationToken cancellationToken)
    {
        var user = access.RequireAuthenticated();
        return user.IsFailure
            ? Result<IReadOnlyList<ProjectDto>>.Failure(user.Error!)
            : Result<IReadOnlyList<ProjectDto>>.Success(await queries.ListForUserAsync(user.Value, cancellationToken));
    }
}

public sealed record ListRepositoriesQuery(Guid ProjectId);

internal sealed class ListRepositoriesHandler(ProjectAccess access, IProjectQueries queries)
    : IQueryHandler<ListRepositoriesQuery, IReadOnlyList<RepositoryDto>>
{
    public async Task<Result<IReadOnlyList<RepositoryDto>>> HandleAsync(ListRepositoriesQuery query, CancellationToken cancellationToken)
    {
        var projectId = new ProjectId(query.ProjectId);
        var user = await access.RequireAsync(projectId, ProjectPermission.ViewTasks, "project", projectId.ToString(), cancellationToken);
        return user.IsFailure
            ? Result<IReadOnlyList<RepositoryDto>>.Failure(user.Error!)
            : Result<IReadOnlyList<RepositoryDto>>.Success(await queries.ListRepositoriesAsync(projectId, cancellationToken));
    }
}

public sealed record ListProjectMembersQuery(Guid ProjectId);

internal sealed class ListProjectMembersHandler(ProjectAccess access, IProjectQueries queries)
    : IQueryHandler<ListProjectMembersQuery, IReadOnlyList<ProjectMemberDto>>
{
    public async Task<Result<IReadOnlyList<ProjectMemberDto>>> HandleAsync(ListProjectMembersQuery query, CancellationToken cancellationToken)
    {
        var projectId = new ProjectId(query.ProjectId);
        var user = await access.RequireAsync(projectId, ProjectPermission.ViewTasks, "project", projectId.ToString(), cancellationToken);
        return user.IsFailure
            ? Result<IReadOnlyList<ProjectMemberDto>>.Failure(user.Error!)
            : Result<IReadOnlyList<ProjectMemberDto>>.Success(await queries.ListMembersAsync(projectId, cancellationToken));
    }
}

using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Projects;

public sealed record ListMyProjectsQuery;

/// <summary>Project người dùng tham gia; Super Admin thấy mọi project.</summary>
internal sealed class ListMyProjectsHandler(ProjectAccess access, IProjectQueries queries)
    : IQueryHandler<ListMyProjectsQuery, IReadOnlyList<ProjectDto>>
{
    public async Task<Result<IReadOnlyList<ProjectDto>>> HandleAsync(ListMyProjectsQuery query, CancellationToken cancellationToken)
    {
        var user = await access.CurrentActiveUserAsync(cancellationToken);
        if (user is null)
        {
            return Error.Unauthorized();
        }

        return Result<IReadOnlyList<ProjectDto>>.Success(user.IsSuperAdmin
            ? await queries.ListAllAsync(user.Id, cancellationToken)
            : await queries.ListForUserAsync(user.Id, cancellationToken));
    }
}

public sealed record GetProjectQuery(Guid ProjectId);

internal sealed class GetProjectHandler(ProjectAccess access, IProjectQueries queries) : IQueryHandler<GetProjectQuery, ProjectDto>
{
    public async Task<Result<ProjectDto>> HandleAsync(GetProjectQuery query, CancellationToken cancellationToken)
    {
        var projectId = new ProjectId(query.ProjectId);
        var user = await access.RequireAsync(projectId, ProjectPermission.ViewTasks, "project", projectId.ToString(), cancellationToken);
        if (user.IsFailure)
        {
            return user.Error!;
        }

        var project = await queries.GetAsync(projectId, user.Value, cancellationToken);
        return project is null ? Error.NotFound("project", query.ProjectId) : project;
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

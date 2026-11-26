using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Projects;

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

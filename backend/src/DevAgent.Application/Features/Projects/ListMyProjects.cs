using DevAgent.Application.Authorization;
using DevAgent.Application.Common;

namespace DevAgent.Application.Features.Projects;

public sealed record ListMyProjectsQuery;

/// <summary>Projects the user belongs to; super admins see every project.</summary>
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

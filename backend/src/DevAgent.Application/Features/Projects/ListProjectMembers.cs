using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Projects;

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

using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Projects;

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

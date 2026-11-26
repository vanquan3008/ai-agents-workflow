using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Options;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Repositories.Repositories;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.Features.Workflows;

public sealed record GetRepositoryWorkflowQuery(Guid RepositoryId);

internal sealed class GetRepositoryWorkflowHandler(ProjectAccess access, ICodeRepositoryRepository repositories, IOptions<AgentLimitsOptions> limits)
    : IQueryHandler<GetRepositoryWorkflowQuery, RepositoryWorkflowDto>
{
    public async Task<Result<RepositoryWorkflowDto>> HandleAsync(GetRepositoryWorkflowQuery query, CancellationToken cancellationToken)
    {
        var repository = await repositories.GetByIdAsync(new RepositoryId(query.RepositoryId), cancellationToken);
        if (repository is null)
        {
            return Error.NotFound("repository", query.RepositoryId);
        }

        var user = await access.RequireAsync(repository.ProjectId, ProjectPermission.ViewTasks, "repository.workflow", repository.Id.ToString(), cancellationToken);
        if (user.IsFailure)
        {
            return user.Error!;
        }

        return WorkflowMapping.ToRepositoryWorkflow(repository, limits.Value);
    }
}

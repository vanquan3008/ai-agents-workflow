using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Repositories.Repositories;
using DevAgent.Domain.Workflows;
using DevAgent.Domain.Workflows.Repositories;

namespace DevAgent.Application.Features.Workflows;

public sealed record ListWorkflowRevisionsQuery(Guid RepositoryId, int? Limit);

internal sealed class ListWorkflowRevisionsHandler(ProjectAccess access, ICodeRepositoryRepository repositories, IWorkflowRevisionRepository revisions)
    : IQueryHandler<ListWorkflowRevisionsQuery, IReadOnlyList<WorkflowRevisionDto>>
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    public async Task<Result<IReadOnlyList<WorkflowRevisionDto>>> HandleAsync(ListWorkflowRevisionsQuery query, CancellationToken cancellationToken)
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

        var items = await revisions.ListByRepositoryAsync(repository.Id, Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit), cancellationToken);
        return Result<IReadOnlyList<WorkflowRevisionDto>>.Success(items
            .Select(r => new WorkflowRevisionDto(r.Version, r.ChangedBy.Value, r.ChangedAt, r.Comment, WorkflowMapping.ToDto(r.Definition)))
            .ToArray());
    }
}

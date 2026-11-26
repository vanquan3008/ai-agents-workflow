using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Options;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Auditing.Repositories;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Repositories.Repositories;
using DevAgent.Domain.Workflows;
using DevAgent.Domain.Workflows.Repositories;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.Features.Workflows;

/// <param name="ExpectedVersion">The version the editor started from; a mismatch returns 409 instead of overwriting.</param>
public sealed record UpdateRepositoryWorkflowCommand(Guid RepositoryId, int ExpectedVersion, WorkflowDefinitionDto Definition, string? Comment);

internal sealed class UpdateRepositoryWorkflowHandler(
    ProjectAccess access,
    ICodeRepositoryRepository repositories,
    IWorkflowRevisionRepository revisions,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    IOptions<AgentLimitsOptions> limits,
    TimeProvider clock) : ICommandHandler<UpdateRepositoryWorkflowCommand, RepositoryWorkflowDto>
{
    public async Task<Result<RepositoryWorkflowDto>> HandleAsync(UpdateRepositoryWorkflowCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Definition is null)
        {
            return Error.Validation("workflow.required", "Thiếu nội dung quy trình.");
        }

        var repository = await repositories.GetByIdAsync(new RepositoryId(command.RepositoryId), cancellationToken);
        if (repository is null)
        {
            return Error.NotFound("repository", command.RepositoryId);
        }

        var user = await access.RequireAsync(repository.ProjectId, ProjectPermission.ManageRepositories, "repository.workflow", repository.Id.ToString(), cancellationToken);
        if (user.IsFailure)
        {
            return user.Error!;
        }

        var definition = WorkflowMapping.FromDto(command.Definition, WorkflowMapping.FixIterationsCap(limits.Value));
        if (definition.IsFailure)
        {
            return definition.Error!;
        }

        // Checked here as well as in the aggregate so the API can answer 409/400 instead of a generic domain error.
        if (command.ExpectedVersion != repository.WorkflowVersion)
        {
            return Error.Conflict(
                "workflow.version_conflict",
                $"Quy trình đã được đổi sang phiên bản {repository.WorkflowVersion}. Tải lại rồi áp dụng thay đổi của bạn.");
        }

        if (definition.Value == repository.Workflow)
        {
            return Error.Validation("workflow.unchanged", "Quy trình không có thay đổi nào.");
        }

        var now = clock.GetUtcNow();
        var revision = repository.UpdateWorkflow(definition.Value, command.ExpectedVersion, user.Value, command.Comment, now);
        revisions.Add(revision);
        auditLog.Add(AuditEntry.Record(
            Actor.ForUser(user.Value),
            "repository.workflow.update",
            "repository",
            repository.Id.ToString(),
            AuditOutcome.Allowed,
            now,
            repository.ProjectId,
            reason: $"v{revision.Version}: {command.Comment}"));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkflowMapping.ToRepositoryWorkflow(repository, limits.Value);
    }
}

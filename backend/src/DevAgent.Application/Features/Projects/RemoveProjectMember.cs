using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Auditing.Repositories;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Projects.Repositories;

namespace DevAgent.Application.Features.Projects;

public sealed record RemoveProjectMemberCommand(Guid ProjectId, Guid UserId);

internal sealed class RemoveProjectMemberHandler(
    ProjectAccess access,
    IProjectRepository projects,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<RemoveProjectMemberCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(RemoveProjectMemberCommand command, CancellationToken cancellationToken)
    {
        var projectId = new ProjectId(command.ProjectId);
        var actor = await access.RequireAsync(projectId, ProjectPermission.ManageMembers, "project", projectId.ToString(), cancellationToken);
        if (actor.IsFailure)
        {
            return actor.Error!;
        }

        var project = (await projects.GetByIdAsync(projectId, cancellationToken))!;
        var memberId = new UserId(command.UserId);
        if (project.RoleOf(memberId) is null)
        {
            return Error.NotFound("member", command.UserId);
        }

        var now = clock.GetUtcNow();
        project.RemoveMember(memberId, now);
        auditLog.Add(AuditEntry.Record(
            Actor.ForUser(actor.Value), "project.member.remove", "project", projectId.ToString(), AuditOutcome.Allowed, now, projectId,
            reason: memberId.ToString()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

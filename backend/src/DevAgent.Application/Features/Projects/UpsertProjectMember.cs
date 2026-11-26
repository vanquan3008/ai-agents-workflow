using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Auditing.Repositories;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Projects.Repositories;
using DevAgent.Domain.Users;
using DevAgent.Domain.Users.Repositories;

namespace DevAgent.Application.Features.Projects;

public sealed record UpsertProjectMemberCommand(Guid ProjectId, string Email, string Role);

internal sealed class UpsertProjectMemberHandler(
    ProjectAccess access,
    IProjectRepository projects,
    IUserRepository users,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<UpsertProjectMemberCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(UpsertProjectMemberCommand command, CancellationToken cancellationToken)
    {
        if (!ApiNames.TryParse<ProjectRole>(command.Role, out var role))
        {
            return Error.Validation("member.role_invalid", "Vai trò không hợp lệ (VIEWER, REQUESTER, APPROVER, OWNER).",
                new Dictionary<string, string[]> { ["role"] = ["Vai trò không hợp lệ."] });
        }

        var projectId = new ProjectId(command.ProjectId);
        var actor = await access.RequireAsync(projectId, ProjectPermission.ManageMembers, "project", projectId.ToString(), cancellationToken);
        if (actor.IsFailure)
        {
            return actor.Error!;
        }

        var member = string.IsNullOrWhiteSpace(command.Email) ? null : await users.FindByEmailAsync(command.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (member is not { IsActive: true })
        {
            return Error.Validation("member.user_not_found", "Không tìm thấy người dùng đang hoạt động với email này.",
                new Dictionary<string, string[]> { ["email"] = ["Không tìm thấy người dùng đang hoạt động với email này. Super Admin cần tạo tài khoản trước."] });
        }

        var project = (await projects.GetByIdAsync(projectId, cancellationToken))!;
        var now = clock.GetUtcNow();
        project.AddOrUpdateMember(member.Id, role, now);
        auditLog.Add(AuditEntry.Record(
            Actor.ForUser(actor.Value), "project.member.upsert", "project", projectId.ToString(), AuditOutcome.Allowed, now, projectId,
            reason: $"{member.Email} => {role}"));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

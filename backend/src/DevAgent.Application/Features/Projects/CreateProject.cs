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

/// <param name="OwnerEmail">The user to make owner; empty means the creating super admin becomes owner.</param>
public sealed record CreateProjectCommand(string Name, string Slug, string? OwnerEmail);

internal sealed class CreateProjectHandler(
    ProjectAccess access,
    IProjectRepository projects,
    IUserRepository users,
    IProjectQueries queries,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CreateProjectCommand, ProjectDto>
{
    public async Task<Result<ProjectDto>> HandleAsync(CreateProjectCommand command, CancellationToken cancellationToken)
    {
        var admin = await access.RequireSuperAdminAsync("project.create", cancellationToken);
        if (admin.IsFailure)
        {
            return admin.Error!;
        }

        if (await projects.SlugExistsAsync(command.Slug.Trim().ToLowerInvariant(), cancellationToken))
        {
            return Error.Field("project.slug_taken", "slug", "Slug đã được dùng.");
        }

        var owner = admin.Value;
        if (!string.IsNullOrWhiteSpace(command.OwnerEmail))
        {
            var found = await users.FindByEmailAsync(command.OwnerEmail.Trim().ToLowerInvariant(), cancellationToken);
            if (found is not { IsActive: true })
            {
                return Error.Field("project.owner_not_found", "ownerEmail", "Không tìm thấy người dùng đang hoạt động với email này.");
            }

            owner = found;
        }

        var now = clock.GetUtcNow();
        var project = Project.Create(command.Name, command.Slug, owner.Id, now);
        projects.Add(project);
        auditLog.Add(AuditEntry.Record(
            Actor.ForUser(admin.Value.Id), "project.create", "project", project.Id.ToString(), AuditOutcome.Allowed, now, project.Id,
            reason: $"owner={owner.Email}"));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return (await queries.GetAsync(project.Id, admin.Value.Id, cancellationToken))!;
    }
}

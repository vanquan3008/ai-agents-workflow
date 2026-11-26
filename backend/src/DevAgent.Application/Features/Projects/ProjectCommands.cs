using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Common;
using DevAgent.Domain.Policies;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;

namespace DevAgent.Application.Features.Projects;

// ---------- Tạo project (Super Admin) ----------

/// <param name="OwnerEmail">Email người được giao làm Owner; để trống thì Super Admin tạo là Owner.</param>
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
            return Error.Validation("project.slug_taken", "Slug đã được dùng.", new Dictionary<string, string[]> { ["slug"] = ["Slug đã được dùng."] });
        }

        var owner = admin.Value;
        if (!string.IsNullOrWhiteSpace(command.OwnerEmail))
        {
            var found = await users.FindByEmailAsync(command.OwnerEmail.Trim().ToLowerInvariant(), cancellationToken);
            if (found is not { IsActive: true })
            {
                return Error.Validation("project.owner_not_found", "Không tìm thấy người dùng đang hoạt động với email này.",
                    new Dictionary<string, string[]> { ["ownerEmail"] = ["Không tìm thấy người dùng đang hoạt động với email này."] });
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

// ---------- Thêm/cập nhật/xóa thành viên ----------

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
        project.RemoveMember(memberId);
        auditLog.Add(AuditEntry.Record(
            Actor.ForUser(actor.Value), "project.member.remove", "project", projectId.ToString(), AuditOutcome.Allowed, now, projectId,
            reason: memberId.ToString()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

// ---------- Đăng ký repo ----------

public sealed record SensitivePathInput(string Pattern, string Area);

public sealed record RegisterRepositoryCommand(
    Guid ProjectId,
    long InstallationId,
    string Owner,
    string Name,
    string DefaultBranch,
    string BuildCommand,
    string TestCommand,
    IReadOnlyList<string>? AutoFixPaths,
    IReadOnlyList<SensitivePathInput>? SensitivePaths,
    int? MaxChangedFiles);

internal sealed class RegisterRepositoryHandler(
    ProjectAccess access,
    ICodeRepositoryRepository repositories,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<RegisterRepositoryCommand, RepositoryDto>
{
    public async Task<Result<RepositoryDto>> HandleAsync(RegisterRepositoryCommand command, CancellationToken cancellationToken)
    {
        var projectId = new ProjectId(command.ProjectId);
        var actor = await access.RequireAsync(projectId, ProjectPermission.ManageRepositories, "project", projectId.ToString(), cancellationToken);
        if (actor.IsFailure)
        {
            return actor.Error!;
        }

        if (await repositories.ExistsAsync(command.Owner.Trim(), command.Name.Trim(), cancellationToken))
        {
            return Error.Conflict("repository.already_registered", "Repo đã được đăng ký.");
        }

        var policy = BuildPolicy(command);
        if (policy.IsFailure)
        {
            return policy.Error!;
        }

        var now = clock.GetUtcNow();
        var repository = CodeRepository.Register(projectId, command.InstallationId, command.Owner, command.Name, command.DefaultBranch, policy.Value, now);
        repositories.Add(repository);
        auditLog.Add(AuditEntry.Record(
            Actor.ForUser(actor.Value), "repository.register", "repository", repository.Id.ToString(), AuditOutcome.Allowed, now, projectId,
            reason: repository.FullName));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return RepositoryDto.From(repository);
    }

    private static Result<RepositoryPolicy> BuildPolicy(RegisterRepositoryCommand command)
    {
        var defaults = RepositoryPolicy.CreateDefault(command.BuildCommand, command.TestCommand);
        if (command.AutoFixPaths is null && command.SensitivePaths is null && command.MaxChangedFiles is null)
        {
            return defaults;
        }

        var rules = new List<SensitivePathRule>();
        foreach (var input in command.SensitivePaths ?? [])
        {
            if (!ApiNames.TryParse<SensitiveArea>(input.Area, out var area) || area == SensitiveArea.None)
            {
                return Error.Validation("policy.area_invalid", $"Vùng nhạy cảm không hợp lệ: {input.Area}.");
            }

            rules.Add(new SensitivePathRule(input.Pattern, area));
        }

        return new RepositoryPolicy(
            1,
            command.AutoFixPaths ?? defaults.AutoFixPaths,
            command.SensitivePaths is null ? defaults.SensitivePaths : rules,
            command.BuildCommand,
            command.TestCommand,
            command.MaxChangedFiles ?? defaults.MaxChangedFiles);
    }
}

using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Auditing.Repositories;
using DevAgent.Domain.Common;
using DevAgent.Domain.Policies;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Repositories.Repositories;

namespace DevAgent.Application.Features.Projects;

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

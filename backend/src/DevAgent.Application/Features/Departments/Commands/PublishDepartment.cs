using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Application.Features.Departments.Shared;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;
using DevAgent.Domain.Departments.Repositories;
using DevAgent.Domain.Departments.Rules;

namespace DevAgent.Application.Features.Departments.Commands;

public sealed record PublishDepartmentCommand(Guid ProjectId, Guid DepartmentId, int ExpectedEditVersion);

/// <summary>
/// Validates the whole draft and, in one transaction, appends the next revision with its rooms and repository links,
/// moves the published pointer, clears the draft and records the audit entry.
/// </summary>
internal sealed class PublishDepartmentHandler(
    DepartmentAccess access,
    IDepartmentQueries queries,
    IItDepartmentRevisionRepository revisions,
    DepartmentChangeRecorder recorder,
    TimeProvider clock) : ICommandHandler<PublishDepartmentCommand, DepartmentEditResultDto>
{
    public async Task<Result<DepartmentEditResultDto>> HandleAsync(PublishDepartmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var context = await access.RequireManagedAsync(new ProjectId(command.ProjectId), new ItDepartmentId(command.DepartmentId), cancellationToken);
        if (context.IsFailure)
        {
            return context.Error!;
        }

        var (authorization, department) = context.Value;
        if (DepartmentAccess.CheckEditable(department, command.ExpectedEditVersion) is { } conflict)
        {
            return conflict;
        }

        if (department.Draft is not { } draft)
        {
            return Error.Conflict("department.no_draft", "Không có bản nháp để xuất bản.");
        }

        var projectRepositories = await queries.ListProjectRepositoriesAsync(department.ProjectId, cancellationToken);
        var violations = draft.Validate(DepartmentValidationLevel.Publish)
            .Concat(DepartmentMapping.ForeignRepositories(draft, projectRepositories))
            .ToArray();
        if (violations.Length > 0)
        {
            return DepartmentMapping.ToValidationError(violations);
        }

        var now = clock.GetUtcNow();
        var revision = department.Publish(command.ExpectedEditVersion, authorization.UserId, now);
        revisions.Add(revision);
        return await recorder.CommitAsync(department, authorization.UserId, "publish", revision.Version, now, cancellationToken);
    }
}

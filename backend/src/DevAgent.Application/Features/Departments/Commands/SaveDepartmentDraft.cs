using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Application.Features.Departments.Shared;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;
using DevAgent.Domain.Departments.Rules;

namespace DevAgent.Application.Features.Departments.Commands;

/// <param name="ExpectedEditVersion">The edit version the editor started from; a mismatch returns 409 and keeps the local draft.</param>
public sealed record SaveDepartmentDraftCommand(Guid ProjectId, Guid DepartmentId, int ExpectedEditVersion, DepartmentConfigDto Config);

/// <summary>Saves private work in progress. Only structural rules apply; completeness is checked on publish.</summary>
internal sealed class SaveDepartmentDraftHandler(
    DepartmentAccess access,
    IDepartmentQueries queries,
    DepartmentChangeRecorder recorder,
    DepartmentDraftProjection projection,
    TimeProvider clock) : ICommandHandler<SaveDepartmentDraftCommand, DepartmentDraftDto>
{
    public async Task<Result<DepartmentDraftDto>> HandleAsync(SaveDepartmentDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Config is null)
        {
            return Error.Validation("department.config_required", "Thiếu cấu hình phòng ban.");
        }

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

        var configuration = DepartmentMapping.FromDto(command.Config);
        var projectRepositories = await queries.ListProjectRepositoriesAsync(department.ProjectId, cancellationToken);
        var violations = configuration.Validate(DepartmentValidationLevel.Draft)
            .Concat(DepartmentMapping.ForeignRepositories(configuration, projectRepositories))
            .ToArray();
        if (violations.Length > 0)
        {
            return DepartmentMapping.ToValidationError(violations);
        }

        var now = clock.GetUtcNow();
        department.SaveDraft(configuration, command.ExpectedEditVersion, now);
        var saved = await recorder.CommitAsync(department, authorization.UserId, "draft.save", null, now, cancellationToken);
        return saved.IsFailure ? saved.Error! : await projection.BuildAsync(department, cancellationToken);
    }
}

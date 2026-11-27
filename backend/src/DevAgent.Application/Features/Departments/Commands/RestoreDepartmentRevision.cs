using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Application.Features.Departments.Shared;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;
using DevAgent.Domain.Departments.Repositories;

namespace DevAgent.Application.Features.Departments.Commands;

public sealed record RestoreDepartmentRevisionCommand(Guid ProjectId, Guid DepartmentId, int Version, int ExpectedEditVersion);

/// <summary>Copies a previous revision into a new draft for review. History is never rewritten; publishing creates a new version.</summary>
internal sealed class RestoreDepartmentRevisionHandler(
    DepartmentAccess access,
    IItDepartmentRevisionRepository revisions,
    DepartmentChangeRecorder recorder,
    DepartmentDraftProjection projection,
    TimeProvider clock) : ICommandHandler<RestoreDepartmentRevisionCommand, DepartmentDraftDto>
{
    public async Task<Result<DepartmentDraftDto>> HandleAsync(RestoreDepartmentRevisionCommand command, CancellationToken cancellationToken)
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

        var revision = await revisions.GetAsync(department.ProjectId, department.Id, command.Version, cancellationToken);
        if (revision is null)
        {
            return Error.NotFound("department_revision", command.Version);
        }

        var now = clock.GetUtcNow();
        department.RestoreRevision(revision, command.ExpectedEditVersion, now);
        var saved = await recorder.CommitAsync(department, authorization.UserId, "restore", revision.Version, now, cancellationToken);
        return saved.IsFailure ? saved.Error! : await projection.BuildAsync(department, cancellationToken);
    }
}

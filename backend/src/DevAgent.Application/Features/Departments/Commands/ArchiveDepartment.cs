using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Application.Features.Departments.Shared;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;

namespace DevAgent.Application.Features.Departments.Commands;

public sealed record ArchiveDepartmentCommand(Guid ProjectId, Guid DepartmentId, int ExpectedEditVersion);

/// <summary>Removes the department from the active directory. Revisions, tasks and executions are untouched.</summary>
internal sealed class ArchiveDepartmentHandler(DepartmentAccess access, DepartmentChangeRecorder recorder, TimeProvider clock)
    : ICommandHandler<ArchiveDepartmentCommand, DepartmentEditResultDto>
{
    public async Task<Result<DepartmentEditResultDto>> HandleAsync(ArchiveDepartmentCommand command, CancellationToken cancellationToken)
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

        var now = clock.GetUtcNow();
        department.Archive(command.ExpectedEditVersion, now);
        return await recorder.CommitAsync(department, authorization.UserId, "archive", department.PublishedVersion, now, cancellationToken);
    }
}

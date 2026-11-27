using System.Text.Json;
using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Auditing.Repositories;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;

namespace DevAgent.Application.Features.Departments.Shared;

/// <summary>
/// Commits a department change together with its audit entry in one transaction and converts lost races into 409:
/// a stale <c>edit_version</c>, a competing revision insert or a slug taken by a concurrent create.
/// </summary>
internal sealed class DepartmentChangeRecorder(IAuditLog auditLog, IUnitOfWork unitOfWork)
{
    public const string SlugConstraint = "ux_it_departments_project_slug";

    public async Task<Result<DepartmentEditResultDto>> CommitAsync(
        ItDepartment department,
        UserId actor,
        string action,
        int? version,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        auditLog.Add(AuditEntry.Record(
            Actor.ForUser(actor),
            $"department.{action}",
            "department",
            department.Id.ToString(),
            AuditOutcome.Allowed,
            now,
            department.ProjectId,
            reason: version is { } v ? $"v{v}" : null,
            dataJson: JsonSerializer.Serialize(new { departmentId = department.Id.Value, slug = department.Slug, version })));

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException ex) when (ex.ConstraintName == SlugConstraint)
        {
            return Error.Conflict("department.slug_taken", "Slug đã được dùng cho một phòng ban khác trong project.");
        }
        catch (DuplicateKeyException)
        {
            return Error.Conflict("department.edit_conflict", "Một phiên bản khác vừa được xuất bản. Tải lại rồi thử lại.");
        }
        catch (ConcurrencyConflictException)
        {
            return Error.Conflict("department.edit_conflict", "Phòng ban vừa được người khác cập nhật. Tải lại rồi thử lại.");
        }

        return new DepartmentEditResultDto(department.Id.Value, department.Slug, department.EditVersion, department.PublishedVersion, department.Draft is not null);
    }
}

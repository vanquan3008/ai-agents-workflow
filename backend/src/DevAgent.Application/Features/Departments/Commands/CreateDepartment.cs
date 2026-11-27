using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Application.Features.Departments.Shared;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;
using DevAgent.Domain.Departments.Repositories;
using DevAgent.Domain.Departments.Rules;
using DevAgent.Domain.Departments.Templates;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Departments.Commands;

/// <param name="Template">Built-in template key, for example SOFTWARE_DEVELOPMENT.</param>
/// <param name="Name">Optional display name; defaults to the template name.</param>
/// <param name="RepositoryIds">Repositories to follow; defaults to every active repository of the project.</param>
public sealed record CreateDepartmentCommand(Guid ProjectId, string Template, string? Name, string Slug, IReadOnlyList<Guid>? RepositoryIds);

/// <summary>Creates a department from a template as an unpublished draft that only managers can see.</summary>
internal sealed class CreateDepartmentHandler(
    DepartmentAccess access,
    IDepartmentQueries queries,
    IItDepartmentRepository departments,
    DepartmentChangeRecorder recorder,
    TimeProvider clock) : ICommandHandler<CreateDepartmentCommand, DepartmentEditResultDto>
{
    public async Task<Result<DepartmentEditResultDto>> HandleAsync(CreateDepartmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var projectId = new ProjectId(command.ProjectId);
        var authorization = await access.AuthorizeProjectAsync(projectId, ProjectPermission.ManageDepartments, cancellationToken);
        if (authorization.IsFailure)
        {
            return authorization.Error!;
        }

        if (!ApiNames.TryParse<DepartmentTemplate>(command.Template, out var template))
        {
            return Error.Field("department.template_invalid", "template", "Mẫu phòng ban không hợp lệ.");
        }

        var slug = ItDepartment.NormalizeSlug(command.Slug);
        if (!ItDepartment.IsValidSlug(slug))
        {
            return Error.Field("department.slug_invalid", "slug", "Slug chỉ gồm chữ thường, số và dấu gạch ngang, tối đa 64 ký tự.");
        }

        if (await departments.SlugExistsAsync(projectId, slug, cancellationToken))
        {
            return Error.Conflict("department.slug_taken", "Slug đã được dùng cho một phòng ban khác trong project.");
        }

        var projectRepositories = await queries.ListProjectRepositoriesAsync(projectId, cancellationToken);
        var repositoryIds = command.RepositoryIds is { } requested
            ? requested.Select(id => new RepositoryId(id))
            : projectRepositories.Where(r => r.IsActive).Select(r => new RepositoryId(r.Id));

        var configuration = DepartmentTemplates.Build(template, command.Name, repositoryIds);
        var violations = configuration.Validate(DepartmentValidationLevel.Draft)
            .Concat(DepartmentMapping.ForeignRepositories(configuration, projectRepositories))
            .ToArray();
        if (violations.Length > 0)
        {
            return DepartmentMapping.ToValidationError(violations);
        }

        var now = clock.GetUtcNow();
        var department = ItDepartment.Create(projectId, slug, configuration, authorization.Value.UserId, now);
        departments.Add(department);
        return await recorder.CommitAsync(department, authorization.Value.UserId, "create", null, now, cancellationToken);
    }
}

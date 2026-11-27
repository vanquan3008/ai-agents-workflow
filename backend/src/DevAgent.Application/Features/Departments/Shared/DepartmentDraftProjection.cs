using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Domain.Departments;
using DevAgent.Domain.Departments.Layout;
using DevAgent.Domain.Departments.Rules;

namespace DevAgent.Application.Features.Departments.Shared;

/// <summary>Builds the editor view of a department: the saved draft, or a copy of the published revision when none exists.</summary>
internal sealed class DepartmentDraftProjection(IDepartmentQueries queries)
{
    public async Task<Result<DepartmentDraftDto>> BuildAsync(ItDepartment department, CancellationToken cancellationToken)
    {
        var configuration = department.Draft;
        if (configuration is null && department.PublishedVersion is { } version)
        {
            var revision = await queries.GetRevisionSnapshotAsync(department.ProjectId, department.Id, version, cancellationToken);
            configuration = revision is null ? null : ToConfiguration(revision);
        }

        if (configuration is null)
        {
            return Error.Failure("department.configuration_missing", "Phòng ban không có bản nháp hoặc phiên bản đã xuất bản.");
        }

        var repositories = await queries.ListProjectRepositoriesAsync(department.ProjectId, cancellationToken);
        var issues = configuration.Validate(DepartmentValidationLevel.Publish)
            .Concat(DepartmentMapping.ForeignRepositories(configuration, repositories));

        return new DepartmentDraftDto(
            department.Id.Value,
            department.ProjectId.Value,
            department.Slug,
            department.EditVersion,
            department.PublishedVersion,
            department.Draft is not null,
            DepartmentMapping.ToDto(configuration),
            DepartmentMapping.ToDto(issues),
            repositories,
            department.UpdatedAt);
    }

    private static DepartmentConfiguration ToConfiguration(DepartmentRevisionSnapshot revision) =>
        DepartmentMapping.FromDto(new DepartmentConfigDto(
            revision.Name,
            ApiNames.ToApi(revision.Theme),
            revision.RepositoryIds.Select(id => id.Value).ToArray(),
            revision.Rooms));
}

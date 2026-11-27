using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Application.Features.Departments.Shared;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments.Layout;
using DevAgent.Domain.Departments.Templates;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Departments.Queries;

public sealed record GetDepartmentEditorOptionsQuery(Guid ProjectId);

/// <summary>Grid limits, allowlists and built-in templates, so the editor mirrors the domain rules exactly.</summary>
internal sealed class GetDepartmentEditorOptionsHandler(DepartmentAccess access)
    : IQueryHandler<GetDepartmentEditorOptionsQuery, DepartmentEditorOptionsDto>
{
    public async Task<Result<DepartmentEditorOptionsDto>> HandleAsync(GetDepartmentEditorOptionsQuery query, CancellationToken cancellationToken)
    {
        var authorization = await access.AuthorizeProjectAsync(new ProjectId(query.ProjectId), ProjectPermission.ViewTasks, cancellationToken);
        if (authorization.IsFailure)
        {
            return authorization.Error!;
        }

        var templates = Enum.GetValues<DepartmentTemplate>()
            .Select(t => new DepartmentTemplateDto(
                ApiNames.ToApi(t),
                DepartmentTemplates.DisplayName(t),
                DepartmentTemplates.Description(t),
                DepartmentMapping.ToDto(DepartmentTemplates.Build(t, null, []))))
            .ToArray();

        return new DepartmentEditorOptionsDto(
            OfficeGrid.Columns,
            OfficeGrid.Rows,
            OfficeGrid.MinRooms,
            OfficeGrid.MaxRooms,
            OfficeGrid.MinRoomSize,
            OfficeGrid.NameMaxLength,
            OfficeGrid.RoomLabelMaxLength,
            Enum.GetValues<WorkflowStation>().Select(s => ApiNames.ToApi(s)).ToArray(),
            Enum.GetValues<DepartmentTheme>().Select(t => ApiNames.ToApi(t)).ToArray(),
            RoomAssetPresets.All,
            Enum.GetValues<RoomAction>().Select(a => ApiNames.ToApi(a)).ToArray(),
            templates);
    }
}

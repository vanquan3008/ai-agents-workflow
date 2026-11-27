using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments.Layout;
using DevAgent.Domain.Departments.Rules;

namespace DevAgent.Application.Features.Departments.Shared;

/// <summary>Converts between the API contract and the domain, and turns rule violations into one validation error.</summary>
public static class DepartmentMapping
{
    /// <summary>
    /// Builds the domain configuration without rejecting anything: unknown enum names become undefined values,
    /// so <see cref="DepartmentConfigurationRules"/> reports every problem in a single response.
    /// </summary>
    public static DepartmentConfiguration FromDto(DepartmentConfigDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var rooms = (dto.Rooms ?? []).Select(r => DepartmentRoom.Create(
            new DepartmentRoomId(r.RoomId),
            r.Label,
            r.Stage is null ? null : ParseOrUndefined<WorkflowStation>(r.Stage),
            r.X,
            r.Y,
            r.Width,
            r.Height,
            r.SortOrder,
            r.AssetPreset,
            r.Action is null ? null : ParseOrUndefined<RoomAction>(r.Action)));

        return DepartmentConfiguration.Create(
            dto.Name,
            ParseOrUndefined<DepartmentTheme>(dto.Theme),
            (dto.RepositoryIds ?? []).Select(id => new RepositoryId(id)),
            rooms);
    }

    public static DepartmentConfigDto ToDto(DepartmentConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new DepartmentConfigDto(
            configuration.Name,
            ApiNames.ToApi(configuration.Theme),
            configuration.RepositoryIds.Select(id => id.Value).ToArray(),
            configuration.Rooms.Select(ToDto).ToArray());
    }

    public static DepartmentRoomDto ToDto(DepartmentRoom room)
    {
        ArgumentNullException.ThrowIfNull(room);
        return new DepartmentRoomDto(
            room.Id.Value,
            room.Label,
            room.Station is { } station ? ApiNames.ToApi(station) : null,
            room.X,
            room.Y,
            room.Width,
            room.Height,
            room.SortOrder,
            room.AssetPreset,
            room.Action is { } action ? ApiNames.ToApi(action) : null);
    }

    public static IReadOnlyList<DepartmentRuleViolationDto> ToDto(IEnumerable<DepartmentRuleViolation> violations) =>
        violations.Select(v => new DepartmentRuleViolationDto(v.Code, v.Field, v.Message)).ToArray();

    /// <summary>One 400 response listing every violation by field, so the editor can mark each offending room.</summary>
    public static Error ToValidationError(IReadOnlyList<DepartmentRuleViolation> violations)
    {
        var details = violations
            .GroupBy(v => v.Field, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(v => v.Message).ToArray(), StringComparer.Ordinal);
        return Error.Validation(violations[0].Code, "Cấu hình phòng ban chưa hợp lệ.", details);
    }

    /// <summary>Repository ids that are not registered in the project, reported as a violation.</summary>
    public static IReadOnlyList<DepartmentRuleViolation> ForeignRepositories(
        DepartmentConfiguration configuration,
        IReadOnlyList<DepartmentRepositoryDto> projectRepositories)
    {
        var known = projectRepositories.Select(r => r.Id).ToHashSet();
        return configuration.RepositoryIds.Any(id => !known.Contains(id.Value))
            ? [new DepartmentRuleViolation("department.repository_not_in_project", "repositoryIds", "Repository không thuộc project này.")]
            : [];
    }

    public static IReadOnlyList<DepartmentRepositoryDto> SelectRepositories(
        IEnumerable<RepositoryId> repositoryIds,
        IReadOnlyList<DepartmentRepositoryDto> projectRepositories)
    {
        var selected = repositoryIds.Select(id => id.Value).ToHashSet();
        return projectRepositories.Where(r => selected.Contains(r.Id)).ToArray();
    }

    private static TEnum ParseOrUndefined<TEnum>(string? value)
        where TEnum : struct, Enum =>
        ApiNames.TryParse<TEnum>(value, out var parsed) ? parsed : default;
}

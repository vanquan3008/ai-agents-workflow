namespace DevAgent.Application.Features.Departments.Contracts;

// API contract for IT department offices. Enum values use UPPER_SNAKE like the rest of /api/v1.

/// <param name="Stage">Pipeline station shown by the room; null for an informational room.</param>
/// <param name="Action">Approved in-app destination of an informational room; null when the room has none.</param>
public sealed record DepartmentRoomDto(
    Guid RoomId,
    string Label,
    string? Stage,
    int X,
    int Y,
    int Width,
    int Height,
    int SortOrder,
    string AssetPreset,
    string? Action);

public sealed record DepartmentConfigDto(string Name, string Theme, IReadOnlyList<Guid> RepositoryIds, IReadOnlyList<DepartmentRoomDto> Rooms);

public sealed record DepartmentSummaryDto(
    Guid Id,
    Guid ProjectId,
    string Slug,
    string Name,
    string? Theme,
    int? PublishedVersion,
    DateTimeOffset? PublishedAt,
    bool HasDraft,
    int RepositoryCount,
    int RoomCount,
    bool IsArchived,
    DateTimeOffset UpdatedAt);

/// <param name="CanManage">Whether the viewer may create and edit departments; drives the editing controls.</param>
public sealed record DepartmentDirectoryDto(IReadOnlyList<DepartmentSummaryDto> Items, bool CanManage);

public sealed record DepartmentRepositoryDto(Guid Id, string FullName, bool IsActive);

public sealed record PublishedDepartmentDto(
    Guid Id,
    Guid ProjectId,
    string Slug,
    int Version,
    string Name,
    string Theme,
    IReadOnlyList<DepartmentRoomDto> Rooms,
    IReadOnlyList<DepartmentRepositoryDto> Repositories,
    DateTimeOffset PublishedAt,
    bool CanManage);

public sealed record DepartmentRuleViolationDto(string Code, string Field, string Message);

/// <param name="HasDraft">False when <paramref name="Config"/> is a copy of the published revision rather than saved work.</param>
/// <param name="PublishIssues">Rules the configuration must still satisfy before it can be published.</param>
public sealed record DepartmentDraftDto(
    Guid Id,
    Guid ProjectId,
    string Slug,
    int EditVersion,
    int? PublishedVersion,
    bool HasDraft,
    DepartmentConfigDto Config,
    IReadOnlyList<DepartmentRuleViolationDto> PublishIssues,
    IReadOnlyList<DepartmentRepositoryDto> AvailableRepositories,
    DateTimeOffset UpdatedAt);

public sealed record DepartmentEditResultDto(Guid Id, string Slug, int EditVersion, int? PublishedVersion, bool HasDraft);

public sealed record DepartmentRevisionDto(
    int Version,
    string Name,
    string Theme,
    int RoomCount,
    int RepositoryCount,
    Guid PublishedBy,
    string PublishedByName,
    DateTimeOffset PublishedAt,
    bool IsCurrent);

public sealed record DepartmentTemplateDto(string Key, string Name, string Description, DepartmentConfigDto Config);

/// <summary>Editor limits and allowlists, so the UI enforces exactly the rules the domain validates.</summary>
public sealed record DepartmentEditorOptionsDto(
    int Columns,
    int Rows,
    int MinRooms,
    int MaxRooms,
    int MinRoomSize,
    int NameMaxLength,
    int RoomLabelMaxLength,
    IReadOnlyList<string> Stages,
    IReadOnlyList<string> Themes,
    IReadOnlyList<string> AssetPresets,
    IReadOnlyList<string> Actions,
    IReadOnlyList<DepartmentTemplateDto> Templates);

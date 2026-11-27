using DevAgent.Domain.Common;
using DevAgent.Domain.Departments.Layout;

namespace DevAgent.Domain.Departments.Templates;

/// <summary>Built-in starting layouts. Each places all six stations, so a new department can be published unchanged.</summary>
public static class DepartmentTemplates
{
    public static string DisplayName(DepartmentTemplate template) => template switch
    {
        DepartmentTemplate.SoftwareDevelopment => "Phát triển phần mềm",
        DepartmentTemplate.Testing => "Kiểm thử",
        DepartmentTemplate.PlatformOperations => "Nền tảng & vận hành",
        _ => throw new ArgumentOutOfRangeException(nameof(template), template, null),
    };

    public static string Description(DepartmentTemplate template) => template switch
    {
        DepartmentTemplate.SoftwareDevelopment => "Quy trình đầy đủ từ tiếp nhận đến pull request, kèm bàn tiếp nhận và runbook.",
        DepartmentTemplate.Testing => "Nhấn mạnh kiểm thử và phê duyệt, kèm bàn tiếp nhận lỗi và hàng chờ duyệt.",
        DepartmentTemplate.PlatformOperations => "Theo dõi repository nền tảng, kèm phòng máy chủ và runbook vận hành.",
        _ => throw new ArgumentOutOfRangeException(nameof(template), template, null),
    };

    public static DepartmentConfiguration Build(DepartmentTemplate template, string? name, IEnumerable<RepositoryId> repositoryIds)
    {
        ArgumentNullException.ThrowIfNull(repositoryIds);
        var displayName = string.IsNullOrWhiteSpace(name) ? DisplayName(template) : name;
        return template switch
        {
            DepartmentTemplate.SoftwareDevelopment => DepartmentConfiguration.Create(displayName, DepartmentTheme.Indigo, repositoryIds,
            [
                .. StationRooms("Kiểm thử"),
                DepartmentRoom.Informational("Bàn tiếp nhận", RoomAssetPresets.ReceptionDesk, RoomAction.OpenTasks, 0, 10, 8, 6, 6),
                DepartmentRoom.Informational("Runbook", RoomAssetPresets.Bookshelf, RoomAction.OpenWorkflow, 16, 10, 8, 6, 7),
            ]),
            DepartmentTemplate.Testing => DepartmentConfiguration.Create(displayName, DepartmentTheme.Emerald, repositoryIds,
            [
                .. StationRooms("Phòng kiểm thử"),
                DepartmentRoom.Informational("Bàn tiếp nhận lỗi", RoomAssetPresets.ReceptionDesk, RoomAction.OpenTasks, 0, 10, 12, 6, 6),
                DepartmentRoom.Informational("Hàng chờ phê duyệt", RoomAssetPresets.Lounge, RoomAction.OpenApprovals, 12, 10, 12, 6, 7),
            ]),
            DepartmentTemplate.PlatformOperations => DepartmentConfiguration.Create(displayName, DepartmentTheme.Slate, repositoryIds,
            [
                .. StationRooms("Kiểm thử"),
                DepartmentRoom.Informational("Phòng máy chủ", RoomAssetPresets.ServerRack, RoomAction.OpenProject, 0, 10, 10, 6, 6),
                DepartmentRoom.Informational("Runbook vận hành", RoomAssetPresets.Bookshelf, RoomAction.OpenWorkflow, 10, 10, 14, 6, 7),
            ]),
            _ => throw new ArgumentOutOfRangeException(nameof(template), template, null),
        };
    }

    // A serpentine floor: planning, development and testing across the top, then review, approval and publishing back.
    private static DepartmentRoom[] StationRooms(string testingLabel) =>
    [
        DepartmentRoom.ForStation(WorkflowStation.Planning, "Lập kế hoạch", 0, 0, 8, 5, 0),
        DepartmentRoom.ForStation(WorkflowStation.Development, "Phát triển", 8, 0, 8, 5, 1),
        DepartmentRoom.ForStation(WorkflowStation.Testing, testingLabel, 16, 0, 8, 5, 2),
        DepartmentRoom.ForStation(WorkflowStation.Review, "Rà soát", 16, 5, 8, 5, 3),
        DepartmentRoom.ForStation(WorkflowStation.Approval, "Phê duyệt", 8, 5, 8, 5, 4),
        DepartmentRoom.ForStation(WorkflowStation.Publishing, "Xuất bản", 0, 5, 8, 5, 5),
    ];
}

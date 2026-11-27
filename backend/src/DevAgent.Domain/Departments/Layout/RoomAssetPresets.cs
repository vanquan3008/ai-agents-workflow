namespace DevAgent.Domain.Departments.Layout;

/// <summary>Allowlisted furniture presets. Keys map to local renderer assets; unknown keys are rejected.</summary>
public static class RoomAssetPresets
{
    public const string PlanningTable = "planning-table";
    public const string Workbench = "workbench";
    public const string TestBench = "test-bench";
    public const string DiffDisplay = "diff-display";
    public const string MeetingTable = "meeting-table";
    public const string ReleaseDesk = "release-desk";
    public const string ReceptionDesk = "reception-desk";
    public const string Bookshelf = "bookshelf";
    public const string Lounge = "lounge";
    public const string ServerRack = "server-rack";

    public static IReadOnlyList<string> All { get; } =
    [
        PlanningTable, Workbench, TestBench, DiffDisplay, MeetingTable, ReleaseDesk, ReceptionDesk, Bookshelf, Lounge, ServerRack,
    ];

    public static bool IsKnown(string? key) => key is not null && All.Contains(key, StringComparer.Ordinal);

    public static string DefaultFor(WorkflowStation station) => station switch
    {
        WorkflowStation.Planning => PlanningTable,
        WorkflowStation.Development => Workbench,
        WorkflowStation.Testing => TestBench,
        WorkflowStation.Review => DiffDisplay,
        WorkflowStation.Approval => MeetingTable,
        WorkflowStation.Publishing => ReleaseDesk,
        _ => throw new ArgumentOutOfRangeException(nameof(station), station, null),
    };
}

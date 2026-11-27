using DevAgent.Domain.Common;

namespace DevAgent.Domain.Departments.Layout;

/// <summary>
/// A rectangle on the office grid. A room bound to a <see cref="WorkflowStation"/> shows that pipeline step;
/// a room without a station is informational and may link to an approved in-app destination.
/// Validity is checked as part of the whole layout by <see cref="DepartmentConfigurationRules"/>.
/// </summary>
public sealed class DepartmentRoom
{
    private DepartmentRoom()
    {
        Label = string.Empty;
        AssetPreset = string.Empty;
    }

    private DepartmentRoom(
        DepartmentRoomId id,
        string label,
        WorkflowStation? station,
        int x,
        int y,
        int width,
        int height,
        int sortOrder,
        string assetPreset,
        RoomAction? action)
    {
        Id = id;
        Label = label;
        Station = station;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        SortOrder = sortOrder;
        AssetPreset = assetPreset;
        Action = action;
    }

    public DepartmentRoomId Id { get; private set; }

    public string Label { get; private set; }

    public WorkflowStation? Station { get; private set; }

    public int X { get; private set; }

    public int Y { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public int SortOrder { get; private set; }

    public string AssetPreset { get; private set; }

    public RoomAction? Action { get; private set; }

    public bool IsInformational => Station is null;

    public static DepartmentRoom Create(
        DepartmentRoomId id,
        string? label,
        WorkflowStation? station,
        int x,
        int y,
        int width,
        int height,
        int sortOrder,
        string? assetPreset,
        RoomAction? action) =>
        new(id, label?.Trim() ?? string.Empty, station, x, y, width, height, sortOrder, assetPreset?.Trim() ?? string.Empty, action);

    public bool Overlaps(DepartmentRoom other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return X < other.X + other.Width && other.X < X + Width && Y < other.Y + other.Height && other.Y < Y + Height;
    }

    /// <summary>A detached copy, so a draft and a revision never share persistence state.</summary>
    internal DepartmentRoom Copy() => new(Id, Label, Station, X, Y, Width, Height, SortOrder, AssetPreset, Action);

    internal bool FitsGrid() =>
        X >= 0 && Y >= 0 && Width >= OfficeGrid.MinRoomSize && Height >= OfficeGrid.MinRoomSize
        && X + Width <= OfficeGrid.Columns && Y + Height <= OfficeGrid.Rows;

    internal static DepartmentRoom ForStation(WorkflowStation station, string label, int x, int y, int width, int height, int sortOrder) =>
        new(DepartmentRoomId.New(), Guard.NotEmpty(label, "label", OfficeGrid.RoomLabelMaxLength), station, x, y, width, height, sortOrder,
            RoomAssetPresets.DefaultFor(station), null);

    internal static DepartmentRoom Informational(string label, string preset, RoomAction action, int x, int y, int width, int height, int sortOrder) =>
        new(DepartmentRoomId.New(), Guard.NotEmpty(label, "label", OfficeGrid.RoomLabelMaxLength), null, x, y, width, height, sortOrder, preset, action);
}

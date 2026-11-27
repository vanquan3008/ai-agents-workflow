namespace DevAgent.Domain.Departments.Layout;

/// <summary>Layout limits shared by the domain rules, the database constraints and the editor.</summary>
public static class OfficeGrid
{
    public const int Columns = 24;
    public const int Rows = 16;
    public const int MinRooms = 6;
    public const int MaxRooms = 12;
    public const int MinRoomSize = 2;
    public const int MaxRepositories = 50;
    public const int NameMaxLength = 100;
    public const int RoomLabelMaxLength = 60;
}

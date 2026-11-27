namespace DevAgent.Domain.Departments.Layout;

/// <summary>Stable room identity: the same room keeps its id across drafts and revisions so the UI can follow it.</summary>
public readonly record struct DepartmentRoomId(Guid Value)
{
    public static DepartmentRoomId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

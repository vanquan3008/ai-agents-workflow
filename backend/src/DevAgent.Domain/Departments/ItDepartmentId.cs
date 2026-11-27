namespace DevAgent.Domain.Departments;

public readonly record struct ItDepartmentId(Guid Value)
{
    public static ItDepartmentId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

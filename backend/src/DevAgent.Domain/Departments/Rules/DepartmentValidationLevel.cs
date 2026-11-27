namespace DevAgent.Domain.Departments.Rules;

public enum DepartmentValidationLevel
{
    /// <summary>Structural rules only, so an editor can save work in progress.</summary>
    Draft = 1,

    /// <summary>Draft rules plus completeness: every stage placed, room count, repositories and no overlaps.</summary>
    Publish = 2,
}

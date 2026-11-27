namespace DevAgent.Domain.Departments.Rules;

/// <summary>One broken layout rule, with a stable code and the offending field path (for example <c>rooms[2].width</c>).</summary>
public sealed record DepartmentRuleViolation(string Code, string Field, string Message);

namespace DevAgent.Domain.Common;

// Định danh kiểu mạnh: không thể truyền nhầm TaskId vào chỗ cần ProjectId.
// Guid v7 có thứ tự theo thời gian, thân thiện với chỉ mục B-tree của PostgreSQL.

public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct ProjectId(Guid Value)
{
    public static ProjectId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct RepositoryId(Guid Value)
{
    public static RepositoryId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct TaskId(Guid Value)
{
    public static TaskId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct TaskPlanId(Guid Value)
{
    public static TaskPlanId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct ExecutionId(Guid Value)
{
    public static ExecutionId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct ToolExecutionId(Guid Value)
{
    public static ToolExecutionId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct ApprovalId(Guid Value)
{
    public static ApprovalId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct AgentProfileId(Guid Value)
{
    public static AgentProfileId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct AgentInstanceId(Guid Value)
{
    public static AgentInstanceId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct AuditEntryId(Guid Value)
{
    public static AuditEntryId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

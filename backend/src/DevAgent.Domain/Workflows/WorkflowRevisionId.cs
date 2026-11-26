namespace DevAgent.Domain.Workflows;

public readonly record struct WorkflowRevisionId(Guid Value)
{
    public static WorkflowRevisionId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

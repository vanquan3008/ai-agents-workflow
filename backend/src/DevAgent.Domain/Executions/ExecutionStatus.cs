namespace DevAgent.Domain.Executions;

public enum ExecutionStatus
{
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
    TimedOut = 5,
    BudgetExceeded = 6,
}

using DevAgent.Domain.Common;

namespace DevAgent.Domain.Executions;

/// <summary>
/// One run of one agent step for a task. Stores a checkpoint so another worker can resume after a lease
/// expires, together with token usage, cost and duration.
/// </summary>
public sealed class AgentExecution : AggregateRoot<ExecutionId>
{
    private readonly List<ToolExecution> _toolExecutions = [];

    private AgentExecution()
    {
        Usage = TokenUsage.Zero;
        Model = string.Empty;
    }

    private AgentExecution(
        ExecutionId id,
        TaskId taskId,
        AgentStep step,
        int attempt,
        AgentProfileId? agentProfileId,
        string model,
        string? workerId,
        DateTimeOffset startedAt)
        : base(id)
    {
        TaskId = taskId;
        Step = step;
        Attempt = attempt;
        AgentProfileId = agentProfileId;
        Model = model;
        WorkerId = workerId;
        StartedAt = startedAt;
        Status = ExecutionStatus.Running;
        Usage = TokenUsage.Zero;
    }

    public TaskId TaskId { get; private set; }

    public AgentStep Step { get; private set; }

    public int Attempt { get; private set; }

    public AgentProfileId? AgentProfileId { get; private set; }

    public string Model { get; private set; }

    public string? WorkerId { get; private set; }

    public ExecutionStatus Status { get; private set; }

    public TokenUsage Usage { get; private set; }

    /// <summary>Model calls made in this run, counted against the per-task call limit.</summary>
    public int ModelCalls { get; private set; }

    /// <summary>Step-defined JSON progress marker used to resume work.</summary>
    public string? Checkpoint { get; private set; }

    public string? Error { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public TimeSpan? Duration => FinishedAt - StartedAt;

    public IReadOnlyCollection<ToolExecution> ToolExecutions => _toolExecutions.AsReadOnly();

    public static AgentExecution Start(
        TaskId taskId,
        AgentStep step,
        int attempt,
        AgentProfileId? agentProfileId,
        string model,
        string? workerId,
        DateTimeOffset now)
    {
        if (attempt < 1)
        {
            throw new DomainException("execution.attempt_invalid", "Attempt phải >= 1.");
        }

        return new AgentExecution(
            ExecutionId.New(),
            Guard.NotDefault(taskId, "task_id"),
            step,
            attempt,
            agentProfileId,
            Guard.NotEmpty(model, "model", 100),
            Guard.Optional(workerId, "worker_id", 200),
            Guard.Utc(now, "started_at"));
    }

    /// <summary>Counts a model call and adds its usage.</summary>
    public void RecordModelCall(TokenUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        EnsureRunning();
        Usage = Usage.Add(usage);
        ModelCalls++;
    }

    /// <summary>Adds usage reported by a call that was already counted.</summary>
    public void RecordUsage(TokenUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        EnsureRunning();
        Usage = Usage.Add(usage);
    }

    public void SaveCheckpoint(string checkpointJson)
    {
        EnsureRunning();
        Checkpoint = Guard.NotEmpty(checkpointJson, "checkpoint", 1_000_000);
    }

    public ToolExecution BeginTool(string toolName, string inputJson, DateTimeOffset now)
    {
        EnsureRunning();
        var tool = new ToolExecution(
            ToolExecutionId.New(),
            Id,
            _toolExecutions.Count + 1,
            Guard.NotEmpty(toolName, "tool_name", 100),
            inputJson,
            Guard.Utc(now, "tool_started_at"));
        _toolExecutions.Add(tool);
        return tool;
    }

    public void Succeed(DateTimeOffset now) => Finish(ExecutionStatus.Succeeded, null, now);

    public void Fail(string error, DateTimeOffset now) => Finish(ExecutionStatus.Failed, Guard.NotEmpty(error, "error", 8000), now);

    public void Cancel(DateTimeOffset now) => Finish(ExecutionStatus.Cancelled, "Task bị hủy.", now);

    public void TimeOut(DateTimeOffset now) => Finish(ExecutionStatus.TimedOut, "Hết thời gian cho phép.", now);

    public void ExceedBudget(string reason, DateTimeOffset now) =>
        Finish(ExecutionStatus.BudgetExceeded, Guard.NotEmpty(reason, "reason", 2000), now);

    private void Finish(ExecutionStatus status, string? error, DateTimeOffset now)
    {
        EnsureRunning();
        Status = status;
        Error = error;
        FinishedAt = Guard.Utc(now, "finished_at");
    }

    private void EnsureRunning()
    {
        if (Status != ExecutionStatus.Running)
        {
            throw new DomainException("execution.not_running", $"Execution đã kết thúc với trạng thái {Status}.");
        }
    }
}

using DevAgent.Domain.Common;
using DevAgent.Domain.Policies;
using DevAgent.Domain.Workflows;

namespace DevAgent.Domain.Tasks;

/// <summary>
/// A bug-fix request against one repository. Owns the lifecycle, plan versions, status history and PR link.
/// Executions and approvals are separate aggregates that reference the task by <see cref="TaskId"/>.
/// </summary>
public sealed class AgentTask : AggregateRoot<TaskId>
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 20_000;

    private readonly List<TaskStatusTransition> _transitions = [];
    private readonly List<TaskPlan> _plans = [];

    private AgentTask()
    {
        Title = string.Empty;
        Description = string.Empty;
        Source = null!;
        IdempotencyKey = null!;
    }

    private AgentTask(
        TaskId id,
        ProjectId projectId,
        RepositoryId repositoryId,
        string title,
        string description,
        TaskSource source,
        IdempotencyKey idempotencyKey,
        UserId requestedBy,
        DateTimeOffset createdAt)
        : base(id)
    {
        ProjectId = projectId;
        RepositoryId = repositoryId;
        Title = title;
        Description = description;
        Source = source;
        IdempotencyKey = idempotencyKey;
        RequestedBy = requestedBy;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        Status = AgentTaskStatus.Pending;
    }

    public ProjectId ProjectId { get; private set; }

    public RepositoryId RepositoryId { get; private set; }

    public string Title { get; private set; }

    public string Description { get; private set; }

    public TaskSource Source { get; private set; }

    public IdempotencyKey IdempotencyKey { get; private set; }

    public UserId RequestedBy { get; private set; }

    /// <summary>Commit captured when processing started; every diff and approval is computed against it.</summary>
    public string? BaseCommitSha { get; private set; }

    /// <summary>Dedicated working branch, e.g. <c>devagent/task-0193...</c>.</summary>
    public string? WorkBranch { get; private set; }

    public AgentTaskStatus Status { get; private set; }

    /// <summary>
    /// Workflow snapshot taken when processing started, so later repository workflow edits never affect a running task.
    /// Null until the task starts.
    /// </summary>
    public WorkflowDefinition? Workflow { get; private set; }

    public int? WorkflowVersion { get; private set; }

    public WorkflowDefinition EffectiveWorkflow => Workflow ?? WorkflowDefinition.Default;

    public string? StatusReason { get; private set; }

    public int RetryCount { get; private set; }

    public PullRequestLink? PullRequest { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public IReadOnlyCollection<TaskStatusTransition> Transitions => _transitions.AsReadOnly();

    public IReadOnlyCollection<TaskPlan> Plans => _plans.AsReadOnly();

    public TaskPlan? CurrentPlan => _plans.MaxBy(p => p.Version);

    public bool IsTerminal => AgentTaskStateMachine.IsTerminal(Status);

    /// <summary>
    /// Time spent in statuses the worker actively processes, excluding waits for approval, clarification
    /// or unblocking. Used for timeout enforcement.
    /// </summary>
    public TimeSpan ActiveDuration(DateTimeOffset now)
    {
        var ordered = _transitions.OrderBy(t => t.Sequence).ToArray();
        var total = TimeSpan.Zero;
        for (var i = 0; i < ordered.Length; i++)
        {
            if (!AgentTaskStateMachine.IsActive(ordered[i].To))
            {
                continue;
            }

            var end = i + 1 < ordered.Length ? ordered[i + 1].OccurredAt : now;
            if (end > ordered[i].OccurredAt)
            {
                total += end - ordered[i].OccurredAt;
            }
        }

        return total;
    }

    public static AgentTask Create(
        ProjectId projectId,
        RepositoryId repositoryId,
        string title,
        string description,
        TaskSource source,
        IdempotencyKey idempotencyKey,
        UserId requestedBy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(idempotencyKey);

        var task = new AgentTask(
            TaskId.New(),
            Guard.NotDefault(projectId, "project_id"),
            Guard.NotDefault(repositoryId, "repository_id"),
            Guard.NotEmpty(title, "title", TitleMaxLength),
            Guard.NotEmpty(description, "description", DescriptionMaxLength),
            source,
            idempotencyKey,
            Guard.NotDefault(requestedBy, "requested_by"),
            Guard.Utc(now, "created_at"));

        task.Record(null, AgentTaskStatus.Pending, Actor.ForUser(requestedBy), "Task được tạo.", now);
        task.Raise(new TaskCreated(task.Id, projectId, repositoryId, source.Channel, now));
        return task;
    }

    public void StartPlanning(string baseCommitSha, string workBranch, WorkflowDefinition workflow, int workflowVersion, Actor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        if (workflowVersion < 0)
        {
            throw new DomainException("task.workflow_version_invalid", "Phiên bản quy trình không hợp lệ.");
        }

        BaseCommitSha = CommitSha.Validate(baseCommitSha);
        WorkBranch = Guard.NotEmpty(workBranch, "work_branch", 255);
        Workflow = workflow;
        WorkflowVersion = workflowVersion;
        TransitionTo(AgentTaskStatus.Planning, actor, $"Bắt đầu lập kế hoạch (quy trình v{workflowVersion}).", now);
    }

    /// <summary>When the task last entered Planning, i.e. the start of the current planning round.</summary>
    public DateTimeOffset? CurrentPlanningStartedAt =>
        _transitions.Where(t => t.To == AgentTaskStatus.Planning).MaxBy(t => t.Sequence)?.OccurredAt;

    public TaskPlan AttachPlan(
        string summary,
        IReadOnlyList<string> steps,
        IReadOnlyList<string> acceptanceCriteria,
        IReadOnlyList<string> risks,
        IReadOnlyList<string> affectedPaths,
        PolicyDecision policyDecision,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(policyDecision);
        EnsureStatus(AgentTaskStatus.Planning);

        var plan = new TaskPlan(
            TaskPlanId.New(),
            (CurrentPlan?.Version ?? 0) + 1,
            Guard.NotEmpty(summary, "plan_summary", 4000),
            steps,
            acceptanceCriteria,
            risks,
            affectedPaths,
            policyDecision,
            Guard.Utc(now, "plan_created_at"));

        _plans.Add(plan);
        Raise(new TaskPlanCreated(Id, ProjectId, plan.Version, now));
        return plan;
    }

    public void RequestClarification(string question, Actor actor, DateTimeOffset now) =>
        TransitionTo(AgentTaskStatus.NeedsClarification, actor, Guard.NotEmpty(question, "question", 2000), now);

    public void ProvideClarification(string additionalInfo, UserId by, DateTimeOffset now)
    {
        EnsureStatus(AgentTaskStatus.NeedsClarification);
        var info = Guard.NotEmpty(additionalInfo, "additional_info", DescriptionMaxLength);
        Description = Guard.NotEmpty($"{Description}\n\n---\nBổ sung:\n{info}", "description", DescriptionMaxLength * 2);
        TransitionTo(AgentTaskStatus.Planning, Actor.ForUser(by), "Người dùng đã bổ sung thông tin.", now);
    }

    public void WaitForApproval(string reason, Actor actor, DateTimeOffset now) =>
        TransitionTo(AgentTaskStatus.WaitingForApproval, actor, Guard.NotEmpty(reason, "reason", 2000), now);

    public void StartCoding(Actor actor, string reason, DateTimeOffset now) =>
        TransitionTo(AgentTaskStatus.InProgress, actor, reason, now);

    public void StartTesting(Actor actor, DateTimeOffset now) =>
        TransitionTo(AgentTaskStatus.Testing, actor, "Chạy build/test.", now);

    public void StartReview(Actor actor, DateTimeOffset now) =>
        TransitionTo(AgentTaskStatus.Reviewing, actor, "Review diff và kết quả test.", now);

    public void StartPublishing(Actor actor, string reason, DateTimeOffset now) =>
        TransitionTo(AgentTaskStatus.Publishing, actor, reason, now);

    public void ReturnToPlanning(Actor actor, string reason, DateTimeOffset now) =>
        TransitionTo(AgentTaskStatus.Planning, actor, reason, now);

    /// <summary>Completion requires a stored pull request link.</summary>
    public void Complete(PullRequestLink pullRequest, Actor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        EnsureStatus(AgentTaskStatus.Publishing);
        PullRequest = pullRequest;
        CompletedAt = Guard.Utc(now, "completed_at");
        TransitionTo(AgentTaskStatus.Completed, actor, $"Đã mở PR #{pullRequest.Number}.", now);
    }

    /// <summary>Re-links an existing PR (recovery after a worker crash) without changing the status.</summary>
    public void LinkPullRequest(PullRequestLink pullRequest)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        if (PullRequest is not null && PullRequest.Number != pullRequest.Number)
        {
            throw new DomainException("task.pull_request_conflict", "Task đã liên kết với một PR khác.");
        }

        PullRequest = pullRequest;
    }

    public void Reject(UserId by, string reason, DateTimeOffset now) =>
        TransitionTo(AgentTaskStatus.Rejected, Actor.ForUser(by), Guard.NotEmpty(reason, "reason", 2000), now);

    public void Fail(string reason, Actor actor, DateTimeOffset now) =>
        TransitionTo(AgentTaskStatus.Failed, actor, Guard.NotEmpty(reason, "reason", 4000), now);

    public void Block(string reason, Actor actor, DateTimeOffset now) =>
        TransitionTo(AgentTaskStatus.Blocked, actor, Guard.NotEmpty(reason, "reason", 4000), now);

    public void TimeOut(string reason, DateTimeOffset now) =>
        TransitionTo(AgentTaskStatus.TimedOut, Actor.System, Guard.NotEmpty(reason, "reason", 2000), now);

    public void Cancel(UserId by, string? reason, DateTimeOffset now)
    {
        if (Status == AgentTaskStatus.Cancelled)
        {
            return;
        }

        TransitionTo(AgentTaskStatus.Cancelled, Actor.ForUser(by), reason ?? "Người dùng hủy task.", now);
    }

    public void Retry(UserId by, int maxRetries, DateTimeOffset now)
    {
        if (RetryCount >= maxRetries)
        {
            throw new DomainException("task.retry_exhausted", $"Đã dùng hết {maxRetries} lần thử lại.");
        }

        RetryCount++;
        TransitionTo(AgentTaskStatus.Pending, Actor.ForUser(by), $"Thử lại lần {RetryCount}.", now);
    }

    private void TransitionTo(AgentTaskStatus target, Actor actor, string? reason, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!AgentTaskStateMachine.CanTransition(Status, target))
        {
            throw new DomainException(
                "task.invalid_transition",
                $"Không thể chuyển task từ {Status} sang {target}.");
        }

        var from = Status;
        Status = target;
        StatusReason = reason;
        UpdatedAt = Guard.Utc(now, "updated_at");
        Record(from, target, actor, reason, now);
    }

    private void Record(AgentTaskStatus? from, AgentTaskStatus to, Actor actor, string? reason, DateTimeOffset now)
    {
        var sequence = _transitions.Count == 0 ? 1 : _transitions.Max(t => t.Sequence) + 1;
        _transitions.Add(new TaskStatusTransition(sequence, from, to, actor, reason, now));
        Raise(new TaskStatusChanged(Id, ProjectId, sequence, from, to, reason, now));
    }

    private void EnsureStatus(AgentTaskStatus expected)
    {
        if (Status != expected)
        {
            throw new DomainException("task.unexpected_status", $"Thao tác yêu cầu trạng thái {expected}, hiện tại là {Status}.");
        }
    }
}

using DevAgent.Domain.Common;
using DevAgent.Domain.Policies;

namespace DevAgent.Domain.Approvals;

/// <summary>
/// A request for human approval. The decision is bound to the approver, the time, the scope and the exact subject:
/// a plan version (<see cref="ApprovalKind.Plan"/>) or a commit SHA (<see cref="ApprovalKind.Diff"/>). When the
/// subject changes the old request is superseded. Approver permissions are checked by the application layer.
/// </summary>
public sealed class Approval : AggregateRoot<ApprovalId>
{
    private Approval()
    {
        Scope = string.Empty;
    }

    private Approval(
        ApprovalId id,
        TaskId taskId,
        ApprovalKind kind,
        int? planVersion,
        string? commitSha,
        SensitiveArea areas,
        int policyVersion,
        string scope,
        DateTimeOffset requestedAt,
        DateTimeOffset expiresAt)
        : base(id)
    {
        TaskId = taskId;
        Kind = kind;
        PlanVersion = planVersion;
        CommitSha = commitSha;
        SensitiveAreas = areas;
        PolicyVersion = policyVersion;
        Scope = scope;
        RequestedAt = requestedAt;
        ExpiresAt = expiresAt;
        Status = ApprovalStatus.Pending;
    }

    public TaskId TaskId { get; private set; }

    public ApprovalKind Kind { get; private set; }

    public int? PlanVersion { get; private set; }

    public string? CommitSha { get; private set; }

    public SensitiveArea SensitiveAreas { get; private set; }

    public int PolicyVersion { get; private set; }

    /// <summary>The policy reasons that made approval necessary.</summary>
    public string Scope { get; private set; }

    public ApprovalStatus Status { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public UserId? DecidedBy { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public string? Comment { get; private set; }

    public static Approval ForPlan(TaskId taskId, int planVersion, PolicyDecision decision, DateTimeOffset now, TimeSpan ttl)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (planVersion < 1)
        {
            throw new DomainException("approval.plan_version_invalid", "Phiên bản kế hoạch không hợp lệ.");
        }

        return Create(taskId, ApprovalKind.Plan, planVersion, null, decision, now, ttl);
    }

    public static Approval ForDiff(TaskId taskId, string commitSha, PolicyDecision decision, DateTimeOffset now, TimeSpan ttl)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return Create(taskId, ApprovalKind.Diff, null, Tasks.CommitSha.Validate(commitSha), decision, now, ttl);
    }

    public void Approve(UserId approver, string? comment, DateTimeOffset now) =>
        Decide(ApprovalStatus.Approved, approver, comment, now);

    public void Reject(UserId approver, string reason, DateTimeOffset now) =>
        Decide(ApprovalStatus.Rejected, approver, Guard.NotEmpty(reason, "reason", 2000), now);

    public void Expire(DateTimeOffset now)
    {
        if (Status == ApprovalStatus.Pending && now >= ExpiresAt)
        {
            Status = ApprovalStatus.Expired;
            DecidedAt = now;
        }
    }

    /// <summary>The subject changed after approval was requested, so this request no longer applies.</summary>
    public void Supersede(DateTimeOffset now)
    {
        if (Status is ApprovalStatus.Pending or ApprovalStatus.Approved)
        {
            Status = ApprovalStatus.Superseded;
            DecidedAt = now;
        }
    }

    public bool IsValidFor(int? planVersion, string? commitSha) =>
        Status == ApprovalStatus.Approved
        && (Kind != ApprovalKind.Plan || PlanVersion == planVersion)
        && (Kind != ApprovalKind.Diff || string.Equals(CommitSha, commitSha, StringComparison.OrdinalIgnoreCase));

    private static Approval Create(
        TaskId taskId,
        ApprovalKind kind,
        int? planVersion,
        string? commitSha,
        PolicyDecision decision,
        DateTimeOffset now,
        TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new DomainException("approval.ttl_invalid", "Thời hạn duyệt phải dương.");
        }

        var approval = new Approval(
            ApprovalId.New(),
            Guard.NotDefault(taskId, "task_id"),
            kind,
            planVersion,
            commitSha,
            decision.Areas,
            decision.PolicyVersion,
            Guard.NotEmpty(string.Join("\n", decision.Reasons), "scope", 8000),
            Guard.Utc(now, "requested_at"),
            now + ttl);

        approval.Raise(new ApprovalRequested(approval.Id, taskId, kind, now));
        return approval;
    }

    private void Decide(ApprovalStatus status, UserId approver, string? comment, DateTimeOffset now)
    {
        Guard.NotDefault(approver, "approver");
        Expire(now);
        if (Status != ApprovalStatus.Pending)
        {
            throw new DomainException("approval.not_pending", $"Yêu cầu duyệt đang ở trạng thái {Status}.");
        }

        Status = status;
        DecidedBy = approver;
        DecidedAt = Guard.Utc(now, "decided_at");
        Comment = Guard.Optional(comment, "comment", 2000);
        Raise(new ApprovalDecided(Id, TaskId, status, approver, now));
    }
}

using DevAgent.Domain.Common;
using DevAgent.Domain.Policies;

namespace DevAgent.Domain.Workflows;

/// <summary>
/// Declarative, versioned per-repository workflow. Immutable value object.
/// A workflow may only tighten the system safety rules, never relax them: every step always runs,
/// sensitive areas always need approval, agents never approve their own work, humans merge, nothing is deployed.
/// </summary>
public sealed record WorkflowDefinition
{
    public const int MaxPlannerAttempts = 5;
    public const int MaxFixIterationsLimit = 10;

    /// <summary>Always announced: approvers must learn about pending work and requesters must receive the PR link or failure.</summary>
    public static readonly IReadOnlyList<NotificationEvent> MandatoryNotifications =
        [NotificationEvent.ApprovalRequired, NotificationEvent.Completed, NotificationEvent.Failed];

    public WorkflowDefinition(
        ApprovalRequirement planApproval,
        ApprovalRequirement diffApproval,
        int plannerMaxAttempts,
        int maxFixIterations,
        FindingSeverity reviewerBlockingSeverity,
        bool requireRegressionTest,
        PullRequestSettings pullRequest,
        IReadOnlyList<NotificationEvent> notifyOn,
        StepModelOverrides models)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        ArgumentNullException.ThrowIfNull(notifyOn);
        ArgumentNullException.ThrowIfNull(models);

        if (!Enum.IsDefined(planApproval) || !Enum.IsDefined(diffApproval))
        {
            throw new DomainException("workflow.approval_invalid", "Yêu cầu duyệt không hợp lệ.");
        }

        if (plannerMaxAttempts is < 1 or > MaxPlannerAttempts)
        {
            throw new DomainException("workflow.planner_attempts_invalid", $"Số lần thử Planner phải trong khoảng 1..{MaxPlannerAttempts}.");
        }

        if (maxFixIterations is < 0 or > MaxFixIterationsLimit)
        {
            throw new DomainException("workflow.fix_iterations_invalid", $"Số vòng sửa phải trong khoảng 0..{MaxFixIterationsLimit}.");
        }

        // Blocking only on Critical findings would be looser than the High default.
        if (reviewerBlockingSeverity is < FindingSeverity.Low or > FindingSeverity.High)
        {
            throw new DomainException("workflow.reviewer_severity_invalid", "Ngưỡng chặn của Reviewer chỉ được là LOW, MEDIUM hoặc HIGH.");
        }

        PlanApproval = planApproval;
        DiffApproval = diffApproval;
        PlannerMaxAttempts = plannerMaxAttempts;
        MaxFixIterations = maxFixIterations;
        ReviewerBlockingSeverity = reviewerBlockingSeverity;
        RequireRegressionTest = requireRegressionTest;
        PullRequest = pullRequest;
        NotifyOn = notifyOn.Where(Enum.IsDefined).Union(MandatoryNotifications).Order().ToArray();
        Models = models;
    }

    public static WorkflowDefinition Default { get; } = new(
        ApprovalRequirement.OnSensitive,
        ApprovalRequirement.OnSensitive,
        plannerMaxAttempts: 3,
        maxFixIterations: 3,
        FindingSeverity.High,
        requireRegressionTest: true,
        new PullRequestSettings(draft: false, labels: ["devagent"], titlePrefix: "[DevAgent]", reviewers: []),
        Enum.GetValues<NotificationEvent>(),
        StepModelOverrides.None);

    public ApprovalRequirement PlanApproval { get; }

    public ApprovalRequirement DiffApproval { get; }

    public int PlannerMaxAttempts { get; }

    public int MaxFixIterations { get; }

    /// <summary>Reviewer findings at or above this severity block publishing.</summary>
    public FindingSeverity ReviewerBlockingSeverity { get; }

    /// <summary>The diff must touch a test file before it can be published.</summary>
    public bool RequireRegressionTest { get; }

    public PullRequestSettings PullRequest { get; }

    public IReadOnlyList<NotificationEvent> NotifyOn { get; }

    public StepModelOverrides Models { get; }

    public bool RequiresPlanApproval(PolicyDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return !decision.CanProceedAutomatically || PlanApproval == ApprovalRequirement.Always;
    }

    public bool RequiresDiffApproval(PolicyDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return !decision.CanProceedAutomatically || DiffApproval == ApprovalRequirement.Always;
    }

    public bool BlocksOn(FindingSeverity severity) => severity >= ReviewerBlockingSeverity;

    public bool ShouldNotify(NotificationEvent notificationEvent) => NotifyOn.Contains(notificationEvent);

    /// <summary>Fix iterations actually allowed, never above the system cap.</summary>
    public int EffectiveFixIterations(int systemCap) => Math.Min(MaxFixIterations, Math.Max(0, systemCap));

    public bool Equals(WorkflowDefinition? other) =>
        other is not null
        && PlanApproval == other.PlanApproval
        && DiffApproval == other.DiffApproval
        && PlannerMaxAttempts == other.PlannerMaxAttempts
        && MaxFixIterations == other.MaxFixIterations
        && ReviewerBlockingSeverity == other.ReviewerBlockingSeverity
        && RequireRegressionTest == other.RequireRegressionTest
        && PullRequest.Draft == other.PullRequest.Draft
        && PullRequest.TitlePrefix == other.PullRequest.TitlePrefix
        && PullRequest.Labels.SequenceEqual(other.PullRequest.Labels)
        && PullRequest.Reviewers.SequenceEqual(other.PullRequest.Reviewers)
        && NotifyOn.SequenceEqual(other.NotifyOn)
        && Models == other.Models;

    public override int GetHashCode() => HashCode.Combine(PlanApproval, DiffApproval, PlannerMaxAttempts, MaxFixIterations, ReviewerBlockingSeverity, RequireRegressionTest);
}

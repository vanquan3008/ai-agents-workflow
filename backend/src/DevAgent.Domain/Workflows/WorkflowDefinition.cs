using System.Text.RegularExpressions;
using DevAgent.Domain.Common;
using DevAgent.Domain.Policies;

namespace DevAgent.Domain.Workflows;

/// <summary>Khi nào cần người duyệt. Không có lựa chọn "không bao giờ": vùng nhạy cảm luôn phải duyệt.</summary>
public enum ApprovalRequirement
{
    /// <summary>Chỉ khi chính sách repo yêu cầu (chạm vùng nhạy cảm, ngoài phạm vi tự động, không phân loại được).</summary>
    OnSensitive = 1,

    /// <summary>Luôn duyệt, kể cả thay đổi nằm trong phạm vi tự động.</summary>
    Always = 2,
}

public enum FindingSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

/// <summary>Sự kiện có thể gửi thông báo ra kênh chat.</summary>
public enum NotificationEvent
{
    TaskAccepted = 1,
    ApprovalRequired = 2,
    ClarificationNeeded = 3,
    Completed = 4,
    Failed = 5,
    Blocked = 6,
    Cancelled = 7,
}

public sealed record PullRequestSettings
{
    public PullRequestSettings(bool draft, IReadOnlyList<string> labels, string titlePrefix, IReadOnlyList<string> reviewers)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(reviewers);

        if (labels.Count > 10)
        {
            throw new DomainException("workflow.pr_labels_too_many", "Tối đa 10 nhãn PR.");
        }

        if (reviewers.Count > 10)
        {
            throw new DomainException("workflow.pr_reviewers_too_many", "Tối đa 10 người review.");
        }

        Draft = draft;
        Labels = labels.Select(l => Guard.NotEmpty(l, "pr_label", 50)).Distinct(StringComparer.Ordinal).ToArray();
        TitlePrefix = Guard.Optional(titlePrefix, "pr_title_prefix", 50) ?? string.Empty;
        Reviewers = reviewers.Select(ValidateLogin).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public bool Draft { get; }

    public IReadOnlyList<string> Labels { get; }

    public string TitlePrefix { get; }

    /// <summary>GitHub login được yêu cầu review. Việc merge vẫn do người có thẩm quyền quyết định trên GitHub.</summary>
    public IReadOnlyList<string> Reviewers { get; }

    private static string ValidateLogin(string login)
    {
        var value = Guard.NotEmpty(login, "pr_reviewer", 39);
        return WorkflowPatterns.GitHubLogin().IsMatch(value)
            ? value
            : throw new DomainException("workflow.pr_reviewer_invalid", $"GitHub login không hợp lệ: {value}.");
    }
}

/// <summary>Model ghi đè theo bước; null dùng model mặc định của cấu hình hệ thống.</summary>
public sealed record StepModelOverrides
{
    public StepModelOverrides(string? planner, string? coding, string? reviewer)
    {
        Planner = Validate(planner, "planner");
        Coding = Validate(coding, "coding");
        Reviewer = Validate(reviewer, "reviewer");
    }

    public static StepModelOverrides None { get; } = new(null, null, null);

    public string? Planner { get; }

    public string? Coding { get; }

    public string? Reviewer { get; }

    private static string? Validate(string? model, string step)
    {
        var value = Guard.Optional(model, $"model_{step}", 100);
        return value is null || WorkflowPatterns.ModelId().IsMatch(value)
            ? value
            : throw new DomainException("workflow.model_invalid", $"Model cho bước {step} không hợp lệ: {value}.");
    }
}

/// <summary>
/// Quy trình xử lý task của một repo (mức 2: khai báo, có phiên bản). Value object bất biến.
/// Nguyên tắc: quy trình chỉ được <b>siết chặt hơn</b> các ràng buộc an toàn của hệ thống, không nới lỏng:
/// các bước Planner → Coding → Testing → Reviewer → Publisher luôn chạy; vùng nhạy cảm luôn cần duyệt;
/// agent không tự duyệt; con người merge; không triển khai production.
/// </summary>
public sealed record WorkflowDefinition
{
    public const int MaxPlannerAttempts = 5;
    public const int MaxFixIterationsLimit = 10;

    /// <summary>Sự kiện luôn được thông báo: người duyệt phải biết có việc chờ, người giao việc phải nhận link PR hoặc lỗi.</summary>
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

        // Chặn chỉ khi phát hiện Critical là nới lỏng so với mặc định (High).
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

    /// <summary>Phát hiện của Reviewer từ mức này trở lên chặn bước xuất bản.</summary>
    public FindingSeverity ReviewerBlockingSeverity { get; }

    /// <summary>Diff phải có thay đổi file test (test hồi quy) mới được xuất bản.</summary>
    public bool RequireRegressionTest { get; }

    public PullRequestSettings PullRequest { get; }

    public IReadOnlyList<NotificationEvent> NotifyOn { get; }

    public StepModelOverrides Models { get; }

    /// <summary>Kế hoạch có cần duyệt không: chính sách repo yêu cầu, hoặc quy trình yêu cầu luôn duyệt.</summary>
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

    /// <summary>Số vòng sửa hiệu lực: không vượt trần hệ thống.</summary>
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

internal static partial class WorkflowPatterns
{
    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9]|-(?=[A-Za-z0-9])){0,38}$", RegexOptions.CultureInvariant)]
    public static partial Regex GitHubLogin();

    [GeneratedRegex("^[a-z0-9][a-z0-9.\\-]{0,99}$", RegexOptions.CultureInvariant)]
    public static partial Regex ModelId();
}

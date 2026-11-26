namespace DevAgent.Application.Features.Workflows;

public sealed record PullRequestSettingsDto(bool Draft, IReadOnlyList<string> Labels, string TitlePrefix, IReadOnlyList<string> Reviewers);

public sealed record ModelOverridesDto(string? Planner, string? Coding, string? Reviewer);

public sealed record WorkflowDefinitionDto(
    string PlanApproval,
    string DiffApproval,
    int PlannerMaxAttempts,
    int MaxFixIterations,
    string ReviewerBlockingSeverity,
    bool RequireRegressionTest,
    PullRequestSettingsDto PullRequest,
    IReadOnlyList<string> NotifyOn,
    ModelOverridesDto Models);

/// <summary>System limits on workflows, exposed so the UI can render and validate before submitting.</summary>
public sealed record WorkflowLimitsDto(
    int PlannerMaxAttemptsCap,
    int MaxFixIterationsCap,
    IReadOnlyList<string> ApprovalOptions,
    IReadOnlyList<string> SeverityOptions,
    IReadOnlyList<string> NotificationOptions,
    IReadOnlyList<string> MandatoryNotifications,
    IReadOnlyList<string> Invariants);

public sealed record RepositoryWorkflowDto(Guid RepositoryId, string RepositoryFullName, int Version, bool IsDefault, WorkflowDefinitionDto Definition, WorkflowLimitsDto Limits);

public sealed record WorkflowRevisionDto(int Version, Guid ChangedBy, DateTimeOffset ChangedAt, string? Comment, WorkflowDefinitionDto Definition);

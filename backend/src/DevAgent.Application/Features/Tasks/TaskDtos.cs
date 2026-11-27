using DevAgent.Domain.Common;
using DevAgent.Domain.Tasks;

namespace DevAgent.Application.Features.Tasks;

public sealed record TaskSummaryDto(
    Guid Id,
    Guid ProjectId,
    Guid RepositoryId,
    string RepositoryFullName,
    string Title,
    string Status,
    string? StatusReason,
    string Channel,
    string? PullRequestUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PullRequestDto(int Number, string Url, string HeadBranch, string HeadCommitSha);

public sealed record TaskPlanDto(
    int Version,
    string Summary,
    IReadOnlyList<string> Steps,
    IReadOnlyList<string> AcceptanceCriteria,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> AffectedPaths,
    string PolicyOutcome,
    IReadOnlyList<string> SensitiveAreas,
    int PolicyVersion,
    IReadOnlyList<string> PolicyReasons,
    DateTimeOffset CreatedAt);

public sealed record UsageDto(long InputTokens, long OutputTokens, long CacheReadTokens, decimal EstimatedCostUsd);

public sealed record TaskDetailDto(
    Guid Id,
    Guid ProjectId,
    Guid RepositoryId,
    string RepositoryFullName,
    string Title,
    string Description,
    string Status,
    string? StatusReason,
    string Channel,
    string? SourceReference,
    Guid RequestedBy,
    string? BaseCommitSha,
    string? WorkBranch,
    int RetryCount,
    PullRequestDto? PullRequest,
    TaskPlanDto? CurrentPlan,
    UsageDto Usage,
    IReadOnlyList<string> AllowedNextStatuses,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);

public sealed record TimelineEntryDto(
    long Sequence,
    string? From,
    string To,
    string ActorKind,
    string ActorId,
    string? Reason,
    DateTimeOffset OccurredAt);

public sealed record TaskCreatedDto(Guid TaskId, string Status, bool Created);

/// <param name="RepositoryIds">When set, limits the list to these repositories (a department's followed repositories).</param>
public sealed record TaskListFilter(
    AgentTaskStatus? Status,
    RepositoryId? RepositoryId,
    string? Search,
    IReadOnlyCollection<RepositoryId>? RepositoryIds = null);

/// <summary>The project and repository a task belongs to, used for scoped authorization.</summary>
public sealed record TaskScope(ProjectId ProjectId, RepositoryId RepositoryId);

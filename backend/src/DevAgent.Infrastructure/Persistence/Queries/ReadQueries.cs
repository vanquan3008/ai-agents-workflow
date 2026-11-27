using DevAgent.Application.Common;
using DevAgent.Application.Features.Admin;
using DevAgent.Application.Features.Approvals;
using DevAgent.Application.Features.Executions;
using DevAgent.Application.Features.Projects;
using DevAgent.Application.Features.Tasks;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Common;
using DevAgent.Domain.Executions;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DevAgent.Infrastructure.Persistence.Queries;

/// <summary>Read side (CQRS-lite): no-tracking queries projected straight into DTOs.</summary>
internal sealed class ProjectQueries(DevAgentDbContext context) : IProjectQueries
{
    public Task<IReadOnlyList<ProjectDto>> ListForUserAsync(UserId userId, CancellationToken cancellationToken) =>
        ProjectRowsAsync(context.Projects.AsNoTracking().Where(p => p.Members.Any(m => m.UserId == userId)), userId, cancellationToken);

    public Task<IReadOnlyList<ProjectDto>> ListAllAsync(UserId viewer, CancellationToken cancellationToken) =>
        ProjectRowsAsync(context.Projects.AsNoTracking(), viewer, cancellationToken);

    public async Task<ProjectDto?> GetAsync(ProjectId projectId, UserId viewer, CancellationToken cancellationToken) =>
        (await ProjectRowsAsync(context.Projects.AsNoTracking().Where(p => p.Id == projectId), viewer, cancellationToken)).SingleOrDefault();

    public async Task<IReadOnlyList<ProjectId>> ListAllProjectIdsAsync(CancellationToken cancellationToken) =>
        await context.Projects.AsNoTracking().Select(p => p.Id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectMemberDto>> ListMembersAsync(ProjectId projectId, CancellationToken cancellationToken)
    {
        var rows = await context.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .SelectMany(p => p.Members)
            .Join(context.Users, m => m.UserId, u => u.Id, (m, u) => new { m.UserId, u.DisplayName, u.Email, m.Role, m.AddedAt })
            .OrderBy(r => r.DisplayName)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ProjectMemberDto(r.UserId.Value, r.DisplayName, r.Email, ApiNames.ToApi(r.Role), r.AddedAt)).ToArray();
    }

    public async Task<IReadOnlyList<ProjectId>> ListProjectIdsWithPermissionAsync(
        UserId userId,
        ProjectPermission permission,
        CancellationToken cancellationToken)
    {
        var roles = Enum.GetValues<ProjectRole>().Where(r => ProjectRolePermissions.Grants(r, permission)).ToArray();
        return await context.Projects.AsNoTracking()
            .Where(p => p.Members.Any(m => m.UserId == userId && roles.Contains(m.Role)))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RepositoryDto>> ListRepositoriesAsync(ProjectId projectId, CancellationToken cancellationToken)
    {
        var repositories = await context.Repositories.AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.Owner).ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);
        return repositories.Select(RepositoryDto.From).ToArray();
    }

    private async Task<IReadOnlyList<ProjectDto>> ProjectRowsAsync(IQueryable<Domain.Projects.Project> source, UserId viewer, CancellationToken cancellationToken)
    {
        var rows = await source
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                p.CreatedAt,
                Role = p.Members.Where(m => m.UserId == viewer).Select(m => (ProjectRole?)m.Role).FirstOrDefault(),
                MemberCount = p.Members.Count,
                RepositoryCount = context.Repositories.Count(r => r.ProjectId == p.Id),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ProjectDto(
            r.Id.Value,
            r.Name,
            r.Slug,
            r.Role is { } role ? ApiNames.ToApi(role) : "SUPER_ADMIN",
            r.MemberCount,
            r.RepositoryCount,
            r.CreatedAt)).ToArray();
    }
}

internal sealed class UserQueries(DevAgentDbContext context) : IUserQueries
{
    public async Task<PagedResult<UserAdminDto>> ListAsync(string? search, PageRequest page, CancellationToken cancellationToken)
    {
        var query = context.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var escaped = search.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
            var pattern = $"%{escaped}%";
            query = query.Where(u => EF.Functions.ILike(u.Email, pattern, "\\") || EF.Functions.ILike(u.DisplayName, pattern, "\\"));
        }

        var total = await query.LongCountAsync(cancellationToken);
        var items = await Project(query.OrderBy(u => u.Email).Skip(page.Skip).Take(page.PageSize)).ToListAsync(cancellationToken);
        return new PagedResult<UserAdminDto>(items.Select(Map).ToArray(), page.Page, page.PageSize, total);
    }

    public async Task<UserAdminDto?> GetAsync(UserId userId, CancellationToken cancellationToken)
    {
        var row = await Project(context.Users.AsNoTracking().Where(u => u.Id == userId)).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : Map(row);
    }

    private IQueryable<UserRow> Project(IQueryable<Domain.Users.User> source) => source.Select(u => new UserRow(
        u.Id,
        u.Email,
        u.DisplayName,
        u.SystemRole,
        u.IsActive,
        u.MustChangePassword,
        u.LockoutEndsAt,
        u.LastLoginAt,
        context.Projects.Count(p => p.Members.Any(m => m.UserId == u.Id)),
        u.CreatedAt));

    private static UserAdminDto Map(UserRow r) => new(
        r.Id.Value,
        r.Email,
        r.DisplayName,
        ApiNames.ToApi(r.Role),
        r.IsActive,
        r.MustChangePassword,
        r.LockedUntil is { } until && until > DateTimeOffset.UtcNow ? until : null,
        r.LastLoginAt,
        r.ProjectCount,
        r.CreatedAt);

    private sealed record UserRow(
        UserId Id,
        string Email,
        string DisplayName,
        Domain.Users.SystemRole Role,
        bool IsActive,
        bool MustChangePassword,
        DateTimeOffset? LockedUntil,
        DateTimeOffset? LastLoginAt,
        int ProjectCount,
        DateTimeOffset CreatedAt);
}

internal sealed class TaskQueries(DevAgentDbContext context) : ITaskQueries
{
    public async Task<PagedResult<TaskSummaryDto>> ListAsync(
        IReadOnlyCollection<ProjectId> projectIds,
        TaskListFilter filter,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
        {
            return new PagedResult<TaskSummaryDto>([], page.Page, page.PageSize, 0);
        }

        var ids = projectIds.ToArray();
        var query = context.Tasks.AsNoTracking().Where(t => ids.Contains(t.ProjectId));
        if (filter.Status is { } status)
        {
            query = query.Where(t => t.Status == status);
        }

        if (filter.RepositoryId is { } repositoryId)
        {
            query = query.Where(t => t.RepositoryId == repositoryId);
        }

        if (filter.RepositoryIds is { } repositoryIds)
        {
            // Translated to repository_id = ANY(@ids) and applied before pagination.
            var scoped = repositoryIds.ToArray();
            query = query.Where(t => scoped.Contains(t.RepositoryId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{EscapeLike(filter.Search)}%";
            query = query.Where(t => EF.Functions.ILike(t.Title, pattern, "\\"));
        }

        var total = await query.LongCountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Join(context.Repositories, t => t.RepositoryId, r => r.Id, (t, r) => new
            {
                t.Id,
                t.ProjectId,
                t.RepositoryId,
                r.Owner,
                RepoName = r.Name,
                t.Title,
                t.Status,
                t.StatusReason,
                t.Source.Channel,
                PullRequestUrl = t.PullRequest == null ? null : t.PullRequest.Url,
                t.CreatedAt,
                t.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new TaskSummaryDto(
            r.Id.Value,
            r.ProjectId.Value,
            r.RepositoryId.Value,
            $"{r.Owner}/{r.RepoName}",
            r.Title,
            ApiNames.ToApi(r.Status),
            r.StatusReason,
            ApiNames.ToApi(r.Channel),
            r.PullRequestUrl,
            r.CreatedAt,
            r.UpdatedAt)).ToArray();

        return new PagedResult<TaskSummaryDto>(items, page.Page, page.PageSize, total);
    }

    public async Task<TaskDetailDto?> GetAsync(TaskId taskId, CancellationToken cancellationToken)
    {
        var task = await context.Tasks.AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        var repository = await context.Repositories.AsNoTracking()
            .Where(r => r.Id == task.RepositoryId)
            .Select(r => new { r.Owner, r.Name })
            .FirstAsync(cancellationToken);

        var usage = await context.Executions.AsNoTracking()
            .Where(e => e.TaskId == taskId)
            .GroupBy(_ => 1)
            .Select(g => new UsageDto(
                g.Sum(e => e.Usage.InputTokens),
                g.Sum(e => e.Usage.OutputTokens),
                g.Sum(e => e.Usage.CacheReadTokens),
                g.Sum(e => e.Usage.EstimatedCostUsd)))
            .SingleOrDefaultAsync(cancellationToken) ?? new UsageDto(0, 0, 0, 0);

        var plan = task.CurrentPlan;
        return new TaskDetailDto(
            task.Id.Value,
            task.ProjectId.Value,
            task.RepositoryId.Value,
            $"{repository.Owner}/{repository.Name}",
            task.Title,
            task.Description,
            ApiNames.ToApi(task.Status),
            task.StatusReason,
            ApiNames.ToApi(task.Source.Channel),
            task.Source.ExternalReference,
            task.RequestedBy.Value,
            task.BaseCommitSha,
            task.WorkBranch,
            task.RetryCount,
            task.PullRequest is { } pr ? new PullRequestDto(pr.Number, pr.Url, pr.HeadBranch, pr.HeadCommitSha) : null,
            plan is null
                ? null
                : new TaskPlanDto(
                    plan.Version,
                    plan.Summary,
                    plan.Steps,
                    plan.AcceptanceCriteria,
                    plan.Risks,
                    plan.AffectedPaths,
                    ApiNames.ToApi(plan.PolicyOutcome),
                    ApiNames.FlagsToApi(plan.SensitiveAreas),
                    plan.PolicyVersion,
                    plan.PolicyReasons,
                    plan.CreatedAt),
            usage,
            AgentTaskStateMachine.NextStates(task.Status).Select(s => ApiNames.ToApi(s)).ToArray(),
            task.CreatedAt,
            task.UpdatedAt,
            task.CompletedAt);
    }

    public async Task<IReadOnlyList<TimelineEntryDto>> GetTimelineAsync(TaskId taskId, long afterSequence, int limit, CancellationToken cancellationToken)
    {
        var rows = await context.Tasks.AsNoTracking()
            .Where(t => t.Id == taskId)
            .SelectMany(t => t.Transitions)
            .Where(x => x.Sequence > afterSequence)
            .OrderBy(x => x.Sequence)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows.Select(x => new TimelineEntryDto(
            x.Sequence,
            x.From is { } from ? ApiNames.ToApi(from) : null,
            ApiNames.ToApi(x.To),
            ApiNames.ToApi(x.ActorKind),
            x.ActorId,
            x.Reason,
            x.OccurredAt)).ToArray();
    }

    public async Task<ProjectId?> GetProjectIdAsync(TaskId taskId, CancellationToken cancellationToken)
    {
        return await context.Tasks.AsNoTracking()
            .Where(t => t.Id == taskId)
            .Select(t => (ProjectId?)t.ProjectId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<TaskScope?> GetScopeAsync(TaskId taskId, CancellationToken cancellationToken) =>
        context.Tasks.AsNoTracking()
            .Where(t => t.Id == taskId)
            .Select(t => new TaskScope(t.ProjectId, t.RepositoryId))
            .SingleOrDefaultAsync(cancellationToken);

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}

internal sealed class ApprovalQueries(DevAgentDbContext context) : IApprovalQueries
{
    public async Task<PagedResult<ApprovalDto>> ListAsync(
        IReadOnlyCollection<ProjectId> projectIds,
        ApprovalStatus? status,
        TaskId? taskId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
        {
            return new PagedResult<ApprovalDto>([], page.Page, page.PageSize, 0);
        }

        var ids = projectIds.ToArray();
        var query = context.Approvals.AsNoTracking()
            .Join(context.Tasks, a => a.TaskId, t => t.Id, (a, t) => new { Approval = a, t.ProjectId, t.Title })
            .Where(x => ids.Contains(x.ProjectId));

        if (status is { } s)
        {
            query = query.Where(x => x.Approval.Status == s);
        }

        if (taskId is { } tid)
        {
            query = query.Where(x => x.Approval.TaskId == tid);
        }

        var total = await query.LongCountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.Approval.RequestedAt).Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<ApprovalDto>(rows.Select(r => Map(r.Approval, r.ProjectId, r.Title)).ToArray(), page.Page, page.PageSize, total);
    }

    public async Task<ApprovalDto?> GetAsync(ApprovalId approvalId, CancellationToken cancellationToken)
    {
        var row = await context.Approvals.AsNoTracking()
            .Where(a => a.Id == approvalId)
            .Join(context.Tasks, a => a.TaskId, t => t.Id, (a, t) => new { Approval = a, t.ProjectId, t.Title })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : Map(row.Approval, row.ProjectId, row.Title);
    }

    private static ApprovalDto Map(Approval a, ProjectId projectId, string title) => new(
        a.Id.Value,
        a.TaskId.Value,
        projectId.Value,
        title,
        ApiNames.ToApi(a.Kind),
        ApiNames.ToApi(a.Status),
        a.PlanVersion,
        a.CommitSha,
        ApiNames.FlagsToApi(a.SensitiveAreas),
        a.PolicyVersion,
        a.Scope,
        a.RequestedAt,
        a.ExpiresAt,
        a.DecidedBy?.Value,
        a.DecidedAt,
        a.Comment);
}

internal sealed class ExecutionQueries(DevAgentDbContext context) : IExecutionQueries
{
    public async Task<IReadOnlyList<ExecutionSummaryDto>> ListByTaskAsync(TaskId taskId, CancellationToken cancellationToken)
    {
        var rows = await context.Executions.AsNoTracking()
            .Where(e => e.TaskId == taskId)
            .OrderBy(e => e.StartedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(MapSummary).ToArray();
    }

    public async Task<ExecutionDetailDto?> GetAsync(ExecutionId executionId, CancellationToken cancellationToken)
    {
        var execution = await context.Executions.AsNoTracking().FirstOrDefaultAsync(e => e.Id == executionId, cancellationToken);
        if (execution is null)
        {
            return null;
        }

        var tools = execution.ToolExecutions
            .OrderBy(t => t.Sequence)
            .Select(t => new ToolExecutionDto(
                t.Id.Value,
                t.Sequence,
                t.ToolName,
                ApiNames.ToApi(t.Status),
                t.ExitCode,
                t.InputJson,
                t.OutputJson,
                t.DenialReason,
                t.StartedAt,
                t.FinishedAt))
            .ToArray();

        return new ExecutionDetailDto(MapSummary(execution), execution.Checkpoint, tools);
    }

    private static ExecutionSummaryDto MapSummary(AgentExecution e) => new(
        e.Id.Value,
        e.TaskId.Value,
        ApiNames.ToApi(e.Step),
        e.Attempt,
        ApiNames.ToApi(e.Status),
        e.Model,
        new UsageDto(e.Usage.InputTokens, e.Usage.OutputTokens, e.Usage.CacheReadTokens, e.Usage.EstimatedCostUsd),
        e.Error,
        e.StartedAt,
        e.FinishedAt,
        e.Duration?.TotalSeconds);
}

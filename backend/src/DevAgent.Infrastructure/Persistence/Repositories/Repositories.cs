using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Domain.Agents;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Common;
using DevAgent.Domain.Executions;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace DevAgent.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(DevAgentDbContext context) : IUserRepository
{
    public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken) =>
        context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> FindByExternalIdentityAsync(IdentityProvider provider, string externalId, CancellationToken cancellationToken) =>
        context.Users.FirstOrDefaultAsync(u => u.Identities.Any(i => i.Provider == provider && i.ExternalId == externalId), cancellationToken);

    public void Add(User user) => context.Users.Add(user);
}

internal sealed class ProjectRepository(DevAgentDbContext context) : IProjectRepository
{
    public Task<Project?> GetByIdAsync(ProjectId id, CancellationToken cancellationToken) =>
        context.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        context.Projects.AnyAsync(p => p.Slug == slug, cancellationToken);

    public void Add(Project project) => context.Projects.Add(project);
}

internal sealed class CodeRepositoryRepository(DevAgentDbContext context) : ICodeRepositoryRepository
{
    public Task<CodeRepository?> GetByIdAsync(RepositoryId id, CancellationToken cancellationToken) =>
        context.Repositories.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(string owner, string name, CancellationToken cancellationToken) =>
        context.Repositories.AnyAsync(r => r.Owner == owner && r.Name == name, cancellationToken);

    public void Add(CodeRepository repository) => context.Repositories.Add(repository);
}

internal sealed class TaskRepository(DevAgentDbContext context) : ITaskRepository
{
    // Owned collections (Transitions, Plans) được EF tải cùng aggregate.
    public Task<AgentTask?> GetByIdAsync(TaskId id, CancellationToken cancellationToken) =>
        context.Tasks.AsSplitQuery().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<AgentTask?> FindByIdempotencyKeyAsync(ProjectId projectId, IdempotencyKey key, CancellationToken cancellationToken) =>
        context.Tasks.AsSplitQuery().FirstOrDefaultAsync(t => t.ProjectId == projectId && t.IdempotencyKey == key, cancellationToken);

    public void Add(AgentTask task) => context.Tasks.Add(task);
}

internal sealed class ExecutionRepository(DevAgentDbContext context) : IExecutionRepository
{
    public Task<AgentExecution?> GetByIdAsync(ExecutionId id, CancellationToken cancellationToken) =>
        context.Executions.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AgentExecution>> ListRunningByTaskAsync(TaskId taskId, CancellationToken cancellationToken) =>
        await context.Executions.Where(e => e.TaskId == taskId && e.Status == ExecutionStatus.Running).ToListAsync(cancellationToken);

    public Task<int> CountAttemptsAsync(TaskId taskId, AgentStep step, CancellationToken cancellationToken) =>
        context.Executions.CountAsync(e => e.TaskId == taskId && e.Step == step, cancellationToken);

    public async Task<TokenUsage> GetTaskUsageAsync(TaskId taskId, CancellationToken cancellationToken)
    {
        var totals = await context.Executions
            .Where(e => e.TaskId == taskId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Input = g.Sum(e => e.Usage.InputTokens),
                Output = g.Sum(e => e.Usage.OutputTokens),
                CacheRead = g.Sum(e => e.Usage.CacheReadTokens),
                Cost = g.Sum(e => e.Usage.EstimatedCostUsd),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return totals is null ? TokenUsage.Zero : new TokenUsage(totals.Input, totals.Output, totals.CacheRead, totals.Cost);
    }

    public void Add(AgentExecution execution) => context.Executions.Add(execution);
}

internal sealed class ApprovalRepository(DevAgentDbContext context) : IApprovalRepository
{
    public Task<Approval?> GetByIdAsync(ApprovalId id, CancellationToken cancellationToken) =>
        context.Approvals.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Approval>> ListOpenByTaskAsync(TaskId taskId, CancellationToken cancellationToken) =>
        await context.Approvals
            .Where(a => a.TaskId == taskId && (a.Status == ApprovalStatus.Pending || a.Status == ApprovalStatus.Approved))
            .ToListAsync(cancellationToken);

    public void Add(Approval approval) => context.Approvals.Add(approval);
}

internal sealed class AgentInstanceRepository(DevAgentDbContext context) : IAgentInstanceRepository
{
    public Task<AgentInstance?> GetByWorkerIdAsync(string workerId, CancellationToken cancellationToken) =>
        context.AgentInstances.FirstOrDefaultAsync(i => i.WorkerId == workerId, cancellationToken);

    public void Add(AgentInstance instance) => context.AgentInstances.Add(instance);
}

internal sealed class AuditLog(DevAgentDbContext context) : IAuditLog
{
    public void Add(AuditEntry entry) => context.AuditEntries.Add(entry);
}

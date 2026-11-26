using DevAgent.Application.Abstractions.Integrations;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Projects;
using DevAgent.Domain.Agents;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Common;
using DevAgent.Domain.Executions;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Users;
using DevAgent.Domain.Workflows;

namespace DevAgent.Application.Tests.Fakes;

/// <summary>Kho dữ liệu trong bộ nhớ hiện thực các port của Application, đủ để kiểm thử use case mà không cần DB.</summary>
internal sealed class InMemoryStore :
    IUnitOfWork, IUserRepository, IProjectRepository, ICodeRepositoryRepository, ITaskRepository,
    IExecutionRepository, IApprovalRepository, IAgentInstanceRepository, IAuditLog, IJobQueue, IProjectQueries, IWorkflowRevisionRepository
{
    public List<User> Users { get; } = [];

    public List<Project> Projects { get; } = [];

    public List<CodeRepository> Repositories { get; } = [];

    public List<AgentTask> Tasks { get; } = [];

    public List<AgentExecution> Executions { get; } = [];

    public List<Approval> Approvals { get; } = [];

    public List<AgentInstance> Instances { get; } = [];

    public List<AuditEntry> Audit { get; } = [];

    public List<JobRequest> Jobs { get; } = [];

    public List<WorkflowRevision> Revisions { get; } = [];

    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    Task<User?> IUserRepository.GetByIdAsync(UserId id, CancellationToken cancellationToken) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<User?> FindByExternalIdentityAsync(IdentityProvider provider, string externalId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Identities.Any(i => i.Provider == provider && i.ExternalId == externalId)));

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Email == normalizedEmail));

    public Task<int> CountActiveSuperAdminsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Users.Count(u => u.IsSuperAdmin && u.IsActive));

    public void Add(User user) => Users.Add(user);

    Task<Project?> IProjectRepository.GetByIdAsync(ProjectId id, CancellationToken cancellationToken) => Task.FromResult(Projects.FirstOrDefault(p => p.Id == id));

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) => Task.FromResult(Projects.Any(p => p.Slug == slug));

    public void Add(Project project) => Projects.Add(project);

    Task<CodeRepository?> ICodeRepositoryRepository.GetByIdAsync(RepositoryId id, CancellationToken cancellationToken) =>
        Task.FromResult(Repositories.FirstOrDefault(r => r.Id == id));

    public Task<bool> ExistsAsync(string owner, string name, CancellationToken cancellationToken) =>
        Task.FromResult(Repositories.Any(r => r.Owner == owner && r.Name == name));

    public void Add(CodeRepository repository) => Repositories.Add(repository);

    Task<AgentTask?> ITaskRepository.GetByIdAsync(TaskId id, CancellationToken cancellationToken) => Task.FromResult(Tasks.FirstOrDefault(t => t.Id == id));

    public Task<AgentTask?> FindByIdempotencyKeyAsync(ProjectId projectId, IdempotencyKey key, CancellationToken cancellationToken) =>
        Task.FromResult(Tasks.FirstOrDefault(t => t.ProjectId == projectId && t.IdempotencyKey == key));

    public void Add(AgentTask task) => Tasks.Add(task);

    Task<AgentExecution?> IExecutionRepository.GetByIdAsync(ExecutionId id, CancellationToken cancellationToken) =>
        Task.FromResult(Executions.FirstOrDefault(e => e.Id == id));

    public Task<IReadOnlyList<AgentExecution>> ListRunningByTaskAsync(TaskId taskId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AgentExecution>>(Executions.Where(e => e.TaskId == taskId && e.Status == ExecutionStatus.Running).ToList());

    public Task<int> CountAttemptsAsync(TaskId taskId, AgentStep step, CancellationToken cancellationToken) =>
        Task.FromResult(Executions.Count(e => e.TaskId == taskId && e.Step == step));

    public Task<int> CountFailuresSinceAsync(TaskId taskId, AgentStep step, DateTimeOffset since, CancellationToken cancellationToken) =>
        Task.FromResult(Executions.Count(e => e.TaskId == taskId && e.Step == step && e.Status == ExecutionStatus.Failed && e.StartedAt >= since));

    public Task<TaskUsageTotals> GetTaskUsageAsync(TaskId taskId, CancellationToken cancellationToken)
    {
        var mine = Executions.Where(e => e.TaskId == taskId).ToList();
        return Task.FromResult(new TaskUsageTotals(mine.Aggregate(TokenUsage.Zero, (acc, e) => acc.Add(e.Usage)), mine.Sum(e => e.ModelCalls)));
    }

    public Task<IReadOnlyList<WorkflowRevision>> ListByRepositoryAsync(RepositoryId repositoryId, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WorkflowRevision>>(Revisions.Where(r => r.RepositoryId == repositoryId).OrderByDescending(r => r.Version).Take(limit).ToList());

    public void Add(WorkflowRevision revision) => Revisions.Add(revision);

    public void Add(AgentExecution execution) => Executions.Add(execution);

    Task<Approval?> IApprovalRepository.GetByIdAsync(ApprovalId id, CancellationToken cancellationToken) => Task.FromResult(Approvals.FirstOrDefault(a => a.Id == id));

    public Task<IReadOnlyList<Approval>> ListOpenByTaskAsync(TaskId taskId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Approval>>(Approvals
            .Where(a => a.TaskId == taskId && (a.Status == ApprovalStatus.Pending || a.Status == ApprovalStatus.Approved))
            .ToList());

    public void Add(Approval approval) => Approvals.Add(approval);

    public Task<AgentInstance?> GetByWorkerIdAsync(string workerId, CancellationToken cancellationToken) =>
        Task.FromResult(Instances.FirstOrDefault(i => i.WorkerId == workerId));

    public void Add(AgentInstance instance) => Instances.Add(instance);

    void IAuditLog.Add(AuditEntry entry) => Audit.Add(entry);

    public Task EnqueueAsync(JobRequest request, CancellationToken cancellationToken)
    {
        if (Jobs.All(j => j.DeduplicationKey != request.DeduplicationKey))
        {
            Jobs.Add(request);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProjectDto>> ListForUserAsync(UserId userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectDto>>(Projects.Where(p => p.RoleOf(userId) is not null).Select(p => Dto(p, userId)).ToList());

    public Task<IReadOnlyList<ProjectDto>> ListAllAsync(UserId viewer, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectDto>>(Projects.Select(p => Dto(p, viewer)).ToList());

    public Task<ProjectDto?> GetAsync(ProjectId projectId, UserId viewer, CancellationToken cancellationToken) =>
        Task.FromResult(Projects.Where(p => p.Id == projectId).Select(p => Dto(p, viewer)).FirstOrDefault());

    public Task<IReadOnlyList<ProjectId>> ListAllProjectIdsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectId>>(Projects.Select(p => p.Id).ToList());

    private ProjectDto Dto(Project p, UserId viewer) => new(
        p.Id.Value, p.Name, p.Slug, p.RoleOf(viewer) is { } role ? ApiNames.ToApi(role) : "SUPER_ADMIN",
        p.Members.Count, Repositories.Count(r => r.ProjectId == p.Id), p.CreatedAt);

    public Task<IReadOnlyList<ProjectMemberDto>> ListMembersAsync(ProjectId projectId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectMemberDto>>([]);

    public Task<IReadOnlyList<ProjectId>> ListProjectIdsWithPermissionAsync(UserId userId, ProjectPermission permission, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectId>>(Projects.Where(p => p.HasPermission(userId, permission)).Select(p => p.Id).ToList());

    public Task<IReadOnlyList<RepositoryDto>> ListRepositoriesAsync(ProjectId projectId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RepositoryDto>>(Repositories.Where(r => r.ProjectId == projectId).Select(RepositoryDto.From).ToList());
}

internal sealed class FakeCurrentUser : ICurrentUser
{
    public UserId? UserId { get; set; }
}

internal sealed class FixedSourceControl : ISourceControlGateway
{
    public const string HeadSha = "0123456789abcdef0123456789abcdef01234567";

    public Task<string> GetBranchHeadAsync(RepositoryCoordinates repository, string branch, CancellationToken cancellationToken) =>
        Task.FromResult(HeadSha);

    public Task<IReadOnlyList<RepositoryFile>> ListFilesAsync(RepositoryCoordinates repository, string commitSha, int maxFiles, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RepositoryFile>>([new("src/Tasks/TaskFilter.cs", 100)]);

    public Task<PullRequestInfo?> FindPullRequestAsync(RepositoryCoordinates repository, string headBranch, CancellationToken cancellationToken) =>
        Task.FromResult<PullRequestInfo?>(null);

    public Task<PullRequestInfo> CreatePullRequestAsync(CreatePullRequestRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

internal sealed class FakeChatNotifier : IChatNotifier
{
    public List<ChatNotification> Sent { get; } = [];

    public Task NotifyAsync(ChatNotification notification, CancellationToken cancellationToken)
    {
        Sent.Add(notification);
        return Task.CompletedTask;
    }
}

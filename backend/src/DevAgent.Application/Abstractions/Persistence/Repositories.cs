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

namespace DevAgent.Application.Abstractions.Persistence;

/// <summary>
/// Đơn vị giao dịch. Mọi thay đổi aggregate, job hàng đợi và bản ghi audit trong cùng một use case
/// được commit cùng lúc; domain event được phát sau khi commit thành công.
/// Ném <see cref="Common.DuplicateKeyException"/> hoặc <see cref="Common.ConcurrencyConflictException"/>.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken);

    Task<User?> FindByExternalIdentityAsync(IdentityProvider provider, string externalId, CancellationToken cancellationToken);

    /// <summary>Tìm theo email đã chuẩn hóa (chữ thường).</summary>
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<int> CountActiveSuperAdminsAsync(CancellationToken cancellationToken);

    void Add(User user);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> ListActiveByFamilyAsync(Guid familyId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> ListActiveByUserAsync(UserId userId, CancellationToken cancellationToken);

    void Add(RefreshToken token);
}

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(ProjectId id, CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    void Add(Project project);
}

public interface ICodeRepositoryRepository
{
    Task<CodeRepository?> GetByIdAsync(RepositoryId id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string owner, string name, CancellationToken cancellationToken);

    void Add(CodeRepository repository);
}

public interface ITaskRepository
{
    /// <summary>Tải task kèm kế hoạch và lịch sử trạng thái (toàn bộ aggregate).</summary>
    Task<AgentTask?> GetByIdAsync(TaskId id, CancellationToken cancellationToken);

    Task<AgentTask?> FindByIdempotencyKeyAsync(ProjectId projectId, IdempotencyKey key, CancellationToken cancellationToken);

    void Add(AgentTask task);
}

public interface IExecutionRepository
{
    Task<AgentExecution?> GetByIdAsync(ExecutionId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentExecution>> ListRunningByTaskAsync(TaskId taskId, CancellationToken cancellationToken);

    /// <summary>Tổng số lần chạy của một bước (đánh số attempt, giữ lịch sử).</summary>
    Task<int> CountAttemptsAsync(TaskId taskId, AgentStep step, CancellationToken cancellationToken);

    /// <summary>Số lần chạy thất bại của một bước kể từ <paramref name="since"/> (giới hạn thử lại trong lượt hiện tại).</summary>
    Task<int> CountFailuresSinceAsync(TaskId taskId, AgentStep step, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>Tổng token/chi phí/lần gọi model của task, dùng để kiểm tra ngân sách trước lần gọi tiếp theo.</summary>
    Task<TaskUsageTotals> GetTaskUsageAsync(TaskId taskId, CancellationToken cancellationToken);

    void Add(AgentExecution execution);
}

public sealed record TaskUsageTotals(TokenUsage Usage, int ModelCalls)
{
    public static TaskUsageTotals Zero { get; } = new(TokenUsage.Zero, 0);
}

public interface IWorkflowRevisionRepository
{
    Task<IReadOnlyList<WorkflowRevision>> ListByRepositoryAsync(RepositoryId repositoryId, int limit, CancellationToken cancellationToken);

    void Add(WorkflowRevision revision);
}

public interface IApprovalRepository
{
    Task<Approval?> GetByIdAsync(ApprovalId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Approval>> ListOpenByTaskAsync(TaskId taskId, CancellationToken cancellationToken);

    void Add(Approval approval);
}

public interface IAgentInstanceRepository
{
    Task<AgentInstance?> GetByWorkerIdAsync(string workerId, CancellationToken cancellationToken);

    void Add(AgentInstance instance);
}

/// <summary>Ghi audit append-only. Bản ghi được lưu cùng giao dịch của use case.</summary>
public interface IAuditLog
{
    void Add(AuditEntry entry);
}

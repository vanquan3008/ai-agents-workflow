using DevAgent.Domain.Agents;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Common;
using DevAgent.Domain.Executions;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Users;
using DevAgent.Infrastructure.Persistence.Converters;
using DevAgent.Infrastructure.Persistence.Jobs;
using Microsoft.EntityFrameworkCore;

namespace DevAgent.Infrastructure.Persistence;

/// <summary>
/// DbContext duy nhất của modular monolith. Mỗi module sở hữu bảng của mình qua file cấu hình riêng
/// trong thư mục Configurations; module khác chỉ tham chiếu bằng ID, không điều hướng chéo aggregate.
/// </summary>
public sealed class DevAgentDbContext(DbContextOptions<DevAgentDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<CodeRepository> Repositories => Set<CodeRepository>();

    public DbSet<AgentTask> Tasks => Set<AgentTask>();

    public DbSet<AgentExecution> Executions => Set<AgentExecution>();

    public DbSet<Approval> Approvals => Set<Approval>();

    public DbSet<AgentProfile> AgentProfiles => Set<AgentProfile>();

    public DbSet<AgentInstance> AgentInstances => Set<AgentInstance>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<JobRecord> Jobs => Set<JobRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("devagent");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DevAgentDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<UserId>().HaveConversion<UserIdConverter>();
        configurationBuilder.Properties<ProjectId>().HaveConversion<ProjectIdConverter>();
        configurationBuilder.Properties<RepositoryId>().HaveConversion<RepositoryIdConverter>();
        configurationBuilder.Properties<TaskId>().HaveConversion<TaskIdConverter>();
        configurationBuilder.Properties<TaskPlanId>().HaveConversion<TaskPlanIdConverter>();
        configurationBuilder.Properties<ExecutionId>().HaveConversion<ExecutionIdConverter>();
        configurationBuilder.Properties<ToolExecutionId>().HaveConversion<ToolExecutionIdConverter>();
        configurationBuilder.Properties<ApprovalId>().HaveConversion<ApprovalIdConverter>();
        configurationBuilder.Properties<AgentProfileId>().HaveConversion<AgentProfileIdConverter>();
        configurationBuilder.Properties<AgentInstanceId>().HaveConversion<AgentInstanceIdConverter>();
        configurationBuilder.Properties<AuditEntryId>().HaveConversion<AuditEntryIdConverter>();

        // Enum lưu dạng chuỗi: đọc được khi truy vấn tay và an toàn khi thêm giá trị mới.
        configurationBuilder.Properties<AgentTaskStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<TaskChannel>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<AgentStep>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ExecutionStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ToolExecutionStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ApprovalKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ApprovalStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ProjectRole>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<IdentityProvider>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<SourceControlProvider>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ActorKind>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<AuditOutcome>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<AgentInstanceStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<Domain.Policies.PolicyOutcome>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<JobStatus>().HaveConversion<string>().HaveMaxLength(20);

        configurationBuilder.Properties<decimal>().HavePrecision(18, 6);
    }
}

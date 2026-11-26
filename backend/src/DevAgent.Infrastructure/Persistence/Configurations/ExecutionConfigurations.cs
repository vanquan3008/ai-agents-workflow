using DevAgent.Domain.Agents;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Executions;
using DevAgent.Domain.Tasks;
using DevAgent.Infrastructure.Persistence.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevAgent.Infrastructure.Persistence.Configurations;

internal sealed class AgentExecutionConfiguration : IEntityTypeConfiguration<AgentExecution>
{
    public void Configure(EntityTypeBuilder<AgentExecution> builder)
    {
        builder.ToTable("agent_executions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Model).HasMaxLength(100).IsRequired();
        builder.Property(e => e.WorkerId).HasMaxLength(200);
        builder.Property(e => e.Checkpoint).HasColumnType("jsonb");
        builder.Property(e => e.Error).HasMaxLength(8000);
        builder.Ignore(e => e.Duration);
        builder.Ignore(e => e.DomainEvents);

        builder.OwnsOne(e => e.Usage, usage =>
        {
            usage.Property(u => u.InputTokens).HasColumnName("usage_input_tokens");
            usage.Property(u => u.OutputTokens).HasColumnName("usage_output_tokens");
            usage.Property(u => u.CacheReadTokens).HasColumnName("usage_cache_read_tokens");
            usage.Property(u => u.EstimatedCostUsd).HasColumnName("usage_cost_usd");
            usage.Ignore(u => u.TotalTokens);
        });
        builder.Navigation(e => e.Usage).IsRequired();

        builder.OwnsMany(e => e.ToolExecutions, tool =>
        {
            tool.ToTable("tool_executions");
            tool.WithOwner().HasForeignKey(t => t.ExecutionId);
            tool.HasKey(t => t.Id);
            tool.Property(t => t.Id).ValueGeneratedNever();
            tool.Property(t => t.ToolName).HasMaxLength(100).IsRequired();
            tool.Property(t => t.InputJson).HasColumnType("jsonb").IsRequired();
            tool.Property(t => t.OutputJson).HasColumnType("jsonb");
            tool.Property(t => t.DenialReason).HasMaxLength(2000);
            tool.HasIndex(t => new { t.ExecutionId, t.Sequence }).IsUnique();
        });
        builder.Navigation(e => e.ToolExecutions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<AgentTask>().WithMany().HasForeignKey(e => e.TaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AgentProfile>().WithMany().HasForeignKey(e => e.AgentProfileId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(e => new { e.TaskId, e.Step, e.Attempt });
        builder.HasIndex(e => new { e.TaskId, e.StartedAt });
        builder.HasIndex(e => e.Status).HasFilter("status = 'Running'");
        builder.HasXminConcurrencyToken();
    }
}

internal sealed class ApprovalConfiguration : IEntityTypeConfiguration<Approval>
{
    public void Configure(EntityTypeBuilder<Approval> builder)
    {
        builder.ToTable("approvals");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.CommitSha).HasMaxLength(64);
        builder.Property(a => a.Scope).HasMaxLength(8000).IsRequired();
        builder.Property(a => a.Comment).HasMaxLength(2000);
        builder.Ignore(a => a.DomainEvents);

        builder.HasOne<AgentTask>().WithMany().HasForeignKey(a => a.TaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => new { a.TaskId, a.Status });
        builder.HasIndex(a => new { a.Status, a.ExpiresAt });
        builder.HasXminConcurrencyToken();
    }
}

internal sealed class AgentProfileConfiguration : IEntityTypeConfiguration<AgentProfile>
{
    public void Configure(EntityTypeBuilder<AgentProfile> builder)
    {
        builder.ToTable("agent_profiles");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Model).HasMaxLength(100).IsRequired();
        builder.HasIndex(p => p.Name).IsUnique();
        builder.Ignore(p => p.DomainEvents);
    }
}

internal sealed class AgentInstanceConfiguration : IEntityTypeConfiguration<AgentInstance>
{
    public void Configure(EntityTypeBuilder<AgentInstance> builder)
    {
        builder.ToTable("agent_instances");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.WorkerId).HasMaxLength(200).IsRequired();
        builder.Property(i => i.HostName).HasMaxLength(200).IsRequired();
        builder.HasIndex(i => i.WorkerId).IsUnique();
        builder.HasIndex(i => new { i.Status, i.LastHeartbeatAt });
        builder.Ignore(i => i.DomainEvents);
    }
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.ActorId).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.ResourceType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.ResourceId).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Reason).HasMaxLength(4000);
        builder.Property(a => a.DataJson).HasColumnType("jsonb");
        builder.Property(a => a.TraceId).HasMaxLength(64);
        builder.HasIndex(a => new { a.TaskId, a.OccurredAt });
        builder.HasIndex(a => new { a.ProjectId, a.OccurredAt });
        builder.HasIndex(a => new { a.Outcome, a.OccurredAt });
    }
}

internal sealed class JobRecordConfiguration : IEntityTypeConfiguration<JobRecord>
{
    public void Configure(EntityTypeBuilder<JobRecord> builder)
    {
        builder.ToTable("jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Type).HasMaxLength(100).IsRequired();
        builder.Property(j => j.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(j => j.DeduplicationKey).HasMaxLength(300).IsRequired();
        builder.Property(j => j.LeasedBy).HasMaxLength(200);
        builder.Property(j => j.LastError).HasMaxLength(8000);

        builder.HasIndex(j => j.DeduplicationKey).IsUnique();

        // Chỉ mục riêng phần cho truy vấn nhận việc: nhỏ và nóng.
        builder.HasIndex(j => j.AvailableAt).HasFilter("status = 'Queued'").HasDatabaseName("ix_jobs_queued_available_at");
        builder.HasIndex(j => j.LeasedUntil).HasFilter("status = 'Leased'").HasDatabaseName("ix_jobs_leased_until");
        builder.HasIndex(j => new { j.Status, j.UpdatedAt });

        // Quét dead-letter chưa đối soát.
        builder.HasIndex(j => j.DeadLetteredAt).HasFilter("status = 'DeadLetter' AND reconciled_at IS NULL").HasDatabaseName("ix_jobs_unreconciled_dead_letters");
    }
}

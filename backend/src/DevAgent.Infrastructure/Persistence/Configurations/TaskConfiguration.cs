using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Users;
using DevAgent.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevAgent.Infrastructure.Persistence.Configurations;

internal sealed class TaskConfiguration : IEntityTypeConfiguration<AgentTask>
{
    public void Configure(EntityTypeBuilder<AgentTask> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).HasMaxLength(AgentTask.TitleMaxLength).IsRequired();
        builder.Property(t => t.Description).IsRequired();
        builder.Property(t => t.BaseCommitSha).HasMaxLength(64);
        builder.Property(t => t.WorkBranch).HasMaxLength(255);
        builder.Property(t => t.StatusReason).HasMaxLength(4000);
        builder.Property(t => t.IdempotencyKey)
            .HasConversion(k => k.Value, v => new IdempotencyKey(v))
            .HasMaxLength(IdempotencyKey.MaxLength)
            .IsRequired();

        builder.OwnsOne(t => t.Source, source =>
        {
            source.Property(s => s.Channel).HasColumnName("source_channel").IsRequired();
            source.Property(s => s.ExternalReference).HasColumnName("source_reference").HasMaxLength(500);
        });
        builder.Navigation(t => t.Source).IsRequired();

        builder.OwnsOne(t => t.PullRequest, pr =>
        {
            pr.Property(p => p.Number).HasColumnName("pr_number");
            pr.Property(p => p.Url).HasColumnName("pr_url").HasMaxLength(500);
            pr.Property(p => p.HeadBranch).HasColumnName("pr_head_branch").HasMaxLength(255);
            pr.Property(p => p.HeadCommitSha).HasColumnName("pr_head_commit_sha").HasMaxLength(64);
        });

        builder.OwnsMany(t => t.Transitions, transition =>
        {
            transition.ToTable("task_status_transitions");
            transition.WithOwner().HasForeignKey("TaskId");
            transition.HasKey("TaskId", nameof(TaskStatusTransition.Sequence));

            // The domain assigns this key. By default EF treats the non-FK part of an owned collection key as
            // database-generated, which would turn inserts of new rows into updates.
            transition.Property(x => x.Sequence).ValueGeneratedNever();
            transition.Property(x => x.ActorId).HasMaxLength(200).IsRequired();
            transition.Property(x => x.Reason).HasMaxLength(4000);

            // System-wide chronological timeline reads.
            transition.HasIndex(x => x.OccurredAt);
        });
        builder.Navigation(t => t.Transitions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(t => t.Plans, plan =>
        {
            plan.ToTable("task_plans");
            plan.WithOwner().HasForeignKey("TaskId");
            plan.HasKey(p => p.Id);
            plan.Property(p => p.Id).ValueGeneratedNever();
            plan.Property(p => p.Summary).HasMaxLength(4000).IsRequired();
            plan.HasIndex("TaskId", nameof(TaskPlan.Version)).IsUnique();
        });
        builder.Navigation(t => t.Plans).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(t => t.Workflow).HasColumnType("jsonb").HasConversion(WorkflowJson.NullableConverter, WorkflowJson.NullableComparer);
        builder.Ignore(t => t.EffectiveWorkflow);
        builder.Ignore(t => t.CurrentPlanningStartedAt);
        builder.Ignore(t => t.CurrentPlan);
        builder.Ignore(t => t.IsTerminal);
        builder.Ignore(t => t.DomainEvents);

        builder.HasOne<Project>().WithMany().HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CodeRepository>().WithMany()
            .HasForeignKey(t => new { t.ProjectId, t.RepositoryId })
            .HasPrincipalKey(r => new { r.ProjectId, r.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_tasks_code_repositories_project_scope");
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.RequestedBy).OnDelete(DeleteBehavior.Restrict);

        // Deduplication: one task per (project, idempotency key).
        builder.HasIndex(t => new { t.ProjectId, t.IdempotencyKey }).IsUnique();

        // Newest-first task lists per project/status, and status sweeps.
        builder.HasIndex(t => new { t.ProjectId, t.Status, t.CreatedAt }).IsDescending(false, false, true);
        builder.HasIndex(t => new { t.Status, t.UpdatedAt });
        builder.HasIndex(t => t.RepositoryId);

        // Department task lists: newest first within the followed repositories of one project.
        builder.HasIndex(t => new { t.ProjectId, t.RepositoryId, t.CreatedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_tasks_project_repository_created");

        builder.HasXminConcurrencyToken();
    }
}

using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Users;
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

            // Khóa do domain gán. Mặc định EF coi phần khóa không phải FK của owned collection là giá trị DB sinh,
            // dẫn tới UPDATE thay vì INSERT cho bản ghi mới.
            transition.Property(x => x.Sequence).ValueGeneratedNever();
            transition.Property(x => x.ActorId).HasMaxLength(200).IsRequired();
            transition.Property(x => x.Reason).HasMaxLength(4000);

            // Timeline đọc theo thời gian trên toàn hệ thống (dashboard vận hành).
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

        builder.Ignore(t => t.CurrentPlan);
        builder.Ignore(t => t.IsTerminal);
        builder.Ignore(t => t.DomainEvents);

        builder.HasOne<Project>().WithMany().HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CodeRepository>().WithMany().HasForeignKey(t => t.RepositoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.RequestedBy).OnDelete(DeleteBehavior.Restrict);

        // Chống trùng: cùng project + idempotency key chỉ có một task (checklist 0.3, nghiệm thu Phase 1).
        builder.HasIndex(t => new { t.ProjectId, t.IdempotencyKey }).IsUnique();

        // Danh sách task theo project/trạng thái mới nhất trước, và quét task theo trạng thái.
        builder.HasIndex(t => new { t.ProjectId, t.Status, t.CreatedAt }).IsDescending(false, false, true);
        builder.HasIndex(t => new { t.Status, t.UpdatedAt });
        builder.HasIndex(t => t.RepositoryId);

        builder.HasXminConcurrencyToken();
    }
}

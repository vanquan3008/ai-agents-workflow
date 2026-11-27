using System.Text.Json;
using DevAgent.Domain.Policies;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Users;
using DevAgent.Domain.Workflows;
using DevAgent.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevAgent.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500);
        builder.Property(u => u.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.Ignore(u => u.IsSuperAdmin);

        // The normalized (lower-case) email is the sign-in identifier.
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => new { u.SystemRole, u.IsActive });
        builder.HasXminConcurrencyToken();

        builder.OwnsMany(u => u.Identities, identity =>
        {
            identity.ToTable("user_identities");
            identity.WithOwner().HasForeignKey("UserId");
            identity.HasKey("UserId", nameof(ExternalIdentity.Provider));
            identity.Property(i => i.Provider).ValueGeneratedNever();
            identity.Property(i => i.ExternalId).HasMaxLength(200).IsRequired();

            // A Discord/GitHub account links to at most one DevAgent user.
            identity.HasIndex(i => new { i.Provider, i.ExternalId }).IsUnique();
        });
        builder.Navigation(u => u.Identities).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(u => u.DomainEvents);
    }
}

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Slug).HasMaxLength(64).IsRequired();
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasXminConcurrencyToken();

        builder.OwnsMany(p => p.Members, member =>
        {
            member.ToTable("project_members");
            member.WithOwner().HasForeignKey("ProjectId");
            member.HasKey("ProjectId", nameof(ProjectMember.UserId));
            member.Property(m => m.UserId).ValueGeneratedNever();

            // Serves "my projects" queries and permission checks.
            member.HasIndex(m => m.UserId);
        });
        builder.Navigation(p => p.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(p => p.DomainEvents);
    }
}

internal sealed class CodeRepositoryConfiguration : IEntityTypeConfiguration<CodeRepository>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<CodeRepository> builder)
    {
        builder.ToTable("code_repositories");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Owner).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.DefaultBranch).HasMaxLength(255).IsRequired();
        builder.Ignore(r => r.FullName);
        builder.HasIndex(r => new { r.Owner, r.Name }).IsUnique();
        builder.HasIndex(r => r.ProjectId);
        builder.HasIndex(r => r.InstallationId);
        builder.HasOne<Project>().WithMany().HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasXminConcurrencyToken();

        // The policy is an immutable, versioned value object stored as a single jsonb document.
        builder.Property(r => r.Policy)
            .HasColumnType("jsonb")
            .HasConversion(
                policy => JsonSerializer.Serialize(PolicySnapshot.From(policy), JsonOptions),
                json => JsonSerializer.Deserialize<PolicySnapshot>(json, JsonOptions)!.ToPolicy(),
                new ValueComparer<RepositoryPolicy>(
                    (a, b) => JsonSerializer.Serialize(PolicySnapshot.From(a!), JsonOptions) == JsonSerializer.Serialize(PolicySnapshot.From(b!), JsonOptions),
                    p => JsonSerializer.Serialize(PolicySnapshot.From(p), JsonOptions).GetHashCode(StringComparison.Ordinal),
                    p => p))
            .IsRequired();

        builder.Property(r => r.Workflow)
            .HasColumnType("jsonb")
            .HasConversion(WorkflowJson.Converter, WorkflowJson.Comparer)
            .IsRequired();

        builder.Ignore(r => r.DomainEvents);
    }

    private sealed record PolicySnapshot(
        int Version,
        string[] AutoFixPaths,
        SensitivePathRule[] SensitivePaths,
        string BuildCommand,
        string TestCommand,
        int MaxChangedFiles)
    {
        public static PolicySnapshot From(RepositoryPolicy policy) => new(
            policy.Version,
            [.. policy.AutoFixPaths],
            [.. policy.SensitivePaths],
            policy.BuildCommand,
            policy.TestCommand,
            policy.MaxChangedFiles);

        public RepositoryPolicy ToPolicy() => new(Version, AutoFixPaths, SensitivePaths, BuildCommand, TestCommand, MaxChangedFiles);
    }
}

internal sealed class WorkflowRevisionConfiguration : IEntityTypeConfiguration<WorkflowRevision>
{
    public void Configure(EntityTypeBuilder<WorkflowRevision> builder)
    {
        builder.ToTable("workflow_revisions");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Definition).HasColumnType("jsonb").HasConversion(WorkflowJson.Converter, WorkflowJson.Comparer).IsRequired();
        builder.Property(r => r.Comment).HasMaxLength(1000);
        builder.HasOne<CodeRepository>().WithMany().HasForeignKey(r => r.RepositoryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.ChangedBy).OnDelete(DeleteBehavior.Restrict);

        // One row per repository version; also serves newest-first history reads.
        builder.HasIndex(r => new { r.RepositoryId, r.Version }).IsUnique();
        builder.Ignore(r => r.DomainEvents);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.TokenHash).HasMaxLength(200).IsRequired();
        builder.Property(t => t.RevokedReason).HasMaxLength(100);
        builder.Property(t => t.CreatedByIp).HasMaxLength(64);
        builder.Property(t => t.UserAgent).HasMaxLength(500);
        builder.Ignore(t => t.WasRotated);
        builder.Ignore(t => t.DomainEvents);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => new { t.FamilyId, t.RevokedAt });
        builder.HasIndex(t => new { t.UserId, t.RevokedAt });
        builder.HasIndex(t => t.ExpiresAt);
    }
}

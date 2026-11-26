using System.Text.Json;
using DevAgent.Domain.Policies;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Users;
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
        builder.Property(u => u.Email).HasMaxLength(320);

        builder.OwnsMany(u => u.Identities, identity =>
        {
            identity.ToTable("user_identities");
            identity.WithOwner().HasForeignKey("UserId");
            identity.HasKey("UserId", nameof(ExternalIdentity.Provider));
            identity.Property(i => i.Provider).ValueGeneratedNever();
            identity.Property(i => i.ExternalId).HasMaxLength(200).IsRequired();

            // Một tài khoản Discord/GitHub chỉ liên kết với một người dùng DevAgent.
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

            // Phục vụ truy vấn "các project của tôi" và kiểm tra quyền.
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

        // Chính sách là value object bất biến có phiên bản: lưu nguyên khối dạng jsonb.
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

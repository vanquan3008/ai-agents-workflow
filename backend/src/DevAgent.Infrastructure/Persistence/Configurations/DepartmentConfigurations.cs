using DevAgent.Domain.Departments;
using DevAgent.Domain.Departments.Layout;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Users;
using DevAgent.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// IT department tables. Composite, project-scoped keys let PostgreSQL itself reject a revision, room or repository link
/// that crosses projects, even when a write bypasses the application.
/// </summary>
internal sealed class ItDepartmentConfiguration : IEntityTypeConfiguration<ItDepartment>
{
    public void Configure(EntityTypeBuilder<ItDepartment> builder)
    {
        builder.ToTable("it_departments", table =>
        {
            table.HasCheckConstraint("ck_it_departments_edit_version_positive", "edit_version > 0");
            table.HasCheckConstraint("ck_it_departments_published_version_positive", "published_version IS NULL OR published_version > 0");
            table.HasCheckConstraint("ck_it_departments_slug_format", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
            table.HasCheckConstraint("ck_it_departments_name_not_blank", "btrim(name) <> ''");

            // Drafts are validated by the domain; the database still bounds their shape and size.
            table.HasCheckConstraint(
                "ck_it_departments_draft_config_object",
                "draft_config IS NULL OR (jsonb_typeof(draft_config) = 'object' AND pg_column_size(draft_config) <= 65536)");
        });

        builder.HasKey(d => d.Id).HasName("pk_it_departments");
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Slug).HasMaxLength(ItDepartment.SlugMaxLength).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(OfficeGrid.NameMaxLength).IsRequired();
        builder.Property(d => d.EditVersion).IsConcurrencyToken();
        builder.Property(d => d.Draft)
            .HasColumnName("draft_config")
            .HasColumnType("jsonb")
            .HasConversion(DepartmentJson.NullableConverter, DepartmentJson.NullableComparer);
        builder.Ignore(d => d.IsArchived);
        builder.Ignore(d => d.IsPublished);
        builder.Ignore(d => d.DomainEvents);

        // Target of the scoped foreign keys from revisions.
        builder.HasAlternateKey(d => new { d.ProjectId, d.Id }).HasName("ak_it_departments_project_id_id");

        // Slugs stay reserved after archive so old links never resolve to a different department.
        builder.HasIndex(d => new { d.ProjectId, d.Slug }).IsUnique().HasDatabaseName("ux_it_departments_project_slug");

        builder.HasOne<Project>().WithMany().HasForeignKey(d => d.ProjectId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_it_departments_project");
        builder.HasOne<User>().WithMany().HasForeignKey(d => d.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_it_departments_created_by");

        // The published pointer can only reference a revision of this same department.
        builder.HasOne<ItDepartmentRevision>().WithMany()
            .HasForeignKey(d => new { d.ProjectId, d.Id, d.PublishedVersion })
            .HasPrincipalKey(r => new { r.ProjectId, r.DepartmentId, r.Version })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_it_departments_published_revision");
    }
}

internal sealed class ItDepartmentRevisionConfiguration : IEntityTypeConfiguration<ItDepartmentRevision>
{
    private static readonly string Stations = SqlList(Enum.GetNames<WorkflowStation>());
    private static readonly string Themes = SqlList(Enum.GetNames<DepartmentTheme>());
    private static readonly string Actions = SqlList(Enum.GetNames<RoomAction>());

    public void Configure(EntityTypeBuilder<ItDepartmentRevision> builder)
    {
        builder.ToTable("it_department_revisions", table =>
        {
            table.HasCheckConstraint("ck_it_department_revisions_version_positive", "version > 0");
            table.HasCheckConstraint("ck_it_department_revisions_name_not_blank", "btrim(name) <> ''");
            table.HasCheckConstraint("ck_it_department_revisions_theme", $"theme IN ({Themes})");
        });

        builder.HasKey(r => new { r.ProjectId, r.DepartmentId, r.Version }).HasName("pk_it_department_revisions");
        builder.Property(r => r.Version).ValueGeneratedNever();
        builder.Property(r => r.Name).HasMaxLength(OfficeGrid.NameMaxLength).IsRequired();

        builder.HasOne<ItDepartment>().WithMany()
            .HasForeignKey(r => new { r.ProjectId, r.DepartmentId })
            .HasPrincipalKey(d => new { d.ProjectId, d.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_it_department_revisions_department");
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.PublishedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_it_department_revisions_published_by");

        builder.OwnsMany(r => r.Rooms, ConfigureRooms);
        builder.Navigation(r => r.Rooms).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(r => r.Repositories, ConfigureRepositories);
        builder.Navigation(r => r.Repositories).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureRooms(OwnedNavigationBuilder<ItDepartmentRevision, DepartmentRoom> room)
    {
        room.ToTable("it_department_revision_rooms", table =>
        {
            table.HasCheckConstraint(
                "ck_it_department_revision_rooms_geometry",
                $"x >= 0 AND y >= 0 AND width > 0 AND height > 0 AND x + width <= {OfficeGrid.Columns} AND y + height <= {OfficeGrid.Rows}");
            table.HasCheckConstraint("ck_it_department_revision_rooms_sort_order", "sort_order >= 0");
            table.HasCheckConstraint("ck_it_department_revision_rooms_label_not_blank", "btrim(label) <> ''");
            table.HasCheckConstraint("ck_it_department_revision_rooms_stage", $"stage IS NULL OR stage IN ({Stations})");
            table.HasCheckConstraint("ck_it_department_revision_rooms_action", $"action IS NULL OR (stage IS NULL AND action IN ({Actions}))");
        });

        room.WithOwner().HasForeignKey("ProjectId", "DepartmentId", "Version").HasConstraintName("fk_it_department_revision_rooms_revision");
        room.HasKey("ProjectId", "DepartmentId", "Version", nameof(DepartmentRoom.Id)).HasName("pk_it_department_revision_rooms");
        room.Property(x => x.Id).HasColumnName("room_id").ValueGeneratedNever();
        room.Property(x => x.Station).HasColumnName("stage");
        room.Property(x => x.Label).HasMaxLength(OfficeGrid.RoomLabelMaxLength).IsRequired();
        room.Property(x => x.AssetPreset).HasMaxLength(40).IsRequired();
        room.Ignore(x => x.IsInformational);

        room.HasIndex("ProjectId", "DepartmentId", "Version", nameof(DepartmentRoom.SortOrder))
            .IsUnique()
            .HasDatabaseName("ux_it_department_revision_rooms_sort_order");
        room.HasIndex("ProjectId", "DepartmentId", "Version", nameof(DepartmentRoom.Station))
            .IsUnique()
            .HasFilter("stage IS NOT NULL")
            .HasDatabaseName("ux_it_department_revision_rooms_stage");
    }

    private static void ConfigureRepositories(OwnedNavigationBuilder<ItDepartmentRevision, RevisionRepositoryLink> link)
    {
        link.ToTable("it_department_revision_repositories");
        link.WithOwner().HasForeignKey("ProjectId", "DepartmentId", "Version").HasConstraintName("fk_it_department_revision_repositories_revision");
        link.HasKey("ProjectId", "DepartmentId", "Version", nameof(RevisionRepositoryLink.RepositoryId))
            .HasName("pk_it_department_revision_repositories");
        link.Property(x => x.RepositoryId).ValueGeneratedNever();

        // Sharing project_id with the owner key makes a link to another project's repository impossible.
        link.HasOne<CodeRepository>().WithMany()
            .HasForeignKey("ProjectId", nameof(RevisionRepositoryLink.RepositoryId))
            .HasPrincipalKey(r => new { r.ProjectId, r.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_it_department_revision_repositories_repository");
        link.HasIndex("ProjectId", nameof(RevisionRepositoryLink.RepositoryId))
            .HasDatabaseName("ix_it_department_revision_repositories_repository");
    }

    private static string SqlList(IEnumerable<string> values) => string.Join(", ", values.Select(v => $"'{v}'"));
}

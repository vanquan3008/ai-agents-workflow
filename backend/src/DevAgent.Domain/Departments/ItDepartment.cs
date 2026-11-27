using System.Text.RegularExpressions;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments.Layout;
using DevAgent.Domain.Departments.Rules;

namespace DevAgent.Domain.Departments;

/// <summary>
/// A configurable office view over a project's repositories. Owns the private draft, the pointer to the published
/// revision and the archive state. Every change requires the caller's <see cref="EditVersion"/> so concurrent editors
/// get a conflict instead of overwriting each other. Published revisions are separate, append-only snapshots.
/// </summary>
public sealed partial class ItDepartment : AggregateRoot<ItDepartmentId>
{
    public const int SlugMaxLength = 64;

    private ItDepartment()
    {
        Slug = string.Empty;
        Name = string.Empty;
    }

    private ItDepartment(ItDepartmentId id, ProjectId projectId, string slug, DepartmentConfiguration draft, UserId createdBy, DateTimeOffset now)
        : base(id)
    {
        ProjectId = projectId;
        Slug = slug;
        Name = draft.Name;
        Draft = draft;
        EditVersion = 1;
        CreatedBy = createdBy;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public ProjectId ProjectId { get; private set; }

    /// <summary>Stable, normalized identifier within the project; kept after archive so links never point elsewhere.</summary>
    public string Slug { get; private set; }

    /// <summary>Directory label: the latest published name, or the draft name until the first publication.</summary>
    public string Name { get; private set; }

    /// <summary>Optimistic concurrency token, incremented on every change.</summary>
    public int EditVersion { get; private set; }

    /// <summary>Private work in progress for department managers; null when nothing is pending.</summary>
    public DepartmentConfiguration? Draft { get; private set; }

    public int? PublishedVersion { get; private set; }

    public UserId CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    public bool IsPublished => PublishedVersion is not null;

    public static string NormalizeSlug(string? slug) => (slug ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsValidSlug(string slug) =>
        slug.Length is > 0 and <= SlugMaxLength && SlugPattern().IsMatch(slug);

    public static ItDepartment Create(ProjectId projectId, string slug, DepartmentConfiguration draft, UserId createdBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var normalizedSlug = NormalizeSlug(slug);
        if (!IsValidSlug(normalizedSlug))
        {
            throw new DomainException("department.slug_invalid", "Slug chỉ gồm chữ thường, số và dấu gạch ngang, tối đa 64 ký tự.");
        }

        EnsureValid(draft, DepartmentValidationLevel.Draft);
        return new ItDepartment(
            ItDepartmentId.New(),
            Guard.NotDefault(projectId, "project_id"),
            normalizedSlug,
            draft.Copy(),
            Guard.NotDefault(createdBy, "created_by"),
            Guard.Utc(now, "created_at"));
    }

    public void SaveDraft(DepartmentConfiguration draft, int expectedEditVersion, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(draft);
        EnsureEditable(expectedEditVersion);
        EnsureValid(draft, DepartmentValidationLevel.Draft);

        Draft = draft.Copy();
        if (!IsPublished)
        {
            Name = draft.Name;
        }

        Touch(now);
    }

    /// <summary>Validates the whole draft and turns it into the next numbered revision, which the caller must persist.</summary>
    public ItDepartmentRevision Publish(int expectedEditVersion, UserId publishedBy, DateTimeOffset now)
    {
        EnsureEditable(expectedEditVersion);
        var draft = Draft ?? throw new DomainException("department.no_draft", "Không có bản nháp để xuất bản.");
        EnsureValid(draft, DepartmentValidationLevel.Publish);

        var revision = new ItDepartmentRevision(
            ProjectId,
            Id,
            (PublishedVersion ?? 0) + 1,
            draft.Copy(),
            Guard.NotDefault(publishedBy, "published_by"),
            Guard.Utc(now, "published_at"));

        PublishedVersion = revision.Version;
        Name = draft.Name;
        Draft = null;
        Touch(now);
        return revision;
    }

    /// <summary>Copies a previous revision into a new draft; publishing it later creates a new version.</summary>
    public void RestoreRevision(ItDepartmentRevision revision, int expectedEditVersion, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(revision);
        EnsureEditable(expectedEditVersion);
        if (revision.ProjectId != ProjectId || revision.DepartmentId != Id)
        {
            throw new DomainException("department.revision_mismatch", "Phiên bản không thuộc phòng ban này.");
        }

        Draft = revision.ToConfiguration();
        Touch(now);
    }

    public void Archive(int expectedEditVersion, DateTimeOffset now)
    {
        EnsureEditable(expectedEditVersion);
        ArchivedAt = Guard.Utc(now, "archived_at");
        Touch(now);
    }

    private static void EnsureValid(DepartmentConfiguration configuration, DepartmentValidationLevel level)
    {
        var violations = configuration.Validate(level);
        if (violations.Count > 0)
        {
            throw new DomainException(violations[0].Code, violations[0].Message);
        }
    }

    private void EnsureEditable(int expectedEditVersion)
    {
        if (IsArchived)
        {
            throw new DomainException("department.archived", "Phòng ban đã được lưu trữ.");
        }

        if (expectedEditVersion != EditVersion)
        {
            throw new DomainException(
                "department.edit_conflict",
                $"Phòng ban đã được cập nhật lên phiên bản chỉnh sửa {EditVersion}; bạn đang sửa phiên bản {expectedEditVersion}.");
        }
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = Guard.Utc(now, "updated_at");
        EditVersion++;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}

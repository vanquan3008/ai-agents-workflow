using System.Text.RegularExpressions;
using DevAgent.Domain.Common;

namespace DevAgent.Domain.Projects;

public sealed class ProjectMember
{
    private ProjectMember()
    {
    }

    internal ProjectMember(UserId userId, ProjectRole role, DateTimeOffset addedAt)
    {
        UserId = userId;
        Role = role;
        AddedAt = addedAt;
    }

    public UserId UserId { get; private set; }

    public ProjectRole Role { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    internal void ChangeRole(ProjectRole role) => Role = role;
}

/// <summary>Project: ranh giới phân quyền. Sở hữu danh sách thành viên và vai trò.</summary>
public sealed partial class Project : AggregateRoot<ProjectId>
{
    private readonly List<ProjectMember> _members = [];

    private Project()
    {
        Name = string.Empty;
        Slug = string.Empty;
    }

    private Project(ProjectId id, string name, string slug, DateTimeOffset createdAt)
        : base(id)
    {
        Name = name;
        Slug = slug;
        CreatedAt = createdAt;
    }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<ProjectMember> Members => _members.AsReadOnly();

    public static Project Create(string name, string slug, UserId owner, DateTimeOffset now)
    {
        var normalizedSlug = Guard.NotEmpty(slug, "slug", 64).ToLowerInvariant();
        if (!SlugPattern().IsMatch(normalizedSlug))
        {
            throw new DomainException("project.slug_invalid", "Slug chỉ gồm chữ thường, số và dấu gạch ngang.");
        }

        var project = new Project(ProjectId.New(), Guard.NotEmpty(name, "name", 200), normalizedSlug, Guard.Utc(now, "created_at"));
        project._members.Add(new ProjectMember(Guard.NotDefault(owner, "owner"), ProjectRole.Owner, now));
        return project;
    }

    public void AddOrUpdateMember(UserId userId, ProjectRole role, DateTimeOffset now)
    {
        var existing = _members.SingleOrDefault(m => m.UserId == userId);
        if (existing is null)
        {
            _members.Add(new ProjectMember(Guard.NotDefault(userId, "user_id"), role, Guard.Utc(now, "added_at")));
            return;
        }

        if (existing.Role == ProjectRole.Owner && role != ProjectRole.Owner && _members.Count(m => m.Role == ProjectRole.Owner) == 1)
        {
            throw new DomainException("project.last_owner", "Project phải còn ít nhất một Owner.");
        }

        existing.ChangeRole(role);
    }

    public void RemoveMember(UserId userId)
    {
        var existing = _members.SingleOrDefault(m => m.UserId == userId)
            ?? throw new DomainException("project.member_not_found", "Người dùng không phải thành viên project.");
        if (existing.Role == ProjectRole.Owner && _members.Count(m => m.Role == ProjectRole.Owner) == 1)
        {
            throw new DomainException("project.last_owner", "Project phải còn ít nhất một Owner.");
        }

        _members.Remove(existing);
    }

    public void Rename(string name) => Name = Guard.NotEmpty(name, "name", 200);

    public ProjectRole? RoleOf(UserId userId) => _members.SingleOrDefault(m => m.UserId == userId)?.Role;

    public bool HasPermission(UserId userId, ProjectPermission permission) =>
        RoleOf(userId) is { } role && ProjectRolePermissions.Grants(role, permission);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}

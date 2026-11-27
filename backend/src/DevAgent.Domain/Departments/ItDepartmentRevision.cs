using DevAgent.Domain.Common;
using DevAgent.Domain.Departments.Layout;

namespace DevAgent.Domain.Departments;

/// <summary>
/// An immutable, complete snapshot written by each publication: name, theme, rooms and followed repositories.
/// Identified by (project, department, version); the office reads the version the department points to.
/// </summary>
public sealed class ItDepartmentRevision
{
    private readonly List<DepartmentRoom> _rooms = [];
    private readonly List<RevisionRepositoryLink> _repositories = [];

    private ItDepartmentRevision()
    {
        Name = string.Empty;
    }

    internal ItDepartmentRevision(
        ProjectId projectId,
        ItDepartmentId departmentId,
        int version,
        DepartmentConfiguration configuration,
        UserId publishedBy,
        DateTimeOffset publishedAt)
    {
        ProjectId = projectId;
        DepartmentId = departmentId;
        Version = version;
        Name = configuration.Name;
        Theme = configuration.Theme;
        PublishedBy = publishedBy;
        PublishedAt = publishedAt;
        _rooms.AddRange(configuration.Rooms);
        _repositories.AddRange(configuration.RepositoryIds.Select(id => new RevisionRepositoryLink(id)));
    }

    public ProjectId ProjectId { get; private set; }

    public ItDepartmentId DepartmentId { get; private set; }

    public int Version { get; private set; }

    public string Name { get; private set; }

    public DepartmentTheme Theme { get; private set; }

    public UserId PublishedBy { get; private set; }

    public DateTimeOffset PublishedAt { get; private set; }

    public IReadOnlyCollection<DepartmentRoom> Rooms => _rooms.AsReadOnly();

    public IReadOnlyCollection<RevisionRepositoryLink> Repositories => _repositories.AsReadOnly();

    public DepartmentConfiguration ToConfiguration() =>
        DepartmentConfiguration.Create(Name, Theme, _repositories.Select(r => r.RepositoryId), _rooms.Select(r => r.Copy()));
}

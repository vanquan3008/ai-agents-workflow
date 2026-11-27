using DevAgent.Domain.Common;
using DevAgent.Domain.Departments.Rules;

namespace DevAgent.Domain.Departments.Layout;

/// <summary>
/// Everything an owner can customize: name, theme, followed repositories and room layout.
/// Immutable; saved as the private draft and copied into each published revision.
/// </summary>
public sealed class DepartmentConfiguration
{
    private DepartmentConfiguration(string name, DepartmentTheme theme, IReadOnlyList<RepositoryId> repositoryIds, IReadOnlyList<DepartmentRoom> rooms)
    {
        Name = name;
        Theme = theme;
        RepositoryIds = repositoryIds;
        Rooms = rooms;
    }

    public string Name { get; }

    public DepartmentTheme Theme { get; }

    public IReadOnlyList<RepositoryId> RepositoryIds { get; }

    /// <summary>Rooms ordered by <see cref="DepartmentRoom.SortOrder"/>: the keyboard and list order in both views.</summary>
    public IReadOnlyList<DepartmentRoom> Rooms { get; }

    public static DepartmentConfiguration Create(
        string? name,
        DepartmentTheme theme,
        IEnumerable<RepositoryId> repositoryIds,
        IEnumerable<DepartmentRoom> rooms)
    {
        ArgumentNullException.ThrowIfNull(repositoryIds);
        ArgumentNullException.ThrowIfNull(rooms);
        return new DepartmentConfiguration(
            name?.Trim() ?? string.Empty,
            theme,
            repositoryIds.ToArray(),
            rooms.OrderBy(r => r.SortOrder).ToArray());
    }

    public IReadOnlyList<DepartmentRuleViolation> Validate(DepartmentValidationLevel level) =>
        DepartmentConfigurationRules.Validate(this, level);

    internal DepartmentConfiguration Copy() => new(Name, Theme, RepositoryIds.ToArray(), Rooms.Select(r => r.Copy()).ToArray());
}

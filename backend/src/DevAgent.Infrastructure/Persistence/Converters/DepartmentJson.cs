using System.Text.Json;
using System.Text.Json.Serialization;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments.Layout;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DevAgent.Infrastructure.Persistence.Converters;

/// <summary>
/// Stores a department draft as a jsonb object through a dedicated snapshot with string enums, so domain refactors
/// never break saved drafts. Drafts are validated by the domain before they reach this converter.
/// </summary>
internal static class DepartmentJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(DepartmentConfiguration configuration) =>
        JsonSerializer.Serialize(ConfigSnapshot.From(configuration), Options);

    public static DepartmentConfiguration Deserialize(string json) =>
        JsonSerializer.Deserialize<ConfigSnapshot>(json, Options)!.ToConfiguration();

    // EF never passes null to a converter, so the nullable annotations only satisfy the property type.
    public static ValueConverter<DepartmentConfiguration?, string> NullableConverter { get; } = new(c => Serialize(c!), s => Deserialize(s));

    // The configuration is immutable, so comparing serialized forms is exact and snapshots can share the instance.
    public static ValueComparer<DepartmentConfiguration?> NullableComparer { get; } = new(
        (a, b) => (a == null && b == null) || (a != null && b != null && Serialize(a) == Serialize(b)),
        c => c == null ? 0 : Serialize(c).GetHashCode(StringComparison.Ordinal),
        c => c);

    private sealed record ConfigSnapshot(string Name, DepartmentTheme Theme, Guid[] RepositoryIds, RoomSnapshot[] Rooms)
    {
        public static ConfigSnapshot From(DepartmentConfiguration c) => new(
            c.Name,
            c.Theme,
            c.RepositoryIds.Select(id => id.Value).ToArray(),
            c.Rooms.Select(RoomSnapshot.From).ToArray());

        public DepartmentConfiguration ToConfiguration() => DepartmentConfiguration.Create(
            Name,
            Theme,
            (RepositoryIds ?? []).Select(id => new RepositoryId(id)),
            (Rooms ?? []).Select(r => r.ToRoom()));
    }

    private sealed record RoomSnapshot(
        Guid Id,
        string Label,
        WorkflowStation? Station,
        int X,
        int Y,
        int Width,
        int Height,
        int SortOrder,
        string AssetPreset,
        RoomAction? Action)
    {
        public static RoomSnapshot From(DepartmentRoom r) =>
            new(r.Id.Value, r.Label, r.Station, r.X, r.Y, r.Width, r.Height, r.SortOrder, r.AssetPreset, r.Action);

        public DepartmentRoom ToRoom() =>
            DepartmentRoom.Create(new DepartmentRoomId(Id), Label, Station, X, Y, Width, Height, SortOrder, AssetPreset, Action);
    }
}

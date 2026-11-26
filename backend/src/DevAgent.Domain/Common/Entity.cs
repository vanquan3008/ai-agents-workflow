namespace DevAgent.Domain.Common;

/// <summary>An object with identity. Two entities are equal when they share the same runtime type and id.</summary>
public abstract class Entity<TId>
    where TId : struct, IEquatable<TId>
{
    protected Entity(TId id) => Id = id;

    // Required by EF Core materialization.
    protected Entity()
    {
    }

    public TId Id { get; protected init; }

    public override bool Equals(object? obj) =>
        obj is Entity<TId> other && other.GetType() == GetType() && other.Id.Equals(Id);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

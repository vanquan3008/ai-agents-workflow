namespace DevAgent.Domain.Common;

/// <summary>Thực thể có định danh. Hai thực thể bằng nhau khi cùng kiểu và cùng Id.</summary>
public abstract class Entity<TId>
    where TId : struct, IEquatable<TId>
{
    protected Entity(TId id) => Id = id;

    // Dành cho EF Core materialization.
    protected Entity()
    {
    }

    public TId Id { get; protected init; }

    public override bool Equals(object? obj) =>
        obj is Entity<TId> other && other.GetType() == GetType() && other.Id.Equals(Id);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

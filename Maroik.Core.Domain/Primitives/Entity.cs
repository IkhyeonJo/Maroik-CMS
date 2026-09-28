namespace Maroik.Core.Domain.Primitives;

/// <summary>
/// Base class for all domain entities.
/// Entities have a persistent identity — equality is determined by <typeparamref name="TId"/>, not by field values.
/// </summary>
/// <typeparam name="TId">Type of the entity's primary key.</typeparam>
public abstract class Entity<TId>(TId id) : IEquatable<Entity<TId>>
    where TId : notnull
{
    /// <summary>Primary key that uniquely identifies this entity.</summary>
    public TId Id { get; } = id;

    /// <inheritdoc/>
    public bool Equals(Entity<TId>? other) =>
        other is not null && GetType() == other.GetType() && Id.Equals(other.Id);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Entity<TId> entity && Equals(entity);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    /// <summary>Returns true when both entities are equal (same type and same ID).</summary>
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);

    /// <summary>Returns true when the entities are not equal.</summary>
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !Equals(left, right);
}

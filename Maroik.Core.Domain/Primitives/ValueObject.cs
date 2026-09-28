namespace Maroik.Core.Domain.Primitives;

/// <summary>
/// Base class for all value objects.
/// Value objects have no identity — equality is determined entirely by their atomic values.
/// Subclasses must implement <see cref="GetAtomicValues"/> to declare which fields compose the value.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>Returns the values that define equality for this value object.</summary>
    protected abstract IEnumerable<object?> GetAtomicValues();

    /// <inheritdoc/>
    public bool Equals(ValueObject? other)
    {
        if (other is null || other.GetType() != GetType())
            return false;

        return GetAtomicValues().SequenceEqual(other.GetAtomicValues());
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ValueObject vo && Equals(vo);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        GetAtomicValues().Aggregate(
            0,
            (hash, value) => HashCode.Combine(hash, value?.GetHashCode() ?? 0));

    /// <summary>Returns true when both value objects have the same atomic values.</summary>
    public static bool operator ==(ValueObject? left, ValueObject? right) => Equals(left, right);

    /// <summary>Returns true when the value objects have different atomic values.</summary>
    public static bool operator !=(ValueObject? left, ValueObject? right) => !Equals(left, right);
}

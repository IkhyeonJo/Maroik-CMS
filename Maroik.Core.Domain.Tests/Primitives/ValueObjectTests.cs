using Maroik.Core.Domain.Primitives;
namespace Maroik.Core.Domain.Tests.Primitives;

/// <summary>
/// Unit tests for <see cref="ValueObject"/>.
/// Covers atomic-value-based equality, type discrimination, null-atomic-value handling,
/// null/foreign-object comparisons, and hash-code consistency.
/// </summary>
public class ValueObjectTests
{
    /// <summary>Minimal value object made of a name and a number.</summary>
    private sealed class SamplePair(string? name, int number) : ValueObject
    {
        /// <inheritdoc />
        protected override IEnumerable<object?> GetAtomicValues()
        {
            yield return name;
            yield return number;
        }
    }

    /// <summary>Same components as <see cref="SamplePair"/> but a different type, to check type-sensitive equality.</summary>
    private sealed class OtherPair(string? name, int number) : ValueObject
    {
        /// <inheritdoc />
        protected override IEnumerable<object?> GetAtomicValues()
        {
            yield return name;
            yield return number;
        }
    }

    /// <summary>Equals same atomic values are equal.</summary>
    [Fact]
    public void Equals_SameAtomicValues_AreEqual()
    {
        var a = new SamplePair("x", 1);
        var b = new SamplePair("x", 1);

        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    /// <summary>Equals different atomic values are not equal.</summary>
    [Fact]
    public void Equals_DifferentAtomicValues_AreNotEqual()
    {
        var a = new SamplePair("x", 1);
        var b = new SamplePair("x", 2);

        Assert.False(a.Equals(b));
        Assert.False(a == b);
        Assert.True(a != b);
    }

    /// <summary>Equals different type and same atomic values are not equal, because equality is scoped to the concrete value-object type.</summary>
    [Fact]
    public void Equals_DifferentTypeAndSameAtomicValues_AreNotEqual()
    {
        var a = new SamplePair("x", 1);
        var b = new OtherPair("x", 1);

        Assert.False(a.Equals(b));
    }

    /// <summary>Equals null atomic value is handled without throwing and participates in equality.</summary>
    [Fact]
    public void Equals_NullAtomicValue_IsHandled()
    {
        var a = new SamplePair(null, 1);
        var b = new SamplePair(null, 1);
        var c = new SamplePair("x", 1);

        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.False(a.Equals(c));
    }

    /// <summary>Equals null other returns false.</summary>
    [Fact]
    public void Equals_NullOther_ReturnsFalse()
    {
        var a = new SamplePair("x", 1);

        Assert.False(a.Equals(null));
        Assert.False(a.Equals((object?)null));
    }

    /// <summary>Equals non value object returns false.</summary>
    [Fact]
    public void Equals_NonValueObject_ReturnsFalse()
    {
        var a = new SamplePair("x", 1);

        // ReSharper disable once SuspiciousTypeConversion.Global -- deliberately comparing to an unrelated type
        Assert.False(a.Equals("not a value object"));
    }

    /// <summary>Operator equality both null returns true.</summary>
    [Fact]
    public void OperatorEquality_BothNull_ReturnsTrue()
    {
        SamplePair? a = null;
        SamplePair? b = null;

        Assert.True(a == b);
        Assert.False(a != b);
    }
}

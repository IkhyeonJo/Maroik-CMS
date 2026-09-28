using Maroik.Core.Domain.Primitives;
namespace Maroik.Core.Domain.Tests.Primitives;

/// <summary>
/// Unit tests for <see cref="Entity{TId}"/>.
/// Covers identity-based equality (same type + same ID), type discrimination
/// (same ID but different runtime type), null handling, and hash-code consistency.
/// </summary>
public class EntityTests
{
    private sealed class SampleEntity(int id) : Entity<int>(id);

    private sealed class OtherEntity(int id) : Entity<int>(id);

    /// <summary>Equals same type and same id are equal.</summary>
    [Fact]
    public void Equals_SameTypeAndSameId_AreEqual()
    {
        var a = new SampleEntity(1);
        var b = new SampleEntity(1);

        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    /// <summary>Equals same type and different id are not equal.</summary>
    [Fact]
    public void Equals_SameTypeAndDifferentId_AreNotEqual()
    {
        var a = new SampleEntity(1);
        var b = new SampleEntity(2);

        Assert.False(a.Equals(b));
        Assert.False(a == b);
        Assert.True(a != b);
    }

    /// <summary>Equals different type and same id are not equal, because identity is scoped to the concrete entity type.</summary>
    [Fact]
    public void Equals_DifferentTypeAndSameId_AreNotEqual()
    {
        var a = new SampleEntity(1);
        var b = new OtherEntity(1);

        Assert.False(a.Equals(b));
    }

    /// <summary>Equals null other returns false.</summary>
    [Fact]
    public void Equals_NullOther_ReturnsFalse()
    {
        var a = new SampleEntity(1);

        Assert.False(a.Equals(null));
        Assert.False(a.Equals((object?)null));
    }

    /// <summary>Equals non entity object returns false.</summary>
    [Fact]
    public void Equals_NonEntityObject_ReturnsFalse()
    {
        var a = new SampleEntity(1);

        // ReSharper disable once SuspiciousTypeConversion.Global -- deliberately comparing to an unrelated type
        Assert.False(a.Equals("not an entity"));
    }

    /// <summary>Operator equality both null returns true.</summary>
    [Fact]
    public void OperatorEquality_BothNull_ReturnsTrue()
    {
        SampleEntity? a = null;
        SampleEntity? b = null;

        Assert.True(a == b);
        Assert.False(a != b);
    }

    /// <summary>Operator equality one null returns false.</summary>
    [Fact]
    public void OperatorEquality_OneNull_ReturnsFalse()
    {
        var a = new SampleEntity(1);
        SampleEntity? b = null;

        Assert.False(a == b);
        Assert.False(b == a);
        Assert.True(a != b);
    }
}

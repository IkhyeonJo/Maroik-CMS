using Maroik.Core.Domain.Primitives;
namespace Maroik.Core.Domain.Tests.Primitives;

/// <summary>
/// Unit tests for <see cref="StringTaxonomy"/> -- the shared "closed set of known string values"
/// primitive now backing every DB-CHECK-mirrored taxonomy (<c>BoardTypes</c>,
/// <c>CalendarEventStatuses</c>, <c>ReminderMethods</c>, <c>AssetItems</c>).
/// </summary>
public class StringTaxonomyTests
{
    /// <summary>All returns every value the taxonomy was built with.</summary>
    [Fact]
    public void All_ReturnsEveryConstructedValue()
    {
        var taxonomy = new StringTaxonomy(["A", "B", "C"]);

        Assert.Equal(new HashSet<string> { "A", "B", "C" }, taxonomy.All);
    }

    /// <summary>IsKnown returns true for a value the taxonomy was built with.</summary>
    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    public void IsKnown_KnownValue_ReturnsTrue(string value)
    {
        var taxonomy = new StringTaxonomy(["A", "B"]);

        Assert.True(taxonomy.IsKnown(value));
    }

    /// <summary>IsKnown returns false for a value not in the taxonomy.</summary>
    [Fact]
    public void IsKnown_UnknownValue_ReturnsFalse()
    {
        var taxonomy = new StringTaxonomy(["A", "B"]);

        Assert.False(taxonomy.IsKnown("C"));
    }

    /// <summary>IsKnown returns false for null, without throwing.</summary>
    [Fact]
    public void IsKnown_Null_ReturnsFalse()
    {
        var taxonomy = new StringTaxonomy(["A", "B"]);

        Assert.False(taxonomy.IsKnown(null));
    }

    /// <summary>Membership is compared ordinally -- a differently-cased value is not known.</summary>
    [Fact]
    public void IsKnown_DifferentCase_ReturnsFalse()
    {
        var taxonomy = new StringTaxonomy(["Busy"]);

        Assert.False(taxonomy.IsKnown("busy"));
    }
}

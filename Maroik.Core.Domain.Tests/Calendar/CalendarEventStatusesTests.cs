using Maroik.Core.Domain.Calendar;
namespace Maroik.Core.Domain.Tests.Calendar;

/// <summary>
/// Unit tests for <see cref="CalendarEventStatuses"/>, pinning its <see cref="Maroik.Core.Domain.Primitives.StringTaxonomy"/>-backed
/// <c>All</c>/<c>IsKnown</c> surface after that refactor.
/// </summary>
public class CalendarEventStatusesTests
{
    /// <summary>All contains exactly the two known statuses.</summary>
    [Fact]
    public void All_ContainsExactlyKnownStatuses()
    {
        Assert.Equal(new HashSet<string> { CalendarEventStatuses.Busy, CalendarEventStatuses.Free }, CalendarEventStatuses.All);
    }

    /// <summary>IsKnown returns true for each known status.</summary>
    [Theory]
    [InlineData(CalendarEventStatuses.Busy)]
    [InlineData(CalendarEventStatuses.Free)]
    public void IsKnown_KnownStatus_ReturnsTrue(string status) => Assert.True(CalendarEventStatuses.IsKnown(status));

    /// <summary>IsKnown returns false for an unrecognized status and for null.</summary>
    [Theory]
    [InlineData("Tentative")]
    [InlineData(null)]
    public void IsKnown_UnknownOrNull_ReturnsFalse(string? status) => Assert.False(CalendarEventStatuses.IsKnown(status));
}

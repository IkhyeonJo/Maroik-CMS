using Maroik.Core.Domain.Calendar;
namespace Maroik.Core.Domain.Tests.Calendar;

/// <summary>
/// Unit tests for <see cref="ReminderMethods"/>, pinning its <see cref="Maroik.Core.Domain.Primitives.StringTaxonomy"/>-backed
/// <c>All</c>/<c>IsKnown</c> surface after that refactor.
/// </summary>
public class ReminderMethodsTests
{
    /// <summary>All contains exactly the two known delivery methods.</summary>
    [Fact]
    public void All_ContainsExactlyKnownMethods()
    {
        Assert.Equal(new HashSet<string> { ReminderMethods.Email, ReminderMethods.Notification }, ReminderMethods.All);
    }

    /// <summary>IsKnown returns true for each known delivery method.</summary>
    [Theory]
    [InlineData(ReminderMethods.Email)]
    [InlineData(ReminderMethods.Notification)]
    public void IsKnown_KnownMethod_ReturnsTrue(string method) => Assert.True(ReminderMethods.IsKnown(method));

    /// <summary>IsKnown returns false for an unrecognised method and for null.</summary>
    [Theory]
    [InlineData("Sms")]
    [InlineData(null)]
    public void IsKnown_UnknownOrNull_ReturnsFalse(string? method) => Assert.False(ReminderMethods.IsKnown(method));
}

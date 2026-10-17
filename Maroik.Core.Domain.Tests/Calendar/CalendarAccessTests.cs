using Maroik.Core.Domain.Calendar;
namespace Maroik.Core.Domain.Tests.Calendar;

/// <summary>
/// Unit tests for the calendar access and provisioning rules: <see cref="CalendarAccessPolicy"/>,
/// <see cref="Domain.Calendar.Calendar.IsOwnedBy"/>, <see cref="Domain.Calendar.Calendar.IsNameTaken"/>,
/// <see cref="Domain.Calendar.Calendar.NeedsDefault"/> and <see cref="Domain.Calendar.Calendar.CreateDefault"/>.
/// </summary>
public class CalendarAccessTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A stored calendar of "user@example.com".</summary>
    private static Domain.Calendar.Calendar CalendarOf(long id, string name = "Work") =>
        Domain.Calendar.Calendar.Reconstitute(id, "user@example.com", name, null, "UTC", "#112233", Now, Now);

    /// <summary>A stored event in calendar <paramref name="calendarId"/>.</summary>
    private static CalendarEvent EventIn(long calendarId) =>
        CalendarEvent.Reconstitute(7, calendarId, "Meeting", null, false, Now, Now.AddHours(1), "UTC", "UTC", null, null, null, Now, Now);

    /// <summary>The account's calendars 1 and 2.</summary>
    private static readonly Domain.Calendar.Calendar[] Owned = [CalendarOf(1, "Work"), CalendarOf(2, "Home")];

    // -- Ownership --------------------------------------------------------------

    /// <summary>A calendar is owned when it is among the account's calendars.</summary>
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void Owns_IsTrue_OnlyForTheAccountsCalendars(long calendarId, bool owned)
        => Assert.Equal(owned, CalendarAccessPolicy.Owns(Owned, calendarId));

    /// <summary>Every target must be owned; one foreign calendar is enough to refuse.</summary>
    [Fact]
    public void OwnsAll_RefusesWhenAnyCalendarIsForeign()
    {
        Assert.True(CalendarAccessPolicy.OwnsAll(Owned, [1, 2]));
        Assert.True(CalendarAccessPolicy.OwnsAll(Owned, []));
        Assert.False(CalendarAccessPolicy.OwnsAll(Owned, [1, 3]));
    }

    /// <summary>An event is owned when it exists and sits in one of the account's calendars.</summary>
    [Fact]
    public void OwnsEvent_RequiresAnExistingEventInAnOwnedCalendar()
    {
        Assert.True(CalendarAccessPolicy.OwnsEvent(Owned, EventIn(2)));
        Assert.False(CalendarAccessPolicy.OwnsEvent(Owned, EventIn(3)));
        Assert.False(CalendarAccessPolicy.OwnsEvent(Owned, null));
    }

    /// <summary>A calendar is owned by the account whose email it carries.</summary>
    [Fact]
    public void IsOwnedBy_MatchesTheAccountEmail()
    {
        Assert.True(CalendarOf(1).IsOwnedBy("user@example.com"));
        Assert.False(CalendarOf(1).IsOwnedBy("other@example.com"));
    }

    // -- Names ------------------------------------------------------------------

    /// <summary>A name is taken when one of the account's calendars already has it, compared exactly.</summary>
    [Theory]
    [InlineData("Work", true)]
    [InlineData("work", false)]
    [InlineData("Travel", false)]
    [InlineData(null, false)]
    public void IsNameTaken_ComparesTheNamesExactly(string? name, bool taken)
        => Assert.Equal(taken, Domain.Calendar.Calendar.IsNameTaken(Owned, name));

    // -- Default calendar -------------------------------------------------------

    /// <summary>The default calendar is only needed by an account that has none.</summary>
    [Fact]
    public void NeedsDefault_OnlyWhenTheAccountHasNoCalendar()
    {
        Assert.True(Domain.Calendar.Calendar.NeedsDefault([]));
        Assert.False(Domain.Calendar.Calendar.NeedsDefault(Owned));
    }

    /// <summary>An unusable time zone falls back to UTC; a usable one is kept.</summary>
    [Theory]
    [InlineData(null, "UTC")]
    [InlineData("Not/AZone", "UTC")]
    [InlineData("Asia/Seoul", "Asia/Seoul")]
    public void CreateDefault_FallsBackToUtc_ForAnUnusableTimeZone(string? timeZone, string expected)
    {
        var result = Domain.Calendar.Calendar.CreateDefault("user@example.com", "My calendar", null, timeZone, "#112233", Now);

        Assert.Equal(expected, result.Value.TimeZone.Value);
    }

    /// <summary>Every other field is still validated as for a normal calendar.</summary>
    [Fact]
    public void CreateDefault_StillRejectsAnInvalidName()
        => Assert.True(Domain.Calendar.Calendar.CreateDefault("user@example.com", " ", null, "UTC", "#112233", Now).IsError);
}

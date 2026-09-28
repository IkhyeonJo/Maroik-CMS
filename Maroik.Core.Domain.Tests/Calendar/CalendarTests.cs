namespace Maroik.Core.Domain.Tests.Calendar;

/// <summary>
/// Unit tests for <see cref="Maroik.Core.Domain.Calendar.Calendar"/>.
/// Covers calendar creation and update.
/// </summary>
public class CalendarTests
{
    private static Domain.Calendar.Calendar ValidCalendar(long id = 1) =>
        Domain.Calendar.Calendar.Reconstitute(id, "user@example.com", "Work", "My work calendar", "UTC", "#3498DB", DateTime.UtcNow, DateTime.UtcNow);

    // -- Create ---------------------------------------------------------------

    /// <summary>Create returns calendar, when valid.</summary>
    [Fact]
    public void Create_ReturnsCalendar_WhenValid()
    {
        var result = Domain.Calendar.Calendar.Create("user@example.com", "Personal", null, "UTC", "#FF0000");

        Assert.False(result.IsError);
        Assert.Equal("Personal", result.Value.Name);
        Assert.Equal("user@example.com", result.Value.AccountEmail.Value);
    }

    /// <summary>Create returns error, when name empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ReturnsError_WhenNameEmpty(string? name)
    {
        var result = Domain.Calendar.Calendar.Create("user@example.com", name, null, "UTC", "#FF0000");

        Assert.True(result.IsError);
        Assert.Equal("Calendar.NameEmpty", result.FirstError.Code);
    }

    /// <summary>Create returns error, when email invalid.</summary>
    [Fact]
    public void Create_ReturnsError_WhenEmailInvalid()
    {
        var result = Domain.Calendar.Calendar.Create("bademail", "Work", null, "UTC", "#FF0000");

        Assert.True(result.IsError);
    }

    /// <summary>Create returns error, when time zone invalid.</summary>
    [Fact]
    public void Create_ReturnsError_WhenTimeZoneInvalid()
    {
        var result = Domain.Calendar.Calendar.Create("user@example.com", "Work", null, "Not/Valid", "#FF0000");

        Assert.True(result.IsError);
        Assert.Equal("TimeZoneId.Invalid", result.FirstError.Code);
    }

    /// <summary>Create returns error, when color code invalid.</summary>
    [Fact]
    public void Create_ReturnsError_WhenColorCodeInvalid()
    {
        var result = Domain.Calendar.Calendar.Create("user@example.com", "Work", null, "UTC", "red");

        Assert.True(result.IsError);
        Assert.Equal("HtmlColorCode.Invalid", result.FirstError.Code);
    }

    /// <summary>Create returns error, when name contains markup or control characters.</summary>
    [Theory]
    [InlineData("<img src=x>")]
    [InlineData("Team </label><a href='https://evil'>")]
    [InlineData("Work\tCalendar")]
    [InlineData("Line\nBreak")]
    public void Create_ReturnsError_WhenNameContainsMarkupOrControlChars(string name)
    {
        var result = Domain.Calendar.Calendar.Create("user@example.com", name, null, "UTC", "#FF0000");

        Assert.True(result.IsError);
        Assert.Equal("Calendar.NameInvalid", result.FirstError.Code);
    }

    /// <summary>Create returns error, when name exceeds the 255-char column width.</summary>
    [Fact]
    public void Create_ReturnsError_WhenNameTooLong()
    {
        var result = Domain.Calendar.Calendar.Create("user@example.com", new string('a', 256), null, "UTC", "#FF0000");

        Assert.True(result.IsError);
        Assert.Equal("Calendar.NameTooLong", result.FirstError.Code);
    }

    /// <summary>Create allows punctuation that is safe in a plain label (ampersand, apostrophe, quotes).</summary>
    [Theory]
    [InlineData("Mom & Dad")]
    [InlineData("Bob's calendar")]
    [InlineData("Q3 \"planning\"")]
    public void Create_AllowsSafePunctuation(string name)
    {
        var result = Domain.Calendar.Calendar.Create("user@example.com", name, null, "UTC", "#FF0000");

        Assert.False(result.IsError);
        Assert.Equal(name, result.Value.Name);
    }

    // -- Update ---------------------------------------------------------------

    /// <summary>Update succeeds, when valid.</summary>
    [Fact]
    public void Update_Succeeds_WhenValid()
    {
        var calendar = ValidCalendar();

        var result = calendar.Update("Personal", "Updated desc", "Asia/Seoul", "#E74C3C");

        Assert.False(result.IsError);
        Assert.Equal("Personal", calendar.Name);
        Assert.Equal("Asia/Seoul", calendar.TimeZone.Value);
        Assert.Equal("#E74C3C", calendar.ColorCode.Value);
    }

    /// <summary>Update returns error, when name empty.</summary>
    [Fact]
    public void Update_ReturnsError_WhenNameEmpty()
    {
        var calendar = ValidCalendar();

        var result = calendar.Update("", null, "UTC", "#FF0000");

        Assert.True(result.IsError);
        Assert.Equal("Calendar.NameEmpty", result.FirstError.Code);
    }

    /// <summary>Update returns error, when name contains markup.</summary>
    [Fact]
    public void Update_ReturnsError_WhenNameContainsMarkup()
    {
        var calendar = ValidCalendar();

        var result = calendar.Update("<script>alert(1)</script>", null, "UTC", "#FF0000");

        Assert.True(result.IsError);
        Assert.Equal("Calendar.NameInvalid", result.FirstError.Code);
    }

}

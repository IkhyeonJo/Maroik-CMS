using Maroik.Core.Domain.Kanbans;
using Maroik.Core.Domain.Errors;
using Maroik.Core.Domain.Primitives;
namespace Maroik.Core.Domain.Tests.Kanbans;

/// <summary>
/// Unit tests for <see cref="KanbanCard"/>: the content rules of a card (K8), the lane it is created in and never leaves (K11),
/// and the due-date state the board shows, decided in the owner's time zone (K10).
/// </summary>
public class KanbanCardTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime Now = new(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A valid card in the To do lane, created at <see cref="Now"/>.</summary>
    private static KanbanCard ValidCard(DateOnly? dueDate = null) =>
        KanbanCard.Create("user@example.com", "ToDo", "Write the report", "First draft", dueDate, Now).Value;

    // -- Create ---------------------------------------------------------------

    /// <summary>A valid card keeps every field it was given and is stamped with the given time.</summary>
    [Fact]
    public void Create_ReturnsTheCard_WhenValid()
    {
        var result = KanbanCard.Create("User@Example.com", "InProgress", "Write the report", "Line 1\nLine 2", new DateOnly(2026, 5, 28), Now);

        Assert.False(result.IsError);
        KanbanCard card = result.Value;
        Assert.Equal(0, card.Id);
        Assert.Equal("user@example.com", card.AccountEmail.Value);
        Assert.Equal("InProgress", card.Lane);
        Assert.Equal("Write the report", card.Title);
        Assert.Equal("Line 1\nLine 2", card.Description);
        Assert.Equal(new DateOnly(2026, 5, 28), card.DueDate);
        Assert.Equal(Now, card.Created);
        Assert.Equal(Now, card.Updated);
    }

    /// <summary>The description and the due date are optional; an empty or blank description is stored as none.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_StoresNoDescription_WhenItIsBlank(string? description)
    {
        var result = KanbanCard.Create("user@example.com", "Backlog", "Title", description, null, Now);

        Assert.False(result.IsError);
        Assert.Null(result.Value.Description);
        Assert.Null(result.Value.DueDate);
    }

    /// <summary>A lane outside the four fixed lanes is refused, naming the value.</summary>
    [Theory]
    [InlineData("Archive")]
    [InlineData("")]
    [InlineData(null)]
    public void Create_RefusesAnUnknownLane(string? lane)
    {
        var result = KanbanCard.Create("user@example.com", lane, "Title", null, null, Now);

        Assert.True(result.IsError);
        Assert.Equal("KanbanCard.LaneInvalid", result.FirstError.Code);
        Assert.Equal("'{0}' is not a Kanban lane.", result.FirstError.Metadata![DomainError.MessageTemplateMetadataKey]);
    }

    /// <summary>A missing or blank title is refused.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t ")]
    public void Create_RefusesABlankTitle(string? title)
    {
        var result = KanbanCard.Create("user@example.com", "Backlog", title, null, null, Now);

        Assert.True(result.IsError);
        Assert.Equal("KanbanCard.TitleEmpty", result.FirstError.Code);
    }

    /// <summary>A title of exactly the post-title limit is accepted; one character more is refused with the limit as its value.</summary>
    [Fact]
    public void Create_AppliesThePostTitleLimit()
    {
        var atLimit = KanbanCard.Create("user@example.com", "Backlog", new string('a', TitledContentPolicy.MaxTitleLength), null, null, Now);
        var overLimit = KanbanCard.Create("user@example.com", "Backlog", new string('a', TitledContentPolicy.MaxTitleLength + 1), null, null, Now);

        Assert.False(atLimit.IsError);
        Assert.Equal("KanbanCard.TitleTooLong", overLimit.FirstError.Code);
        Assert.Equal([TitledContentPolicy.MaxTitleLength], (object[])overLimit.FirstError.Metadata![DomainError.MessageArgsMetadataKey]);
    }

    /// <summary>A description of exactly the body limit is accepted; one character more is refused with the limit as its value.</summary>
    [Fact]
    public void Create_AppliesTheBodyLimit_ToTheDescription()
    {
        var atLimit = KanbanCard.Create("user@example.com", "Backlog", "Title", new string('a', TitledContentPolicy.MaxBodyLength), null, Now);
        var overLimit = KanbanCard.Create("user@example.com", "Backlog", "Title", new string('a', TitledContentPolicy.MaxBodyLength + 1), null, Now);

        Assert.False(atLimit.IsError);
        Assert.Equal("KanbanCard.DescriptionTooLong", overLimit.FirstError.Code);
        Assert.Equal([TitledContentPolicy.MaxBodyLength], (object[])overLimit.FirstError.Metadata![DomainError.MessageArgsMetadataKey]);
    }

    /// <summary>An invalid owner email is refused.</summary>
    [Fact]
    public void Create_RefusesAnInvalidEmail()
    {
        var result = KanbanCard.Create("not-an-email", "Backlog", "Title", null, null, Now);

        Assert.True(result.IsError);
        Assert.Equal("Email.Invalid", result.FirstError.Code);
    }

    // -- Update ---------------------------------------------------------------

    /// <summary>Update replaces the title, description and due date, stamps Updated, and keeps the lane and Created.</summary>
    [Fact]
    public void Update_ReplacesTheContent_AndKeepsTheLane()
    {
        KanbanCard card = ValidCard(new DateOnly(2026, 5, 28));
        DateTime later = Now.AddHours(3);

        var result = card.Update("Send the report", null, null, later);

        Assert.False(result.IsError);
        Assert.Equal("Send the report", card.Title);
        Assert.Null(card.Description);
        Assert.Null(card.DueDate);
        Assert.Equal("ToDo", card.Lane);
        Assert.Equal(Now, card.Created);
        Assert.Equal(later, card.Updated);
    }

    /// <summary>A refused update leaves the card exactly as it was.</summary>
    [Theory]
    [InlineData(" ", "KanbanCard.TitleEmpty")]
    [InlineData(null, "KanbanCard.TitleEmpty")]
    public void Update_RefusesABlankTitle_AndChangesNothing(string? title, string code)
    {
        KanbanCard card = ValidCard(new DateOnly(2026, 5, 28));

        var result = card.Update(title, "changed", new DateOnly(2027, 1, 1), Now.AddHours(1));

        Assert.Equal(code, result.FirstError.Code);
        Assert.Equal("Write the report", card.Title);
        Assert.Equal("First draft", card.Description);
        Assert.Equal(new DateOnly(2026, 5, 28), card.DueDate);
        Assert.Equal(Now, card.Updated);
    }

    /// <summary>Update applies the same title and description limits as Create.</summary>
    [Fact]
    public void Update_AppliesTheSameLimits()
    {
        KanbanCard card = ValidCard();

        Assert.Equal("KanbanCard.TitleTooLong", card.Update(new string('a', TitledContentPolicy.MaxTitleLength + 1), null, null, Now).FirstError.Code);
        Assert.Equal("KanbanCard.DescriptionTooLong", card.Update("Title", new string('a', TitledContentPolicy.MaxBodyLength + 1), null, Now).FirstError.Code);
    }

    /// <summary>
    /// A card's lane is fixed for its whole life (K11): nothing on the card can change it — the property has no public setter and
    /// no public method takes a lane, so moving work to another lane means deleting the card and creating a new one there.
    /// </summary>
    [Fact]
    public void TheLane_CannotBeChangedAfterCreation()
    {
        var lane = typeof(KanbanCard).GetProperty(nameof(KanbanCard.Lane))!;
        Assert.False(lane.SetMethod?.IsPublic ?? false);

        var methodsTakingALane = typeof(KanbanCard).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => string.Equals(p.Name, "lane", StringComparison.OrdinalIgnoreCase)))
            .Select(m => m.Name);
        Assert.Empty(methodsTakingALane);
    }

    // -- Reconstitute ----------------------------------------------------------

    /// <summary>Reconstitute assigns every stored field verbatim.</summary>
    [Fact]
    public void Reconstitute_AssignsEveryField()
    {
        DateTime created = new(2026, 1, 2, 3, 4, 5);
        DateTime updated = new(2026, 2, 3, 4, 5, 6);

        KanbanCard card = KanbanCard.Reconstitute(42, "user@example.com", "Done", "Title", "Body", new DateOnly(2026, 3, 4), created, updated);

        Assert.Equal(42, card.Id);
        Assert.Equal("user@example.com", card.AccountEmail.Value);
        Assert.Equal("Done", card.Lane);
        Assert.Equal("Title", card.Title);
        Assert.Equal("Body", card.Description);
        Assert.Equal(new DateOnly(2026, 3, 4), card.DueDate);
        Assert.Equal(created, card.Created);
        Assert.Equal(updated, card.Updated);
    }

    // -- Due-date state ----------------------------------------------------------

    /// <summary>A card without a due date shows nothing.</summary>
    [Fact]
    public void DueStateAt_IsNone_WithoutADueDate()
    {
        Assert.Equal(KanbanDueState.None, ValidCard().DueStateAt(Now, "UTC"));
    }

    /// <summary>A card due later, or today, is not overdue; one due yesterday is.</summary>
    [Theory]
    [InlineData(2026, 5, 25, KanbanDueState.Upcoming)]
    [InlineData(2026, 5, 24, KanbanDueState.Upcoming)]
    [InlineData(2026, 5, 23, KanbanDueState.Overdue)]
    public void DueStateAt_ComparesTheDueDate_WithToday(int year, int month, int day, KanbanDueState expected)
    {
        Assert.Equal(expected, ValidCard(new DateOnly(year, month, day)).DueStateAt(Now, "UTC"));
    }

    /// <summary>
    /// "Today" is the owner's local date, not the server's UTC date: at 2026-05-24 20:00 UTC it is already 2026-05-25 in Seoul
    /// (UTC+9), so a card due on the 24th is overdue there, while in New York (UTC-4) it is still the 24th and the card is not.
    /// </summary>
    [Fact]
    public void DueStateAt_DecidesToday_InTheOwnersTimeZone()
    {
        DateTime evening = new(2026, 5, 24, 20, 0, 0, DateTimeKind.Utc);
        KanbanCard card = ValidCard(new DateOnly(2026, 5, 24));

        Assert.Equal(KanbanDueState.Overdue, card.DueStateAt(evening, "Asia/Seoul"));
        Assert.Equal(KanbanDueState.Upcoming, card.DueStateAt(evening, "America/New_York"));
    }
}

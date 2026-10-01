using Maroik.Core.Domain.Board;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.Domain.Primitives;
using Maroik.Core.Domain.ValueObjects;
using DomainCalendar = Maroik.Core.Domain.Calendar.Calendar;
namespace Maroik.Core.Domain.Tests.Boundaries;

/// <summary>
/// Exact-limit behavior of every length / size rule in the domain: a value AT the limit is accepted, one
/// character more is refused with the documented error. (A "> max" written as ">= max" would reject the
/// longest legal value — off-by-one errors that "the too-long value is rejected" tests never notice.)
/// </summary>
public class LengthBoundaryTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A string of exactly <paramref name="length"/> characters.</summary>
    private static string Text(int length) => new('x', length);

    /// <summary>Writer nickname used for boards and comments.</summary>
    private const string Writer = "Alice";

    // ---- Board ----------------------------------------------------------------------------

    /// <summary>Verifies that <c>Board.Create</c> accepts a title and content at the maximum length and refuses one character more.</summary>
    [Fact]
    public void Board_Create_AcceptsATitleAndContentAtTheLimit_AndRefusesOneMore()
    {
        Assert.False(Domain.Board.Board.Create(BoardTypes.FreeForum, Text(TitledContentPolicy.MaxTitleLength), Text(TitledContentPolicy.MaxBodyLength), Writer).IsError);

        ErrorAssert.Validation(Domain.Board.Board.Create(BoardTypes.FreeForum, Text(TitledContentPolicy.MaxTitleLength + 1), "c", Writer),
            "Board.TitleTooLong", "Post title must be {0} characters or fewer.", TitledContentPolicy.MaxTitleLength);
        ErrorAssert.Validation(Domain.Board.Board.Create(BoardTypes.FreeForum, "t", Text(TitledContentPolicy.MaxBodyLength + 1), Writer),
            "Board.ContentTooLong", "Post content must be {0} characters or fewer.", TitledContentPolicy.MaxBodyLength);
    }

    /// <summary>Verifies that <c>Board.Update</c> accepts a title and content at the maximum length and refuses one character more.</summary>
    [Fact]
    public void Board_Update_AcceptsATitleAndContentAtTheLimit_AndRefusesOneMore()
    {
        Domain.Board.Board board = Domain.Board.Board.Create(BoardTypes.FreeForum, "t", "c", Writer).Value;

        Assert.False(board.Update(Text(TitledContentPolicy.MaxTitleLength), Text(TitledContentPolicy.MaxBodyLength)).IsError);

        ErrorAssert.Validation(board.Update(Text(TitledContentPolicy.MaxTitleLength + 1), "c"),
            "Board.TitleTooLong", "Post title must be {0} characters or fewer.", TitledContentPolicy.MaxTitleLength);
        ErrorAssert.Validation(board.Update("t", Text(TitledContentPolicy.MaxBodyLength + 1)),
            "Board.ContentTooLong", "Post content must be {0} characters or fewer.", TitledContentPolicy.MaxBodyLength);
    }

    /// <summary>Verifies the empty/unknown board type, empty title and empty writer errors.</summary>
    [Fact]
    public void Board_ValidationMessages_ForEmptyAndUnknownInput()
    {
        ErrorAssert.Validation(Domain.Board.Board.Create("", "t", "c", Writer), "Board.TypeEmpty", "Board type cannot be empty.");
        ErrorAssert.Validation(Domain.Board.Board.Create("Nonsense", "t", "c", Writer), "Board.TypeInvalid", "Board type is not a recognised value.");
        ErrorAssert.Validation(Domain.Board.Board.Create(BoardTypes.FreeForum, " ", "c", Writer), "Board.TitleEmpty", "Post title cannot be empty.");
        ErrorAssert.Validation(Domain.Board.Board.Create(BoardTypes.FreeForum, "t", "c", " "), "Board.WriterEmpty", "Post author (writer) cannot be empty.");
        ErrorAssert.Validation(Domain.Board.Board.Create(BoardTypes.FreeForum, "t", "c", Writer).Value.Update(" ", "c"),
            "Board.TitleEmpty", "Post title cannot be empty.");
    }

    // ---- Calendar / CalendarEvent -----------------------------------------------------------

    /// <summary>Verifies that a calendar name at the limit is accepted, and that too-long, empty and angle-bracket names are refused.</summary>
    [Fact]
    public void Calendar_Create_AcceptsANameAtTheLimit_AndRefusesOneMore()
    {
        Assert.False(DomainCalendar.Create("a@b.com", Text(ShortTextPolicy.MaxLength), null, "UTC", "#112233", Now).IsError);

        ErrorAssert.Validation(DomainCalendar.Create("a@b.com", Text(ShortTextPolicy.MaxLength + 1), null, "UTC", "#112233", Now),
            "Calendar.NameTooLong", "Calendar name must be {0} characters or fewer.", ShortTextPolicy.MaxLength);
        ErrorAssert.Validation(DomainCalendar.Create("a@b.com", " ", null, "UTC", "#112233", Now),
            "Calendar.NameEmpty", "Calendar name cannot be empty.");
        ErrorAssert.Validation(DomainCalendar.Create("a@b.com", "a<b", null, "UTC", "#112233", Now),
            "Calendar.NameInvalid", "Calendar name cannot contain angle brackets or control characters.");
    }

    /// <summary>Start time of the calendar events built by <see cref="Event"/>.</summary>
    private static readonly DateTime _start = new(2026, 1, 1, 9, 0, 0);

    /// <summary>Result of <c>CalendarEvent.Create</c> for a one-hour UTC event with the given text fields and status.</summary>
    private static ErrorOr.ErrorOr<CalendarEvent> Event(string title = "t", string? description = null, string? location = null, string? status = null) =>
        CalendarEvent.Create(1, title, description, false, _start, _start.AddHours(1), "UTC", "UTC", location, status, Now);

    /// <summary>Verifies the title / description / location length limits, the empty title and the unknown status on <c>CalendarEvent.Create</c>.</summary>
    [Fact]
    public void CalendarEvent_Create_AcceptsTextAtTheLimits_AndRefusesOneMore()
    {
        Assert.False(Event(Text(TitledContentPolicy.MaxTitleLength), Text(TitledContentPolicy.MaxBodyLength), Text(ShortTextPolicy.MaxLength)).IsError);

        ErrorAssert.Validation(Event(Text(TitledContentPolicy.MaxTitleLength + 1)), "CalendarEvent.TitleTooLong",
            "Event title must be {0} characters or fewer.", TitledContentPolicy.MaxTitleLength);
        ErrorAssert.Validation(Event(description: Text(TitledContentPolicy.MaxBodyLength + 1)), "CalendarEvent.DescriptionTooLong",
            "Event description must be {0} characters or fewer.", TitledContentPolicy.MaxBodyLength);
        ErrorAssert.Validation(Event(location: Text(ShortTextPolicy.MaxLength + 1)), "CalendarEvent.LocationTooLong",
            "Event location must be {0} characters or fewer.", ShortTextPolicy.MaxLength);
        ErrorAssert.Validation(Event(title: " "), "CalendarEvent.TitleEmpty", "Event title cannot be empty.");
        ErrorAssert.Validation(Event(status: "nonsense"), "CalendarEvent.StatusInvalid", "Event status is not a recognised value.");
    }

    /// <summary>Verifies the title / description / location length limits, the empty title and the unknown status on <c>CalendarEvent.Update</c>.</summary>
    [Fact]
    public void CalendarEvent_Update_AcceptsTextAtTheLimits_AndRefusesOneMore()
    {
        CalendarEvent ev = Event().Value;

        Assert.False(ev.Update(Text(TitledContentPolicy.MaxTitleLength), Text(TitledContentPolicy.MaxBodyLength), false,
            _start, _start.AddHours(1), "UTC", "UTC", Text(ShortTextPolicy.MaxLength), null, Now).IsError);

        ErrorAssert.Validation(ev.Update(Text(TitledContentPolicy.MaxTitleLength + 1), null, false, _start, _start.AddHours(1), "UTC", "UTC", null, null, Now),
            "CalendarEvent.TitleTooLong", "Event title must be {0} characters or fewer.", TitledContentPolicy.MaxTitleLength);
        ErrorAssert.Validation(ev.Update("t", Text(TitledContentPolicy.MaxBodyLength + 1), false, _start, _start.AddHours(1), "UTC", "UTC", null, null, Now),
            "CalendarEvent.DescriptionTooLong", "Event description must be {0} characters or fewer.", TitledContentPolicy.MaxBodyLength);
        ErrorAssert.Validation(ev.Update("t", null, false, _start, _start.AddHours(1), "UTC", "UTC", Text(ShortTextPolicy.MaxLength + 1), null, Now),
            "CalendarEvent.LocationTooLong", "Event location must be {0} characters or fewer.", ShortTextPolicy.MaxLength);
        ErrorAssert.Validation(ev.Update(" ", null, false, _start, _start.AddHours(1), "UTC", "UTC", null, null, Now),
            "CalendarEvent.TitleEmpty", "Event title cannot be empty.");
        ErrorAssert.Validation(ev.Update("t", null, false, _start, _start.AddHours(1), "UTC", "UTC", null, "nonsense", Now),
            "CalendarEvent.StatusInvalid", "Event status is not a recognised value.");
    }

    /// <summary>Verifies that a zero-length event is valid and an end one minute before the start is refused.</summary>
    [Fact]
    public void CalendarEvent_EndEqualToStart_IsAccepted_ButEndBeforeStart_IsRefused()
    {
        Assert.False(CalendarEvent.Create(1, "t", null, false, _start, _start, "UTC", "UTC", null, null, Now).IsError);

        ErrorAssert.Validation(CalendarEvent.Create(1, "t", null, false, _start, _start.AddMinutes(-1), "UTC", "UTC", null, null, Now),
            "CalendarEvent.InvalidDateRange", "End date must not be earlier than start date.");
    }

    // ---- Money ----------------------------------------------------------------------------

    /// <summary>Verifies that a 45-character currency code is accepted, a 46-character or blank one is refused.</summary>
    [Fact]
    public void Money_Create_AcceptsACurrencyAtTheLimit_AndRefusesOneMore()
    {
        Assert.False(Money.Create(1m, Text(45)).IsError);

        ErrorAssert.Validation(Money.Create(1m, Text(46)), "Money.CurrencyTooLong", "Currency code must be {0} characters or fewer.", 45);
        ErrorAssert.Validation(Money.Create(1m, " "), "Money.CurrencyEmpty", "Currency code cannot be empty.");
    }

    /// <summary>Verifies that adding or subtracting mixed currencies reports both currency codes as template arguments.</summary>
    [Fact]
    public void Money_ArithmeticAcrossCurrencies_ReportsBothCodes()
    {
        Money krw = Money.Create(10m, "KRW").Value;
        Money usd = Money.Create(10m, "USD").Value;

        ErrorAssert.Validation(krw.Add(usd), "Money.CurrencyMismatch", "Cannot add {0} and {1}.", "KRW", "USD");
        ErrorAssert.Validation(krw.Subtract(usd), "Money.CurrencyMismatch", "Cannot subtract {0} from {1}.", "USD", "KRW");
    }

    // ---- Attached files (board and calendar event share the rules) ---------------------------

    /// <summary>Verifies the size, empty and length limits of a board attachment (a size of 0 is legal).</summary>
    [Fact]
    public void BoardAttachedFile_Create_AcceptsTheLimits_AndRefusesOneMore()
    {
        Assert.False(BoardAttachedFile.Create(1, 0, Text(ShortTextPolicy.MaxLength), Text(ShortTextPolicy.MaxLength), Text(ShortTextPolicy.MaxLength)).IsError); // size 0 is a legal (empty) size

        ErrorAssert.Validation(BoardAttachedFile.Create(1, -1, "n", ".e", "p"), "BoardAttachedFile.InvalidSize", "File size cannot be negative.");
        ErrorAssert.Validation(BoardAttachedFile.Create(1, 1, " ", ".e", "p"), "BoardAttachedFile.NameEmpty", "File name cannot be empty.");
        ErrorAssert.Validation(BoardAttachedFile.Create(1, 1, "n", ".e", " "), "BoardAttachedFile.PathEmpty", "File storage path cannot be empty.");
        ErrorAssert.Validation(BoardAttachedFile.Create(1, 1, Text(ShortTextPolicy.MaxLength + 1), ".e", "p"), "BoardAttachedFile.NameTooLong", "File name must be {0} characters or fewer.", ShortTextPolicy.MaxLength);
        ErrorAssert.Validation(BoardAttachedFile.Create(1, 1, "n", Text(ShortTextPolicy.MaxLength + 1), "p"), "BoardAttachedFile.ExtensionTooLong", "File extension must be {0} characters or fewer.", ShortTextPolicy.MaxLength);
        ErrorAssert.Validation(BoardAttachedFile.Create(1, 1, "n", ".e", Text(ShortTextPolicy.MaxLength + 1)), "BoardAttachedFile.PathTooLong", "File storage path must be {0} characters or fewer.", ShortTextPolicy.MaxLength);
    }

    /// <summary>Verifies the size, empty and length limits of a calendar-event attachment (a size of 0 is legal).</summary>
    [Fact]
    public void CalendarEventAttachedFile_Create_AcceptsTheLimits_AndRefusesOneMore()
    {
        Assert.False(CalendarEventAttachedFile.Create(1, 0, Text(ShortTextPolicy.MaxLength), Text(ShortTextPolicy.MaxLength), Text(ShortTextPolicy.MaxLength)).IsError);

        ErrorAssert.Validation(CalendarEventAttachedFile.Create(1, -1, "n", ".e", "p"), "CalendarEventAttachedFile.InvalidSize", "File size cannot be negative.");
        ErrorAssert.Validation(CalendarEventAttachedFile.Create(1, 1, " ", ".e", "p"), "CalendarEventAttachedFile.NameEmpty", "File name cannot be empty.");
        ErrorAssert.Validation(CalendarEventAttachedFile.Create(1, 1, "n", ".e", " "), "CalendarEventAttachedFile.PathEmpty", "File storage path cannot be empty.");
        ErrorAssert.Validation(CalendarEventAttachedFile.Create(1, 1, "n", Text(ShortTextPolicy.MaxLength + 1), "p"), "CalendarEventAttachedFile.ExtensionTooLong", "File extension must be {0} characters or fewer.", ShortTextPolicy.MaxLength);
        ErrorAssert.Validation(CalendarEventAttachedFile.Create(1, 1, "n", ".e", Text(ShortTextPolicy.MaxLength + 1)), "CalendarEventAttachedFile.PathTooLong", "File storage path must be {0} characters or fewer.", ShortTextPolicy.MaxLength);
    }
}

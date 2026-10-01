using Maroik.Core.Domain.Board;
using Maroik.Core.Domain.Calendar;
using DomainBoard = Maroik.Core.Domain.Board.Board;
using DomainCalendar = Maroik.Core.Domain.Calendar.Calendar;
namespace Maroik.Core.Domain.Tests.Boundaries;

/// <summary>
/// The domain never reads the clock: every factory stamps Created and Updated with the one instant
/// passed in (so they cannot drift apart), and every later change stamps only Updated with its own
/// given instant.
/// </summary>
public class ClockTests
{
    /// <summary>The creation instant passed to the factories.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A later instant passed to a state change.</summary>
    private static readonly DateTime Later = Now.AddMinutes(5);

    /// <summary>Calendar: create stamps both, update stamps Updated only.</summary>
    [Fact]
    public void Calendar_CreateAndUpdate_UseTheGivenInstants()
    {
        DomainCalendar calendar = DomainCalendar.Create("a@b.com", "Work", null, "UTC", "#112233", Now).Value;
        Assert.Equal((Now, Now), (calendar.Created, calendar.Updated));

        Assert.False(calendar.Update("Home", null, "UTC", "#112233", Later).IsError);
        Assert.Equal((Now, Later), (calendar.Created, calendar.Updated));
    }

    /// <summary>Calendar event: create stamps both; update and reassign stamp Updated only.</summary>
    [Fact]
    public void CalendarEvent_CreateUpdateAndReassign_UseTheGivenInstants()
    {
        DateTime start = new(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        CalendarEvent ev = CalendarEvent.Create(10, "Stand-up", null, false, start, start.AddHours(1), null, null, null, null, Now).Value;
        Assert.Equal((Now, Now), (ev.Created, ev.Updated));

        Assert.False(ev.Update("Stand-up", null, false, start, start.AddHours(2), null, null, null, null, Later).IsError);
        Assert.Equal((Now, Later), (ev.Created, ev.Updated));

        ev.Reassign(11, null, Later.AddMinutes(1));
        Assert.Equal(Later.AddMinutes(1), ev.Updated);
    }

    /// <summary>Board: create stamps both; every state change stamps Updated only.</summary>
    [Fact]
    public void Board_CreateAndChanges_UseTheGivenInstants()
    {
        DomainBoard board = DomainBoard.Create(BoardTypes.FreeForum, "t", "c", "Alice", Now).Value;
        Assert.Equal((Now, Now), (board.Created, board.Updated));

        board.Lock(Later);
        Assert.Equal((Now, Later), (board.Created, board.Updated));
        board.Unlock(Later.AddMinutes(1));
        Assert.Equal(Later.AddMinutes(1), board.Updated);
        board.Pin(Later.AddMinutes(2));
        Assert.Equal(Later.AddMinutes(2), board.Updated);
        board.Unpin(Later.AddMinutes(3));
        Assert.Equal(Later.AddMinutes(3), board.Updated);
        Assert.False(board.Update("t2", "c2", Later.AddMinutes(4)).IsError);
        Assert.Equal(Later.AddMinutes(4), board.Updated);
        Assert.False(board.SoftDelete(Later.AddMinutes(5)).IsError);
        Assert.Equal(Later.AddMinutes(5), board.Updated);
    }

    /// <summary>A board comment's Created is the instant passed in.</summary>
    [Fact]
    public void BoardComment_Create_UsesTheGivenInstant()
        => Assert.Equal(Now, BoardComment.Create(1, 1, null, "Bob", "hi", Now).Value.Created);
}

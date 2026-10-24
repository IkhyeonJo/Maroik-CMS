using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Messages;
using Maroik.Core.Contract.Settings;
using Maroik.Core.Domain.Accounts;
using Maroik.Core.Domain.Calendars;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CalendarReminderDispatchService"/>: each due reminder is handled in its own transaction — e-mailed to its
/// calendar's owner through the mail queue (subject and body in the owner's time zone, a link to the owner's calendar page) or
/// skipped with its reason — and marked handled; a queue failure or an unexpected one rolls back and ends the run, logged.
/// </summary>
public class CalendarReminderDispatchServiceTests
{
    /// <summary>"Now": 2026-11-02 08:30 UTC (17:30 in Seoul).</summary>
    private static readonly DateTime Now = new(2026, 11, 2, 8, 30, 0, DateTimeKind.Utc);

    private readonly Mock<ICalendarEventReminderRepository> _reminders = new();
    private readonly Mock<ICalendarEventRepository> _events = new();
    private readonly Mock<ICalendarRepository> _calendars = new();
    private readonly Mock<IAccountRepository> _accounts = new();
    private readonly Mock<IEmailPublisher> _publisher = new();
    private readonly Mock<IMailClient> _mailClient = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeLogger<CalendarReminderDispatchService> _logger = new();
    private readonly List<SendEmailMessage> _published = [];
    private readonly List<CalendarEventReminder> _saved = [];

    /// <summary>Wires the doubles: an empty queue, a mail body echoing its inputs, a publisher that records.</summary>
    public CalendarReminderDispatchServiceTests()
    {
        _reminders.Setup(r => r.LockDueAsync(It.IsAny<DateTime>(), 1, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _reminders.Setup(r => r.UpdateEntityAsync(It.IsAny<CalendarEventReminder>(), It.IsAny<CancellationToken>()))
            .Callback<CalendarEventReminder, CancellationToken>((r, _) => _saved.Add(r)).Returns(Task.CompletedTask);
        _mailClient.Setup(m => m.GetCalendarReminderBody(It.IsAny<CalendarReminderMail>(), It.IsAny<string>()))
            .Returns<CalendarReminderMail, string>((mail, domain) => $"{mail} @ {domain}");
        _publisher.Setup(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SendEmailMessage, CancellationToken>((m, _) => _published.Add(m)).Returns(Task.CompletedTask);
    }

    /// <summary>The service under test.</summary>
    private CalendarReminderDispatchService Sut() => new(_reminders.Object, _events.Object, _calendars.Object, _accounts.Object,
        _publisher.Object, _mailClient.Object, _unitOfWork.Object,
        Options.Create(new ServerSetting { DomainName = "https://www.maroik.com/", DemoAccountEmail = "demo@maroik.com" }),
        _logger, new FakeTimeProvider(new DateTimeOffset(Now)));

    /// <summary>Queues reminder <paramref name="id"/> (due at <paramref name="fireAt"/>) of event 41 in calendar 7, owned by <paramref name="owner"/>.</summary>
    private CalendarEventReminder GivenDue(long id, Account? owner, CalendarEvent? calendarEvent = null, DateTime? fireAt = null)
    {
        var reminder = CalendarEventReminder.Reconstitute(id, 41, "Email", 30, null, null, null, null, fireAt ?? Now, null);
        _reminders.SetupSequence(r => r.LockDueAsync(Now, 1, It.IsAny<CancellationToken>())).ReturnsAsync([reminder]).ReturnsAsync([]);
        _events.Setup(e => e.FindByIdAsync(41, It.IsAny<CancellationToken>())).ReturnsAsync(calendarEvent ?? TimedEvent());
        _calendars.Setup(c => c.FindByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Calendar.Reconstitute(7, "owner@example.com", "Home & Work", null, "Asia/Seoul", "#3788d8", Now, Now));
        _accounts.Setup(a => a.FindByEmailAsync("owner@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(owner);
        return reminder;
    }

    /// <summary>An account in Seoul.</summary>
    private static Account Owner(bool locked = false, string role = Role.User) =>
        Account.Reconstitute("owner@example.com", "hash", "Owner", null, role, "Asia/Seoul", null, locked, 0, true, true, null, null,
            Now, Now, null, false, "stamp", false);

    /// <summary>Event 41, 09:00–10:00 UTC (18:00–19:00 in Seoul) on 2026-11-02, at "Room 2".</summary>
    private static CalendarEvent TimedEvent() => CalendarEvent.Reconstitute(41, 7, "Dentist", null, false,
        new DateTime(2026, 11, 2, 9, 0, 0), new DateTime(2026, 11, 2, 10, 0, 0), "UTC", "UTC", "Room 2", "Busy", null, Now, Now);

    /// <summary>A due reminder is e-mailed to the owner (in the owner's time zone), marked handled and committed, and logged.</summary>
    [Fact]
    public async Task ADueReminder_IsEmailedToTheOwner_AndMarkedHandled()
    {
        GivenDue(3, Owner());

        int handled = await Sut().DispatchDueAsync(10, TestContext.Current.CancellationToken);

        Assert.Equal(1, handled);
        SendEmailMessage mail = Assert.Single(_published);
        Assert.Equal(("owner@example.com", "[Maroik] Dentist — 2026-11-02 18:00 (Asia/Seoul)"), (mail.ToEmail, mail.Subject));
        Assert.Equal(new CalendarReminderMail("Dentist", "2026-11-02 18:00 – 19:00 (Asia/Seoul)", "Room 2", "Home & Work", "Calendar/UserIndex")
            + " @ https://www.maroik.com/", mail.Body);
        Assert.Equal(Now, Assert.Single(_saved).HandledAt);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once); // the empty second look
        FakeLogRecord log = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal((LogLevel.Information, "Sent calendar reminder 3 of event 41 to owner@example.com"), (log.Level, log.Message));
    }

    /// <summary>An all-day event shows its dates with "all day"; an administrator's link goes to the admin calendar page.</summary>
    [Fact]
    public async Task AnAllDayEvent_ShowsItsDates_AndAnAdministratorGetsTheAdminPage()
    {
        GivenDue(4, Owner(role: Role.Admin), CalendarEvent.Reconstitute(41, 7, "Trip", null, true,
            new DateTime(2026, 11, 3), new DateTime(2026, 11, 5), null, null, null, "Busy", null, Now, Now));

        await Sut().DispatchDueAsync(10, TestContext.Current.CancellationToken);

        SendEmailMessage mail = Assert.Single(_published);
        Assert.Equal("[Maroik] Trip — 2026-11-03 (종일 / All day)", mail.Subject);
        Assert.Equal(new CalendarReminderMail("Trip", "2026-11-03 – 2026-11-05 (종일 / All day)", null, "Home & Work", "Calendar/AdminIndex")
            + " @ https://www.maroik.com/", mail.Body);
    }

    /// <summary>A reminder the policy skips is marked handled without a mail, and the reason is logged.</summary>
    [Fact]
    public async Task ASkippedReminder_IsHandled_WithoutAMail()
    {
        GivenDue(5, Owner(locked: true));

        int handled = await Sut().DispatchDueAsync(10, TestContext.Current.CancellationToken);

        Assert.Equal(1, handled);
        Assert.Empty(_published);
        Assert.Equal(Now, Assert.Single(_saved).HandledAt);
        FakeLogRecord log = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal((LogLevel.Information, "Skipped calendar reminder 5 of event 41: the owner's account is locked"), (log.Level, log.Message));
    }

    /// <summary>When the mail queue refuses the message, the reminder stays due (rolled back, not saved) and the run ends, logged as a warning.</summary>
    [Fact]
    public async Task AQueueFailure_RollsBack_AndEndsTheRun()
    {
        GivenDue(6, Owner());
        var down = new InvalidOperationException("broker down");
        _publisher.Setup(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>())).ThrowsAsync(down);

        int handled = await Sut().DispatchDueAsync(10, TestContext.Current.CancellationToken);

        Assert.Equal(0, handled);
        Assert.Empty(_saved);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        FakeLogRecord log = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal((LogLevel.Warning, "Could not queue calendar reminder 6 for owner@example.com; it is tried again on the next run"),
            (log.Level, log.Message));
        Assert.Same(down, log.Exception);
    }

    /// <summary>An unexpected failure rolls back and ends the run, logged as an error with the exception.</summary>
    [Fact]
    public async Task AnUnexpectedFailure_RollsBack_AndEndsTheRun()
    {
        var broken = new InvalidOperationException("db");
        _reminders.Setup(r => r.LockDueAsync(It.IsAny<DateTime>(), 1, It.IsAny<CancellationToken>())).ThrowsAsync(broken);

        int handled = await Sut().DispatchDueAsync(10, TestContext.Current.CancellationToken);

        Assert.Equal(0, handled);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        FakeLogRecord log = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal((LogLevel.Error, "Failed to handle the due calendar reminders"), (log.Level, log.Message));
        Assert.Same(broken, log.Exception);
    }

    /// <summary>A run handles at most <c>max</c> reminders, so one run cannot go on for ever; with nothing due it does nothing.</summary>
    [Fact]
    public async Task ARun_StopsAfterMax_AndDoesNothingWhenNothingIsDue()
    {
        Assert.Equal(0, await Sut().DispatchDueAsync(10, TestContext.Current.CancellationToken));
        Assert.Empty(_published);

        GivenDue(7, Owner());
        _reminders.Setup(r => r.LockDueAsync(Now, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => [CalendarEventReminder.Reconstitute(7, 41, "Email", 30, null, null, null, null, Now, null)]);

        Assert.Equal(2, await Sut().DispatchDueAsync(2, TestContext.Current.CancellationToken));
        Assert.Equal(2, _published.Count);
    }
}

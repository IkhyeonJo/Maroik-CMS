using System.Text.RegularExpressions;
using ErrorOr;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Board;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Domain.ValueObjects;
using DomainAccount = Maroik.Core.Domain.Account.Account;
using DomainCalendar = Maroik.Core.Domain.Calendar.Calendar;
using DomainIncome = Maroik.Core.Domain.Finance.Income;

namespace Maroik.Core.Domain.Tests.Boundaries;

/// <summary>Rules the mutation run showed no test noticed: boundaries, value equality, ownership branches, round trips.</summary>
public class LogicGapTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Owner e-mail used by every aggregate built in these tests.</summary>
    private const string Email = "user@example.com";

    // ---- Fixed income / fixed expenditure: month & maturity boundaries ------------------------

    /// <summary>Result of registering a fixed income on the given deposit month/day (no maturity unless given).</summary>
    private static ErrorOr<FixedIncome> Income(short month, short day = 1, DateTime? maturity = null) =>
        FixedIncome.Register(Email, "RegularIncome", "LaborIncome", "c", 1000m, "KRW", "asset", month, day, maturity ?? FixedSchedulePolicy.NoMaturityDate, Now);

    /// <summary>Result of registering a fixed expenditure on the given deposit month/day (no maturity unless given).</summary>
    private static ErrorOr<FixedExpenditure> Expense(short month, short day = 1, DateTime? maturity = null) =>
        FixedExpenditure.Register(Email, "ConsumerSpending", "MealOrEatOutExpenses", "c", 1000m, "KRW", "asset", null, month, day, maturity ?? FixedSchedulePolicy.NoMaturityDate, Now);

    /// <summary>Verifies that months 1 and 12 are accepted as the deposit month.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public void FixedIncomeAndExpenditure_AcceptTheFirstAndLastMonth(short month)
    {
        Assert.False(Income(month).IsError);
        Assert.False(Expense(month).IsError);
    }

    /// <summary>Verifies that months 0 and 13 are refused as the deposit month.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void FixedIncomeAndExpenditure_RefuseMonthsOutsideOneToTwelve(short month)
    {
        ErrorAssert.Validation(Income(month), "FixedIncome.InvalidDepositMonth", "Deposit month must be between 1 and 12.");
        ErrorAssert.Validation(Expense(month), "FixedExpenditure.InvalidDepositMonth", "Deposit month must be between 1 and 12.");
    }

    /// <summary>Verifies the invalid-day and empty-field errors of fixed income and fixed expenditure registration.</summary>
    [Fact]
    public void FixedIncomeAndExpenditure_ErrorContract()
    {
        ErrorAssert.Validation(Income(2, 30), "FixedIncome.InvalidDepositDay", "Deposit day is not valid for the selected month.");
        ErrorAssert.Validation(Expense(2, 30), "FixedExpenditure.InvalidDepositDay", "Deposit day is not valid for the selected month.");

        ErrorAssert.Validation(FixedIncome.Register(Email, " ", "LaborIncome", "c", 1m, "KRW", "a", 1, 1, Now.AddDays(1), Now), "FixedIncome.MainClassEmpty", "Main income category cannot be empty.");
        ErrorAssert.Validation(FixedIncome.Register(Email, "RegularIncome", " ", "c", 1m, "KRW", "a", 1, 1, Now.AddDays(1), Now), "FixedIncome.SubClassEmpty", "Sub income category cannot be empty.");
        ErrorAssert.Validation(FixedIncome.Register(Email, "RegularIncome", "LaborIncome", "c", 1m, "KRW", " ", 1, 1, Now.AddDays(1), Now), "FixedIncome.DepositAssetEmpty", "Deposit asset product name cannot be empty.");
        ErrorAssert.Validation(FixedExpenditure.Register(Email, " ", "MealOrEatOutExpenses", "c", 1m, "KRW", "a", null, 1, 1, Now.AddDays(1), Now), "FixedExpenditure.MainClassEmpty", "Main expense category cannot be empty.");
        ErrorAssert.Validation(FixedExpenditure.Register(Email, "ConsumerSpending", " ", "c", 1m, "KRW", "a", null, 1, 1, Now.AddDays(1), Now), "FixedExpenditure.SubClassEmpty", "Sub expense category cannot be empty.");
        ErrorAssert.Validation(FixedExpenditure.Register(Email, "ConsumerSpending", "MealOrEatOutExpenses", "c", 1m, "KRW", " ", null, 1, 1, Now.AddDays(1), Now), "FixedExpenditure.PaymentMethodEmpty", "Payment method cannot be empty.");
    }

    /// <summary>Verifies that a maturity date in the past is refused both when registering and when updating.</summary>
    [Fact]
    public void FixedIncomeAndExpenditure_MaturityInThePast_IsRefusedOnRegisterAndOnUpdate()
    {
        DateTime past = Now.AddDays(-30);
        const string template = "The maturity date cannot be earlier than the current date.";

        ErrorAssert.Validation(Income(1, 1, past), "FixedIncome.MaturityDateInPast", template);
        ErrorAssert.Validation(Expense(1, 1, past), "FixedExpenditure.MaturityDateInPast", template);
        ErrorAssert.Validation(Income(1).Value.Update("RegularIncome", "LaborIncome", "c", 1m, "KRW", "asset", 1, 1, past, null, Now), "FixedIncome.MaturityDateInPast", template);
        ErrorAssert.Validation(Expense(1).Value.Update("ConsumerSpending", "MealOrEatOutExpenses", "c", 1m, "KRW", "asset", null, 1, 1, past, null, Now), "FixedExpenditure.MaturityDateInPast", template);
    }

    /// <summary>Verifies the edges of the notice window and the one-day grace for a maturity date.</summary>
    [Fact]
    public void FixedSchedulePolicy_Boundaries()
    {
        DateTime today = new(2026, 6, 15, 13, 45, 0);

        Assert.True(FixedSchedulePolicy.IsNoticed(6, 15, today, 3, false));   // the deposit day itself is inside the window
        Assert.True(FixedSchedulePolicy.IsNoticed(6, 18, today, 3, false));   // exactly window days ahead
        Assert.False(FixedSchedulePolicy.IsNoticed(6, 19, today, 3, false));  // one day beyond
        Assert.False(FixedSchedulePolicy.IsNoticed(6, 14, today, 3, false));  // yesterday: this year's occurrence has passed

        Assert.True(FixedSchedulePolicy.IsAcceptableMaturityDate(today.AddDays(-1), today));  // one day of grace for timezones
        Assert.False(FixedSchedulePolicy.IsAcceptableMaturityDate(today.AddDays(-2), today));
        Assert.Equal("9999-12-31", FixedSchedulePolicy.NoMaturityDateIso);
    }

    // ---- Value-object equality --------------------------------------------------------------

    /// <summary>Verifies that two <c>Money</c> values are equal (with equal hash codes) only when amount and currency both match.</summary>
    [Fact]
    public void Money_EqualityConsidersBothAmountAndCurrency()
    {
        Money krw10 = Money.Create(10m, "KRW").Value;

        Assert.Equal(krw10, Money.Create(10m, "KRW").Value);
        Assert.Equal(krw10.GetHashCode(), Money.Create(10m, "KRW").Value.GetHashCode());
        Assert.NotEqual(krw10, Money.Create(10m, "USD").Value);
        Assert.NotEqual(krw10, Money.Create(11m, "KRW").Value);
    }

    /// <summary>Verifies value equality of color codes, time-zone IDs and emails (emails compare case-insensitively).</summary>
    [Fact]
    public void HtmlColorCodeTimeZoneIdAndEmail_AreEqualExactlyWhenTheirValuesAre()
    {
        Assert.Equal(HtmlColorCode.Create("#112233").Value, HtmlColorCode.Create("#112233").Value);
        Assert.NotEqual(HtmlColorCode.Create("#112233").Value, HtmlColorCode.Create("#445566").Value);
        Assert.Equal(TimeZoneId.Create("UTC").Value, TimeZoneId.Create("UTC").Value);
        Assert.NotEqual(TimeZoneId.Create("UTC").Value, TimeZoneId.Create("Asia/Seoul").Value);
        Assert.Equal(Domain.ValueObjects.Email.Create("a@b.com").Value, Domain.ValueObjects.Email.Create("A@B.com").Value);
        Assert.NotEqual(Domain.ValueObjects.Email.Create("a@b.com").Value, Domain.ValueObjects.Email.Create("c@b.com").Value);
    }

    // ---- Email parsing ------------------------------------------------------------------------

    /// <summary>Verifies that ordinary addresses, including dotted local parts and sub-domains, are accepted.</summary>
    [Theory]
    [InlineData("a@b.co")]
    [InlineData("first.last@sub.example.com")]
    public void Email_AcceptsOrdinaryAddresses(string address) => Assert.False(Domain.ValueObjects.Email.Create(address).IsError);

    /// <summary>Verifies that malformed hosts, local parts and the display-name form are rejected with <c>Email.Invalid</c>.</summary>
    [Theory]
    [InlineData("a@localhost")]     // host without a dot
    [InlineData("a@b..com")]        // empty host label
    [InlineData("a@.com")]
    [InlineData("a@b.c")]           // one-letter top-level domain
    [InlineData("a..b@x.com")]      // empty local-part label
    [InlineData(".a@x.com")]
    [InlineData("a.@x.com")]
    [InlineData("Jane <jane@x.com>")] // display-name form
    public void Email_RejectsMalformedAddresses(string address)
    {
        ErrorAssert.Validation(Domain.ValueObjects.Email.Create(address), "Email.Invalid", "'{0}' is not a valid email address.", address);
    }

    // ---- Account --------------------------------------------------------------------------------

    /// <summary>A persisted, confirmed account with the given lockout state.</summary>
    private static DomainAccount Account(bool locked = false, long loginAttempt = 0) =>
        DomainAccount.Reconstitute(Email, "$2a$hash", "Nick", null, Role.User, "UTC", null, locked, loginAttempt, true, true,
            null, null, DateTime.UtcNow, DateTime.UtcNow, null, false, "stamp", false);

    /// <summary>Verifies that locking keeps the login attempt count and unlocking clears it.</summary>
    [Fact]
    public void Lock_KeepsTheAttemptCount_AndUnlockClearsIt()
    {
        DomainAccount account = Account(locked: false, loginAttempt: 3);
        account.Lock(Now);
        Assert.Equal(3, account.LoginAttempt);

        account.Unlock(Now);
        Assert.Equal(0, account.LoginAttempt);
    }

    /// <summary>Verifies that each new account gets a different 32-hex-digit security stamp.</summary>
    [Fact]
    public void NewAccounts_GetADistinctHyphenlessSecurityStamp()
    {
        string stamp1 = DomainAccount.Create(Email, "$2a$h", "Nick", Role.User, "UTC", null, GuidToken.Generate(Now), true, Now).Value.SecurityStamp;
        string stamp2 = DomainAccount.Create(Email, "$2a$h", "Nick", Role.User, "UTC", null, GuidToken.Generate(Now), true, Now).Value.SecurityStamp;

 #pragma warning disable SYSLIB1045
        Assert.Matches(new Regex("^[0-9a-f]{32}$"), stamp1);
 #pragma warning restore SYSLIB1045
        Assert.NotEqual(stamp1, stamp2);
    }

    /// <summary>Verifies that <c>Account.Reconstitute</c> restores every field (the email lower-cased).</summary>
    [Fact]
    public void Account_Reconstitute_RestoresEveryFieldVerbatim()
    {
        DateTime created = new(2025, 1, 2, 3, 4, 5), updated = new(2025, 6, 7, 8, 9, 10);

        DomainAccount account = DomainAccount.Reconstitute("Mixed@Case.com", "$2a$hash", "Nick", "/a.png", Role.Admin, "Asia/Seoul", "KRW",
            locked: true, loginAttempt: 4, emailConfirmed: true, agreedServiceTerms: true, registrationToken: "reg", resetPasswordToken: "reset",
            created, updated, "msg", deleted: true, securityStamp: "stamp", mustChangePassword: true);

        Assert.Equal("mixed@case.com", account.Email.Value);
        Assert.Equal("$2a$hash", account.HashedPassword);
        Assert.Equal("Nick", account.Nickname);
        Assert.Equal("/a.png", account.AvatarImagePath);
        Assert.Equal(Role.Admin, account.Role);
        Assert.Equal("Asia/Seoul", account.TimeZone.Value);
        Assert.Equal("KRW", account.DefaultMonetaryUnit);
        Assert.True(account.Locked);
        Assert.Equal(4, account.LoginAttempt);
        Assert.True(account.EmailConfirmed);
        Assert.True(account.AgreedServiceTerms);
        Assert.Equal("reg", account.RegistrationToken);
        Assert.Equal("reset", account.ResetPasswordToken);
        Assert.Equal(created, account.Created);
        Assert.Equal(updated, account.Updated);
        Assert.Equal("msg", account.Message);
        Assert.True(account.Deleted);
        Assert.Equal("stamp", account.SecurityStamp);
        Assert.True(account.MustChangePassword);
    }

    // ---- Income / Expenditure / CalendarEvent round trips ---------------------------------------

    /// <summary>Verifies that <c>Income</c> and <c>Expenditure</c> reconstitution restore every field.</summary>
    [Fact]
    public void IncomeAndExpenditure_Reconstitute_RestoreEveryFieldVerbatim()
    {
        DateTime created = new(2025, 1, 2), updated = new(2025, 3, 4);

        DomainIncome income = DomainIncome.Reconstitute(7, Email, "RegularIncome", "LaborIncome", "c", 123.45m, "KRW", "asset", "n", created, updated);
        Assert.Equal((7, Email, "RegularIncome", "LaborIncome", "c", "asset", "n", created, updated),
            (income.Id, income.AccountEmail.Value, income.MainClass, income.SubClass, income.Content, income.DepositMyAssetProductName, income.Note, income.Created, income.Updated));
        Assert.Equal(Money.Create(123.45m, "KRW").Value, income.Amount);

        Expenditure expenditure = Expenditure.Reconstitute(8, Email, "ConsumerSpending", "MealOrEatOutExpenses", "c", 5m, "KRW", "pay", "dep", "n", created, updated);
        Assert.Equal((8, Email, "ConsumerSpending", "MealOrEatOutExpenses", "c", "pay", "dep", "n", created, updated),
            (expenditure.Id, expenditure.AccountEmail.Value, expenditure.MainClass, expenditure.SubClass, expenditure.Content, expenditure.PaymentMethod, expenditure.MyDepositAsset, expenditure.Note, expenditure.Created, expenditure.Updated));
        Assert.Equal(Money.Create(5m, "KRW").Value, expenditure.Amount);
    }

    /// <summary>Verifies that <c>Reassign</c> sets the new calendar and recurrence IDs and does not move <c>Updated</c> backwards.</summary>
    [Fact]
    public void CalendarEvent_Reassign_MovesTheEventAndTouchesUpdated()
    {
        DateTime start = new(2026, 1, 1, 9, 0, 0);
        CalendarEvent ev = CalendarEvent.Create(1, "t", null, false, start, start.AddHours(1), "UTC", "UTC", null, null).Value;
        DateTime before = ev.Updated;

        ev.Reassign(5, 9);

        Assert.Equal(5, ev.CalendarId);
        Assert.Equal(9, ev.RecurrenceId);
        Assert.True(ev.Updated >= before);
    }

    // ---- Board ownership --------------------------------------------------------------------------

    /// <summary>Verifies that a forum post may be deleted by its writer or an admin, but a private note only by its writer.</summary>
    [Fact]
    public void Board_CanBeDeletedBy_FollowsThePrivateNoteAndForumRules()
    {
        Domain.Board.Board forum = Domain.Board.Board.Reconstitute(1, BoardTypes.FreeForum, "t", "c", "Alice", DateTime.UtcNow, DateTime.UtcNow, 0L, false, false, false);
        Domain.Board.Board note = Domain.Board.Board.Reconstitute(2, BoardTypes.PrivateNote, "t", "c", "Alice", DateTime.UtcNow, DateTime.UtcNow, 0L, false, false, false);

        Assert.True(forum.CanBeDeletedBy("Alice", isAdmin: false));
        Assert.True(forum.CanBeDeletedBy("Admin", isAdmin: true));
        Assert.False(forum.CanBeDeletedBy("Bob", isAdmin: false));

        Assert.True(note.CanBeDeletedBy("Alice", isAdmin: false));
        Assert.False(note.CanBeDeletedBy("Admin", isAdmin: true)); // a private note has no admin bypass
        Assert.False(note.CanBeDeletedBy("Bob", isAdmin: false));
    }

    // ---- Reminders --------------------------------------------------------------------------------

    /// <summary>Verifies the method and lead-time errors of <c>CalendarEventReminder.Create</c>.</summary>
    [Fact]
    public void Reminder_Create_ErrorContract()
    {
        ErrorAssert.Validation(CalendarEventReminder.Create(1, " ", 5, null, null, null, null), "Reminder.MethodEmpty", "Notification method cannot be empty.");
        ErrorAssert.Validation(CalendarEventReminder.Create(1, "nonsense", 5, null, null, null, null), "Reminder.MethodInvalid", "Notification method is not a recognised value.");
        ErrorAssert.Validation(CalendarEventReminder.Create(1, "Email", null, null, null, null, null), "Reminder.InvalidLeadTime", "Exactly one lead-time field (minutes/hours/days/weeks) must be set.");
        ErrorAssert.Validation(CalendarEventReminder.Create(1, "Email", 5, 1, null, null, null), "Reminder.InvalidLeadTime", "Exactly one lead-time field (minutes/hours/days/weeks) must be set.");
        ErrorAssert.Validation(CalendarEventReminder.Create(1, "Email", null, null, null, 5, null), "Reminder.LeadTimeOutOfRange",
            "Reminder lead time is outside the allowed range (0 to 28 days before the event).");
    }

    // ---- Policies ---------------------------------------------------------------------------------

    /// <summary>Verifies that a still-used unit is kept, an unused one falls back to the most common asset currency, and no assets yield <see langword="null"/>.</summary>
    [Fact]
    public void DefaultMonetaryUnit_FallsBackToTheMostCommonCurrency()
    {
        IReadOnlyCollection<Asset> assets = [A("a", "USD"), A("b", "KRW"), A("c", "KRW")];

        Assert.Equal("KRW", DefaultMonetaryUnitPolicy.Resolve("EUR", assets));
        Assert.Equal("USD", DefaultMonetaryUnitPolicy.Resolve("USD", assets)); // a still-valid current unit wins over popularity
        Assert.Null(DefaultMonetaryUnitPolicy.Resolve("KRW", []));
        return;
 #pragma warning disable IDE0062
        Asset A(string name, string unit) => Asset.Create(name, Email, "FreeDepositAndWithdrawal", 1m, unit, Now).Value;
 #pragma warning restore IDE0062
    }

    /// <summary>Verifies that a breakdown whose total is zero reports amounts but no percentages.</summary>
    [Fact]
    public void FinanceBreakdown_WithATotalOfZero_HasNoPercentages()
    {
        FinanceBreakdownPolicy.SubClassBreakdown result = FinanceBreakdownPolicy.Compute([("a", 5m), ("a", -5m)]);

        Assert.Equal(0m, result.Total);
        Assert.Empty(result.PercentageBySubClass);
        Assert.Equal(0m, result.AmountBySubClass["a"]);
    }

    // ---- Reconstitution of the calendar aggregates and the asset note -------------------------------

    /// <summary>Verifies that <c>Calendar.Reconstitute</c> restores every field.</summary>
    [Fact]
    public void Calendar_Reconstitute_RestoresEveryField()
    {
        DateTime created = new(2025, 1, 2, 3, 4, 5), updated = new(2025, 6, 7, 8, 9, 10);

        DomainCalendar calendar = DomainCalendar.Reconstitute(4, Email, "Work", "desc", "Asia/Seoul", "#112233", created, updated);

        Assert.Equal((4L, Email, "Work", "desc", "Asia/Seoul", "#112233", created, updated),
            (calendar.Id, calendar.AccountEmail.Value, calendar.Name, calendar.Description, calendar.TimeZone.Value, calendar.ColorCode.Value, calendar.Created, calendar.Updated));
    }

    /// <summary>Verifies that <c>CalendarEvent.Reconstitute</c> restores every field.</summary>
    [Fact]
    public void CalendarEvent_Reconstitute_RestoresEveryField()
    {
        DateTime start = new(2026, 1, 1, 9, 0, 0), end = new(2026, 1, 1, 10, 0, 0), created = new(2025, 1, 2), updated = new(2025, 3, 4);

        CalendarEvent ev = CalendarEvent.Reconstitute(6, 2, "Title", "desc", true, start, end, "UTC", "Asia/Seoul", "Room", "Confirmed", 9, created, updated);

        Assert.Equal((6L, 2L, "Title", "desc", true, start, end, "UTC", "Asia/Seoul", "Room", "Confirmed", (long?)9, created, updated),
            (ev.Id, ev.CalendarId, ev.Title, ev.Description, ev.AllDay, ev.StartDate, ev.EndDate, ev.StartDateTimeZoneIanaId, ev.EndDateTimeZoneIanaId, ev.Location, ev.Status, ev.RecurrenceId, ev.Created, ev.Updated));
    }

    /// <summary>Verifies that the note passed to <c>Asset.Create</c> is kept.</summary>
    [Fact]
    public void Asset_Create_KeepsTheNote()
    {
        Assert.Equal("a note", Asset.Create("a", Email, "FreeDepositAndWithdrawal", 1m, "KRW", Now, "a note").Value.Note);
    }
}

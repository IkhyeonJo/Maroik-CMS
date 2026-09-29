using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for <c>UpdateCalendarShared</c> (Admin-only) and <c>UpdateOtherCalendar</c>
/// (subscribing to another account's shared calendar) on <c>CalendarController</c> — the last
/// two action groups left uncovered after <c>CalendarControllerCalendarTests</c>,
/// <c>CalendarControllerReadOnlyTests</c> and <c>CalendarControllerEventTests</c>. Runs against
/// a real PostgreSQL instance (via <see cref="MaroikWebApplicationFactory"/>'s Testcontainers
/// setup), so the <c>IUnitOfWork</c> transactions these two service methods wrap are exercised
/// for real.
/// </summary>
[Collection("Website Integration")]
public class CalendarControllerSharingTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    /// <summary>Seeds (if missing) an Admin account for <paramref name="email"/> and signs in as it.</summary>
    private Task<AuthenticatedSession> LoginAsAdminAsync(string email = "calendar-sharing-admin@test.com") =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "AdminPassword1!", Role.Admin, TestContext.Current.CancellationToken);

    /// <summary>A name unique to this call, prefixed with <paramref name="testName"/>.</summary>
    private static string UniqueName(string testName = "") =>
        $"{testName}-{Guid.NewGuid():N}";

    /// <summary>
    /// Creates a calendar as the given (Admin) session, then calls <c>GetCalendarShareds</c> —
    /// which backfills a <c>CalendarShared</c> row for every calendar the caller owns — so the
    /// new calendar has one before <c>UpdateCalendarShared</c> is exercised. Without this
    /// backfill step, <c>UpdateCalendarShared</c> would silently no-op: its repository call is a
    /// plain EF "find the existing row, then overwrite" that does nothing if the row is missing
    /// (see <c>CalendarControllerReadOnlyTests.GetCalendarShareds_AdminSession_ReturnsSuccessResult</c>).
    /// </summary>
    private async Task<long> CreateCalendarAndBackfillSharedAsync(AuthenticatedSession admin, string name)
    {
        var payload = new
        {
            Calendars = new[] { new { Id = 0, Name = name, Description = "", TimeZoneIanaId = "UTC", HtmlColorCode = "#3788d8" } }
        };
        using var createRequest = admin.BuildJsonPostRequest("/Calendar/CreateCalendar", payload);
        await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);

        using var getRequest = admin.BuildJsonPostRequest("/Calendar/GetCalendars");
        var getResponse = await _client.SendAsync(getRequest, TestContext.Current.CancellationToken);
        string getJson = await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = System.Text.Json.JsonDocument.Parse(getJson);
        long calendarId = 0;
        foreach (var calendar in doc.RootElement.GetProperty("calendars").EnumerateArray().Where(calendar => calendar.GetProperty("name").GetString() == name))
        {
            calendarId = calendar.GetProperty("id").GetInt64();
        }
        if (calendarId == 0)
            throw new InvalidOperationException($"Calendar named '{name}' not found in GetCalendars response: {getJson}");

        using var sharedsRequest = admin.BuildJsonPostRequest("/Calendar/GetCalendarShareds");
        await _client.SendAsync(sharedsRequest, TestContext.Current.CancellationToken);

        return calendarId;
    }

    // -- UpdateCalendarShared: role gating -------------------------------------------

    /// <summary>Update calendar shared user session is forbidden.</summary>
    [Fact]
    public async Task UpdateCalendarShared_UserSession_IsForbidden()
    {
        var user = await AuthenticatedSessionHelper.LoginAsync(factory, _client, "calendar-sharing-user@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        using var request = user.BuildJsonPostRequest("/Calendar/UpdateCalendarShared", new[] { new { CalendarId = 1, User = true, Anonymous = false } });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- UpdateCalendarShared: success path --------------------------------------------

    /// <summary>Update calendar shared existing calendar persists sharing flags.</summary>
    [Fact]
    public async Task UpdateCalendarShared_ExistingCalendar_PersistsSharingFlags()
    {
        var admin = await LoginAsAdminAsync();
        long calendarId = await CreateCalendarAndBackfillSharedAsync(admin, UniqueName());

        using var updateRequest = admin.BuildJsonPostRequest("/Calendar/UpdateCalendarShared",
            new[] { new { CalendarId = calendarId, User = true, Anonymous = true } });
        var response = await _client.SendAsync(updateRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var shared = db.CalendarShareds.Single(s => s.CalendarId == calendarId);
        Assert.True(shared.User);
        Assert.True(shared.Anonymous);
    }

    // -- UpdateOtherCalendar: role gating ----------------------------------------------

    /// <summary>Update other calendar anonymous session is forbidden.</summary>
    [Fact]
    public async Task UpdateOtherCalendar_AnonymousSession_IsForbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Calendar/UpdateOtherCalendar");
        request.Content = System.Net.Http.Json.JsonContent.Create(new[] { new { AccountEmail = "x@test.com", CalendarId = 1 } });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- UpdateOtherCalendar: success path / validation --------------------------------

    /// <summary>Update other calendar shared calendar persists subscription.</summary>
    [Fact]
    public async Task UpdateOtherCalendar_SharedCalendar_PersistsSubscription()
    {
        var admin = await LoginAsAdminAsync();
        long calendarId = await CreateCalendarAndBackfillSharedAsync(admin, UniqueName());
        using var shareRequest = admin.BuildJsonPostRequest("/Calendar/UpdateCalendarShared",
            new[] { new { CalendarId = calendarId, User = true, Anonymous = false } });
        await _client.SendAsync(shareRequest, TestContext.Current.CancellationToken);

        const string subscriberEmail = "calendar-sharing-subscriber1@test.com";
        var subscriberClient = factory.CreateTestClient(followRedirects: false);
        var subscriber = await AuthenticatedSessionHelper.LoginAsync(factory, subscriberClient, subscriberEmail, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

        using var subscribeRequest = subscriber.BuildJsonPostRequest("/Calendar/UpdateOtherCalendar",
            new[] { new { AccountEmail = subscriberEmail, CalendarId = calendarId } });
        var response = await subscriberClient.SendAsync(subscribeRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(db.OtherCalendars.Any(o => o.AccountEmail == subscriberEmail && o.CalendarId == calendarId));
    }

    /// <summary>Update other calendar not shared to users returns failure result.</summary>
    [Fact]
    public async Task UpdateOtherCalendar_NotSharedToUsers_ReturnsFailureResult()
    {
        var admin = await LoginAsAdminAsync();
        // Backfilled but never explicitly shared, so CalendarShared.User remains false (the
        // default CalendarShared.CreatePrivate leaves it in) — not a shareable calendar.
        long calendarId = await CreateCalendarAndBackfillSharedAsync(admin, UniqueName());

        const string subscriberEmail = "calendar-sharing-subscriber2@test.com";
        var subscriberClient = factory.CreateTestClient(followRedirects: false);
        var subscriber = await AuthenticatedSessionHelper.LoginAsync(factory, subscriberClient, subscriberEmail, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

        using var subscribeRequest = subscriber.BuildJsonPostRequest("/Calendar/UpdateOtherCalendar",
            new[] { new { AccountEmail = subscriberEmail, CalendarId = calendarId } });
        var response = await subscriberClient.SendAsync(subscribeRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(db.OtherCalendars.Any(o => o.AccountEmail == subscriberEmail && o.CalendarId == calendarId));
    }
}

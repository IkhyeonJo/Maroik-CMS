using System.Text.Json;
using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrmCalendar = Maroik.Core.PostgreSQL.Models.Calendar;
using OrmCalendarEvent = Maroik.Core.PostgreSQL.Models.CalendarEvent;
using OrmCalendarShared = Maroik.Core.PostgreSQL.Models.CalendarShared;
using OrmOtherCalendar = Maroik.Core.PostgreSQL.Models.OtherCalendar;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for who can see which calendar data in <c>CalendarController</c>: the first-visit
/// default-calendar provisioning of <c>AdminIndex</c>/<c>UserIndex</c>, and — most importantly — that
/// <c>GetCalendarEvents</c> / <c>GetOtherCalendars</c> never hand one account another account's private
/// calendar (a tampered request body naming someone else's calendar id must not leak their events).
/// Data is seeded straight into PostgreSQL (each test uses its own e-mails/titles), then read back
/// through the real HTTP pipeline.
/// </summary>
[Collection("Website Integration")]
public class CalendarControllerVisibilityTests(MaroikWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);
    
    private Task<AuthenticatedSession> LoginAsync(string email, string role = Role.User) =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "UserPassword1!", role, TestContext.Current.CancellationToken);

    private async Task<long> SeedCalendarAsync(string ownerEmail, string name, string? eventTitle = null, bool sharedToUsers = false, bool sharedToAnonymous = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var calendar = new OrmCalendar
        {
            AccountEmail = ownerEmail, Name = name, TimeZoneIanaId = "UTC", HtmlColorCode = "#3788d8",
            Created = DateTime.UtcNow, Updated = DateTime.UtcNow
        };
        db.Calendars.Add(calendar);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        if (eventTitle != null)
        {
            db.CalendarEvents.Add(new OrmCalendarEvent
            {
                CalendarId = calendar.Id, Title = eventTitle, AllDay = false,
                StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddHours(1), Status = "Busy",
                Created = DateTime.UtcNow, Updated = DateTime.UtcNow
            });
        }
        db.CalendarShareds.Add(new OrmCalendarShared { CalendarId = calendar.Id, User = sharedToUsers, Anonymous = sharedToAnonymous });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return calendar.Id;
    }

    private async Task SubscribeAsync(string subscriberEmail, long calendarId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.OtherCalendars.Add(new OrmOtherCalendar { AccountEmail = subscriberEmail, CalendarId = calendarId });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<string> PostJsonAsync(AuthenticatedSession session, string url, object? body = null)
    {
        using var request = session.BuildJsonPostRequest(url, body);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static object CalendarsPayload(params long[] ids) => new { Calendars = ids.Select(id => new { Id = id, Name = "x", Description = "", TimeZoneIanaId = "UTC", HtmlColorCode = "#3788d8" }).ToArray() };

    // -- GetCalendarEvents ---------------------------------------------------------------------

    /// <summary>The owner gets their own calendar's events.</summary>
    [Fact]
    public async Task GetCalendarEvents_ReturnsTheOwnersEvents()
    {
        const string owner = "cal-vis-owner1@test.com";
        var session = await LoginAsync(owner);
        long calendarId = await SeedCalendarAsync(owner, "Mine-1", eventTitle: "OwnerEvent-1");

        string json = await PostJsonAsync(session, "/Calendar/GetCalendarEvents", CalendarsPayload(calendarId));

        Assert.Contains("\"result\":true", json);
        Assert.Contains("OwnerEvent-1", json);
    }

    /// <summary>
    /// A tampered request that names someone else's PRIVATE calendar id gets nothing back: the events are
    /// neither returned nor even hinted at (same "Input is invalid" answer as an unknown calendar).
    /// </summary>
    [Fact]
    public async Task GetCalendarEvents_DoesNotLeakAnotherUsersPrivateCalendar_WhenItsIdIsSupplied()
    {
        const string victim = "cal-vis-victim2@test.com";
        const string attacker = "cal-vis-attacker2@test.com";
        await LoginAsync(victim);
        var attackerSession = await LoginAsync(attacker);
        long victimCalendar = await SeedCalendarAsync(victim, "Private-2", eventTitle: "VictimSecretEvent-2");

        string json = await PostJsonAsync(attackerSession, "/Calendar/GetCalendarEvents", CalendarsPayload(victimCalendar));

        Assert.Contains("\"result\":false", json);
        Assert.DoesNotContain("VictimSecretEvent-2", json);
    }

    /// <summary>Only the authorized subset of a mixed request is answered: the caller's own events come back, the foreign private ones do not.</summary>
    [Fact]
    public async Task GetCalendarEvents_AnswersOnlyTheAuthorizedCalendars_OfAMixedRequest()
    {
        const string victim = "cal-vis-victim3@test.com";
        const string caller = "cal-vis-caller3@test.com";
        await LoginAsync(victim);
        var session = await LoginAsync(caller);
        long victimCalendar = await SeedCalendarAsync(victim, "Private-3", eventTitle: "VictimSecretEvent-3");
        long ownCalendar = await SeedCalendarAsync(caller, "Own-3", eventTitle: "OwnEvent-3");

        string json = await PostJsonAsync(session, "/Calendar/GetCalendarEvents", CalendarsPayload(victimCalendar, ownCalendar));

        Assert.Contains("OwnEvent-3", json);
        Assert.DoesNotContain("VictimSecretEvent-3", json);
    }

    /// <summary>A calendar another user shared with users is visible only after subscribing to it (and while it stays shared).</summary>
    [Fact]
    public async Task GetCalendarEvents_ShowsASharedCalendar_OnlyToItsSubscribers()
    {
        const string owner = "cal-vis-owner4@test.com";
        const string subscriber = "cal-vis-subscriber4@test.com";
        const string stranger = "cal-vis-stranger4@test.com";
        await LoginAsync(owner);
        var subscriberSession = await LoginAsync(subscriber);
        var strangerSession = await LoginAsync(stranger);
        long shared = await SeedCalendarAsync(owner, "Shared-4", eventTitle: "SharedEvent-4", sharedToUsers: true);
        await SubscribeAsync(subscriber, shared);

        string subscriberJson = await PostJsonAsync(subscriberSession, "/Calendar/GetCalendarEvents", CalendarsPayload(shared));
        string strangerJson = await PostJsonAsync(strangerSession, "/Calendar/GetCalendarEvents", CalendarsPayload(shared));

        Assert.Contains("SharedEvent-4", subscriberJson);
        Assert.DoesNotContain("SharedEvent-4", strangerJson);
        Assert.Contains("\"result\":false", strangerJson);
    }

    /// <summary>Un-sharing a calendar hides it from a subscriber immediately, even though the stale subscription row still exists.</summary>
    [Fact]
    public async Task GetCalendarEvents_HidesACalendarThatStoppedBeingShared_EvenForAStaleSubscriber()
    {
        const string owner = "cal-vis-owner5@test.com";
        const string subscriber = "cal-vis-subscriber5@test.com";
        await LoginAsync(owner);
        var subscriberSession = await LoginAsync(subscriber);
        long calendar = await SeedCalendarAsync(owner, "WasShared-5", eventTitle: "WasSharedEvent-5", sharedToUsers: false);
        await SubscribeAsync(subscriber, calendar);

        string json = await PostJsonAsync(subscriberSession, "/Calendar/GetCalendarEvents", CalendarsPayload(calendar));

        Assert.DoesNotContain("WasSharedEvent-5", json);
    }

    /// <summary>A body without a usable calendar list is refused with the generic error.</summary>
    [Fact]
    public async Task GetCalendarEvents_ReturnsTheGenericError_WhenNoCalendarIsRequested()
    {
        var session = await LoginAsync("cal-vis-empty6@test.com");

        string json = await PostJsonAsync(session, "/Calendar/GetCalendarEvents", CalendarsPayload());

        Assert.Contains("\"result\":false", json);
        Assert.Contains("Input is invalid", json);
    }

    // -- GetOtherCalendars -----------------------------------------------------------------------

    /// <summary>A user is offered exactly the calendars they subscribed to that are still shared with users.</summary>
    [Fact]
    public async Task GetOtherCalendars_ListsOnlySubscribedCalendarsThatAreStillShared()
    {
        const string owner = "cal-vis-owner7@test.com";
        const string subscriber = "cal-vis-subscriber7@test.com";
        await LoginAsync(owner);
        var session = await LoginAsync(subscriber);
        long stillShared = await SeedCalendarAsync(owner, "StillShared-7", sharedToUsers: true);
        long unshared = await SeedCalendarAsync(owner, "Unshared-7", sharedToUsers: false);
        long notSubscribed = await SeedCalendarAsync(owner, "NotSubscribed-7", sharedToUsers: true);
        await SubscribeAsync(subscriber, stillShared);
        await SubscribeAsync(subscriber, unshared);

        string json = await PostJsonAsync(session, "/Calendar/GetOtherCalendars");

        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("result").GetBoolean());
        string[] names = [.. doc.RootElement.GetProperty("tempOtherCalendars").EnumerateArray().Select(c => c.GetProperty("name").GetString()!)];
        Assert.Equal(["StillShared-7"], names);
        Assert.NotEqual(stillShared, notSubscribed);
        Assert.NotEqual(unshared, notSubscribed);
    }

    // -- IsCalendarExists / IsCalendarEventExists ---------------------------------------------------

    /// <summary>Looking up someone else's calendar id by hand answers "invalid" — the same as an unknown id.</summary>
    [Fact]
    public async Task IsCalendarExists_DoesNotRevealAnotherUsersCalendar()
    {
        const string victim = "cal-vis-victim8@test.com";
        await LoginAsync(victim);
        var session = await LoginAsync("cal-vis-caller8@test.com");
        long victimCalendar = await SeedCalendarAsync(victim, "Private-8");
        await SeedCalendarAsync("cal-vis-caller8@test.com", "Own-8");

        string json = await PostJsonAsync(session, $"/Calendar/IsCalendarExists?id={victimCalendar}");

        Assert.Contains("\"result\":false", json);
        Assert.DoesNotContain("Private-8", json);
    }

    /// <summary>An account with no calendar at all is told so (distinct from "not yours").</summary>
    [Fact]
    public async Task IsCalendarExists_ReportsNoCalendar_WhenTheAccountHasNone()
    {
        var session = await LoginAsync("cal-vis-nocal9@test.com");

        string json = await PostJsonAsync(session, "/Calendar/IsCalendarExists?id=1");

        Assert.Contains("\"result\":false", json);
        Assert.Contains("No calendar exists", json);
    }

    /// <summary>An event detail lookup by id only resolves events on the caller's own calendars.</summary>
    [Fact]
    public async Task IsCalendarEventExists_DoesNotRevealAnotherUsersEvent()
    {
        const string victim = "cal-vis-victim10@test.com";
        await LoginAsync(victim);
        var session = await LoginAsync("cal-vis-caller10@test.com");
        long victimCalendar = await SeedCalendarAsync(victim, "Private-10", eventTitle: "VictimSecretEvent-10");
        await SeedCalendarAsync("cal-vis-caller10@test.com", "Own-10");
        long victimEventId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            victimEventId = await db.CalendarEvents.Where(e => e.CalendarId == victimCalendar).Select(e => e.Id).SingleAsync(TestContext.Current.CancellationToken);
        }

        string json = await PostJsonAsync(session, $"/Calendar/IsCalendarEventExists?id={victimEventId}");

        Assert.Contains("\"result\":false", json);
        Assert.DoesNotContain("VictimSecretEvent-10", json);
    }

    // -- AdminIndex / UserIndex -----------------------------------------------------------------------

    /// <summary>
    /// A first visit provisions the default calendar (named after the nickname) so the grid is never empty;
    /// visiting again does not create a second one.
    /// </summary>
    [Theory]
    [InlineData(Role.User, "/Calendar/UserIndex", "cal-vis-firstvisit-user@test.com")]
    [InlineData(Role.Admin, "/Calendar/AdminIndex", "cal-vis-firstvisit-admin@test.com")]
    public async Task IndexPage_ProvisionsTheDefaultCalendarOnFirstVisit_AndOnlyOnce(string role, string url, string email)
    {
        var session = await LoginAsync(email, role);

        for (int visit = 0; visit < 2; visit++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Cookie", session.CookieHeader);
            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        List<string> names = await db.Calendars.AsNoTracking().Where(c => c.AccountEmail == email).Select(c => c.Name).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal([session.Nickname], names);
    }

    /// <summary>The user calendar page lists the shared calendars the user subscribed to alongside their own.</summary>
    [Fact]
    public async Task UserIndex_RendersSubscribedSharedCalendarNames()
    {
        const string owner = "cal-vis-owner11@test.com";
        const string subscriber = "cal-vis-subscriber11@test.com";
        await LoginAsync(owner);
        var session = await LoginAsync(subscriber);
        long shared = await SeedCalendarAsync(owner, "TeamCalendar-11", sharedToUsers: true);
        await SubscribeAsync(subscriber, shared);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/Calendar/UserIndex");
        request.Headers.Add("Cookie", session.CookieHeader);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("TeamCalendar-11", html);
    }

    // -- UploadImageFile: pre-upload validation ---------------------------------------------------------

    private async Task<string> UploadAsync(AuthenticatedSession session, string fileName, byte[] bytes)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(bytes), "summernoteImageFile", fileName } };
        using var request = session.BuildFormPostRequest("/Calendar/UploadImageFile", form);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>An empty upload is refused with the size message (no storage call).</summary>
    [Fact]
    public async Task UploadImageFile_RefusesAnEmptyFile()
    {
        var session = await LoginAsync("cal-vis-upload12@test.com");

        string json = await UploadAsync(session, "empty.png", []);

        Assert.Contains("\"result\":false", json);
        Assert.Contains("File Size must be smaller than", json);
    }

    /// <summary>Only .jpg/.jpeg/.png are accepted; anything else (here a .gif, or a double extension) is refused before any decoding or storage.</summary>
    [Theory]
    [InlineData("anim.gif")]
    [InlineData("vector.svg")]
    [InlineData("shell.png.exe")]
    public async Task UploadImageFile_RefusesADisallowedExtension(string fileName)
    {
        var session = await LoginAsync("cal-vis-upload13@test.com");

        string json = await UploadAsync(session, fileName, [1, 2, 3]);

        Assert.Contains("\"result\":false", json);
        Assert.Contains("Only .jpg or jpeg or .png file allowed.", json);
    }

    /// <summary>Bytes that merely claim to be a PNG are refused by content validation, and nothing is stored.</summary>
    [Fact]
    public async Task UploadImageFile_RefusesBytesThatAreNotARealImage()
    {
        var session = await LoginAsync("cal-vis-upload14@test.com");

        string json = await UploadAsync(session, "fake.png", [
            .. "this is not an image"u8
        ]);

        Assert.Contains("\"result\":false", json);
        Assert.DoesNotContain("filePath", json);
    }

    // -- Model validation ---------------------------------------------------------------------------------

    /// <summary>A malformed JSON body on create is rejected, not turned into an exception page.</summary>
    [Fact]
    public async Task CreateCalendar_RejectsAnInvalidBody()
    {
        var session = await LoginAsync("cal-vis-badbody15@test.com");
        using var request = session.BuildJsonPostRequest("/Calendar/CreateCalendar");
        request.Content = new StringContent("{ \"Calendars\": \"not-a-list\" }", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
    }
}

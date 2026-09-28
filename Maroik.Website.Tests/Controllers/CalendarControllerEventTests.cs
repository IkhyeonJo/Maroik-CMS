using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for the CalendarEvent create/update/delete action group of
/// <c>CalendarController</c> — the group <c>CalendarControllerCalendarTests</c> and
/// <c>CalendarControllerReadOnlyTests</c> left untested, since <c>CalendarService</c>'s
/// CalendarEvent methods wrap <c>IUnitOfWork</c> transactions the EF Core InMemory provider
/// couldn't execute (see <c>AccountBookControllerAssetTests</c> for the same limitation
/// elsewhere). Runs against a real PostgreSQL instance (via
/// <see cref="MaroikWebApplicationFactory"/>'s Testcontainers setup), so those transactions are
/// exercised for real here.
/// </summary>
[Collection("Website Integration")]
public class CalendarControllerEventTests(MaroikWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    private Task<AuthenticatedSession> LoginAsUserAsync(string email = "calendar-event-user@test.com") =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

    private async Task<long> CreateOwnCalendarAsync(AuthenticatedSession session, string name)
    {
        var payload = new
        {
            Calendars = new[] { new { Id = 0, Name = name, Description = "", TimeZoneIanaId = "UTC", HtmlColorCode = "#3788d8" } }
        };
        using var createRequest = session.BuildJsonPostRequest("/Calendar/CreateCalendar", payload);
        await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);

        using var getRequest = session.BuildJsonPostRequest("/Calendar/GetCalendars");
        var getResponse = await _client.SendAsync(getRequest, TestContext.Current.CancellationToken);
        string getJson = await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var doc = System.Text.Json.JsonDocument.Parse(getJson);
        foreach (var calendar in doc.RootElement.GetProperty("calendars").EnumerateArray().Where(calendar => calendar.GetProperty("name").GetString() == name))
        {
            return calendar.GetProperty("id").GetInt64();
        }
        throw new InvalidOperationException($"Calendar named '{name}' not found in GetCalendars response: {getJson}");
    }

    private static MultipartFormDataContent EventForm(long calendarId, string title, string startDate = "2030-06-01", string endDate = "2030-06-02", long id = 0)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(id.ToString()), "Id" },
            { new StringContent(calendarId.ToString()), "CalendarId" },
            { new StringContent(title), "Title" },
            { new StringContent("true"), "AllDay" },
            { new StringContent(startDate), "StartDate" },
            { new StringContent(endDate), "EndDate" },
            { new StringContent("Somewhere"), "Location" },
            // "Status" carries a real DB CHECK constraint restricting it to "Busy"/"Free" —
            // anything else (e.g. "Confirmed") is silently rejected by CreateCalendarEventAsync's
            // catch-all as "Input is invalid".
            { new StringContent("Busy"), "Status" }
        };
        return form;
    }

    private static string UniqueTitle([System.Runtime.CompilerServices.CallerMemberName] string testName = "") =>
        $"{testName}-{Guid.NewGuid():N}";

    // -- CreateCalendarEvent: role gating ------------------------------------------

    /// <summary>Create calendar event anonymous session is forbidden.</summary>
    [Fact]
    public async Task CreateCalendarEvent_AnonymousSession_IsForbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Calendar/CreateCalendarEvent");
        request.Content = EventForm(1, "Blocked");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- CreateCalendarEvent: validation --------------------------------------------

    /// <summary>Create calendar event unowned calendar id returns failure result.</summary>
    [Fact]
    public async Task CreateCalendarEvent_UnownedCalendarId_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync();
        using var request = session.BuildFormPostRequest("/Calendar/CreateCalendarEvent", EventForm(999999, UniqueTitle()));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    // -- CreateCalendarEvent / IsCalendarEventExists: success path ------------------

    /// <summary>Create calendar event then is calendar event exists finds the new event.</summary>
    [Fact]
    public async Task CreateCalendarEvent_ThenIsCalendarEventExists_FindsTheNewEvent()
    {
        var session = await LoginAsUserAsync();
        long calendarId = await CreateOwnCalendarAsync(session, UniqueTitle());
        string title = UniqueTitle();

        using var createRequest = session.BuildFormPostRequest("/Calendar/CreateCalendarEvent", EventForm(calendarId, title));
        var createResponse = await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);
        string createJson = await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"result\":true", createJson);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.CalendarEvents.Single(e => e.CalendarId == calendarId && e.Title == title);

        using var existsRequest = session.BuildJsonPostRequest($"/Calendar/IsCalendarEventExists?id={created.Id}");
        var existsResponse = await _client.SendAsync(existsRequest, TestContext.Current.CancellationToken);
        string existsJson = await existsResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", existsJson);
    }

    /// <summary>Is calendar event exists unknown id returns failure result.</summary>
    [Fact]
    public async Task IsCalendarEventExists_UnknownId_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync();
        using var request = session.BuildJsonPostRequest("/Calendar/IsCalendarEventExists?id=999999");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    // -- UpdateCalendarEvent ---------------------------------------------------------

    /// <summary>Update calendar event own event returns success result.</summary>
    [Fact]
    public async Task UpdateCalendarEvent_OwnEvent_ReturnsSuccessResult()
    {
        var session = await LoginAsUserAsync();
        long calendarId = await CreateOwnCalendarAsync(session, UniqueTitle());
        string title = UniqueTitle();
        using var createRequest = session.BuildFormPostRequest("/Calendar/CreateCalendarEvent", EventForm(calendarId, title));
        await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.CalendarEvents.Single(e => e.CalendarId == calendarId && e.Title == title);

        string updatedTitle = UniqueTitle();
        using var updateRequest = session.BuildFormPostRequest("/Calendar/UpdateCalendarEvent",
            EventForm(calendarId, updatedTitle, startDate: "2030-07-01", endDate: "2030-07-02", id: created.Id));
        var response = await _client.SendAsync(updateRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updated = verifyDb.CalendarEvents.Single(e => e.Id == created.Id);
        Assert.Equal(updatedTitle, updated.Title);
        Assert.Equal(new DateTime(2030, 7, 1, 0, 0, 0, DateTimeKind.Utc), updated.StartDate);
    }

    /// <summary>Update calendar event unknown id returns failure result.</summary>
    [Fact]
    public async Task UpdateCalendarEvent_UnknownId_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync();
        long calendarId = await CreateOwnCalendarAsync(session, UniqueTitle());
        using var request = session.BuildFormPostRequest("/Calendar/UpdateCalendarEvent", EventForm(calendarId, UniqueTitle(), id: 999999));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    /// <summary>Update calendar event not owned by caller returns failure result.</summary>
    [Fact]
    public async Task UpdateCalendarEvent_NotOwnedByCaller_ReturnsFailureResult()
    {
        var owner = await LoginAsUserAsync("calendar-event-owner@test.com");
        long ownerCalendarId = await CreateOwnCalendarAsync(owner, UniqueTitle());
        string title = UniqueTitle();
        using var createRequest = owner.BuildFormPostRequest("/Calendar/CreateCalendarEvent", EventForm(ownerCalendarId, title));
        await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.CalendarEvents.Single(e => e.CalendarId == ownerCalendarId && e.Title == title);

        var otherClient = factory.CreateTestClient(followRedirects: false);
        var other = await AuthenticatedSessionHelper.LoginAsync(factory, otherClient, "calendar-event-other@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        long otherCalendarId = await CreateOwnCalendarAsync(other, UniqueTitle());

        using var updateRequest = other.BuildFormPostRequest("/Calendar/UpdateCalendarEvent", EventForm(otherCalendarId, UniqueTitle(), id: created.Id));
        var response = await otherClient.SendAsync(updateRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    // -- DeleteCalendarEvent -----------------------------------------------------------

    /// <summary>Delete calendar event own event returns success result.</summary>
    [Fact]
    public async Task DeleteCalendarEvent_OwnEvent_ReturnsSuccessResult()
    {
        var session = await LoginAsUserAsync();
        long calendarId = await CreateOwnCalendarAsync(session, UniqueTitle());
        string title = UniqueTitle();
        using var createRequest = session.BuildFormPostRequest("/Calendar/CreateCalendarEvent", EventForm(calendarId, title));
        await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.CalendarEvents.Single(e => e.CalendarId == calendarId && e.Title == title);

        using var deleteRequest = session.BuildJsonPostRequest($"/Calendar/DeleteCalendarEvent?id={created.Id}");
        var response = await _client.SendAsync(deleteRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(verifyDb.CalendarEvents.Any(e => e.Id == created.Id));
    }

    /// <summary>Delete calendar event unknown id returns failure result.</summary>
    [Fact]
    public async Task DeleteCalendarEvent_UnknownId_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync();
        using var request = session.BuildJsonPostRequest("/Calendar/DeleteCalendarEvent?id=999999");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }
}

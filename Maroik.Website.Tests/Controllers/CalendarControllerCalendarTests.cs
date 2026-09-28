using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for the Calendar (not CalendarEvent) CRUD action group of
/// <c>CalendarController</c> — <c>CreateCalendar</c>/<c>IsCalendarExists</c>/
/// <c>UpdateCalendar</c>/<c>DeleteCalendar</c>, all <c>[RequiredHttpPostAccess]</c>-gated for
/// both Admin and User. See <c>CalendarControllerEventTests</c> and
/// <c>CalendarControllerSharingTests</c> for the CalendarEvent/OtherCalendar/CalendarShared
/// action groups, which wrap <c>IUnitOfWork</c> transactions.
/// </summary>
[Collection("Website Integration")]
public class CalendarControllerCalendarTests(MaroikWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    private Task<AuthenticatedSession> LoginAsUserAsync(string email = "calendar-user@test.com") =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

    private static object ValidCalendarPayload(string name) => new
    {
        Calendars = new[]
        {
            new { Id = 0, Name = name, Description = "", TimeZoneIanaId = "UTC", HtmlColorCode = "#3788d8" }
        }
    };

    // -- CreateCalendar: role gating ----------------------------------------------

    /// <summary>Create calendar anonymous session is forbidden.</summary>
    [Fact]
    public async Task CreateCalendar_AnonymousSession_IsForbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Calendar/CreateCalendar");
        request.Content = System.Net.Http.Json.JsonContent.Create(ValidCalendarPayload("Blocked"));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- CreateCalendar / IsCalendarExists: success path --------------------------

    /// <summary>Create calendar then is calendar exists finds the new calendar.</summary>
    [Fact]
    public async Task CreateCalendar_ThenIsCalendarExists_FindsTheNewCalendar()
    {
        var session = await LoginAsUserAsync();
        using var createRequest = session.BuildJsonPostRequest("/Calendar/CreateCalendar", ValidCalendarPayload("Work"));
        var createResponse = await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);
        string createJson = await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"result\":true", createJson);

        using var getRequest = session.BuildJsonPostRequest("/Calendar/GetCalendars");
        var getResponse = await _client.SendAsync(getRequest, TestContext.Current.CancellationToken);
        string getJson = await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Work", getJson);
    }

    /// <summary>
    /// Regression: the response echoes the new calendar with its REAL id. It used to be 0, so the client
    /// built the new calendar's checkbox / edit / delete controls for "calendar 0" until the page was reloaded.
    /// </summary>
    [Fact]
    public async Task CreateCalendar_ResponseCarriesTheRealIdOfTheNewCalendar()
    {
        var session = await LoginAsUserAsync();
        using var createRequest = session.BuildJsonPostRequest("/Calendar/CreateCalendar", ValidCalendarPayload("WithId"));
        var createResponse = await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);
        using var doc = System.Text.Json.JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        long returnedId = doc.RootElement.GetProperty("calendar").GetProperty("id").GetInt64();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        long storedId = db.Calendars.Single(c => c.AccountEmail == "calendar-user@test.com" && c.Name == "WithId").Id;

        Assert.True(returnedId > 0);
        Assert.Equal(storedId, returnedId);
    }

    /// <summary>Create calendar duplicate name returns already exists error.</summary>
    [Fact]
    public async Task CreateCalendar_DuplicateName_ReturnsAlreadyExistsError()
    {
        var session = await LoginAsUserAsync();
        using var firstRequest = session.BuildJsonPostRequest("/Calendar/CreateCalendar", ValidCalendarPayload("Duplicate"));
        await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);

        using var secondRequest = session.BuildJsonPostRequest("/Calendar/CreateCalendar", ValidCalendarPayload("Duplicate"));
        var response = await _client.SendAsync(secondRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("The calendar already exists.", json);
    }

    /// <summary>Is calendar exists unknown id returns failure result.</summary>
    [Fact]
    public async Task IsCalendarExists_UnknownId_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync();
        using var request = session.BuildJsonPostRequest("/Calendar/IsCalendarExists?id=999999");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    // -- UpdateCalendar -------------------------------------------------------------

    /// <summary>Update calendar existing calendar returns success result.</summary>
    [Fact]
    public async Task UpdateCalendar_ExistingCalendar_ReturnsSuccessResult()
    {
        var session = await LoginAsUserAsync();
        using var createRequest = session.BuildJsonPostRequest("/Calendar/CreateCalendar", ValidCalendarPayload("ToRename"));
        await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);
        long createdId = await FindCalendarIdByNameAsync(session, "ToRename");

        var updatePayload = new
        {
            Calendars = new[]
            {
                new { Id = createdId, Name = "Renamed", Description = "updated", TimeZoneIanaId = "Asia/Seoul", HtmlColorCode = "#abcdef" }
            }
        };
        using var updateRequest = session.BuildJsonPostRequest("/Calendar/UpdateCalendar", updatePayload);
        var response = await _client.SendAsync(updateRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
    }

    /// <summary>Update calendar unknown id returns failure result.</summary>
    [Fact]
    public async Task UpdateCalendar_UnknownId_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync();
        var payload = new
        {
            Calendars = new[]
            {
                new { Id = 999999, Name = "Ghost", Description = "", TimeZoneIanaId = "UTC", HtmlColorCode = "#000000" }
            }
        };
        using var request = session.BuildJsonPostRequest("/Calendar/UpdateCalendar", payload);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    // -- DeleteCalendar ---------------------------------------------------------------

    /// <summary>Delete calendar existing calendar returns success result.</summary>
    [Fact]
    public async Task DeleteCalendar_ExistingCalendar_ReturnsSuccessResult()
    {
        var session = await LoginAsUserAsync();
        using var createRequest = session.BuildJsonPostRequest("/Calendar/CreateCalendar", ValidCalendarPayload("ToDelete"));
        await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);
        long createdId = await FindCalendarIdByNameAsync(session, "ToDelete");

        var deletePayload = new { Calendars = new[] { new { Id = createdId, Name = "ToDelete", Description = "", TimeZoneIanaId = "UTC", HtmlColorCode = "#3788d8" } } };
        using var deleteRequest = session.BuildJsonPostRequest("/Calendar/DeleteCalendar", deletePayload);
        var response = await _client.SendAsync(deleteRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
    }

    /// <summary>Delete calendar unknown id returns failure result.</summary>
    [Fact]
    public async Task DeleteCalendar_UnknownId_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync();
        var payload = new { Calendars = new[] { new { Id = 999999, Name = "Ghost", Description = "", TimeZoneIanaId = "UTC", HtmlColorCode = "#000000" } } };
        using var request = session.BuildJsonPostRequest("/Calendar/DeleteCalendar", payload);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    /// <summary>
    /// CreateCalendar's JSON response echoes back the client-submitted request object (still
    /// carrying <c>Id = 0</c>), not the newly-persisted entity, so the real database ID has to
    /// be looked up via <c>GetCalendars</c> instead.
    /// </summary>
    private async Task<long> FindCalendarIdByNameAsync(AuthenticatedSession session, string name)
    {
        using var request = session.BuildJsonPostRequest("/Calendar/GetCalendars");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        foreach (var calendar in doc.RootElement.GetProperty("calendars").EnumerateArray().Where(calendar => calendar.GetProperty("name").GetString() == name))
        {
            return calendar.GetProperty("id").GetInt64();
        }

        throw new InvalidOperationException($"Calendar named '{name}' not found in GetCalendars response: {json}");
    }
}

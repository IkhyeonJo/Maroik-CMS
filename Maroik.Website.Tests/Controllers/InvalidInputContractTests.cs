using System.Net;
using System.Text.Json;
using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.Extensions.DependencyInjection;
using Maroik.Website.Tests.Infrastructure;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// The validation contract of the write endpoints: a request whose body fails model validation (or is rejected by the
/// service) gets HTTP 200 with <c>{ "result": false, "error": "Input is invalid" }</c> — no exception page, no partial write.
/// Runs against the real services and database.
/// </summary>
[Collection("Website Integration")]
public class InvalidInputContractTests(MaroikWebApplicationFactory factory)
{
    /// <summary>The generic message returned for input that fails validation.</summary>
    private const string Invalid = "Input is invalid";

    /// <summary>A form with a single irrelevant empty field, so every required field is missing.</summary>
    private static MultipartFormDataContent EmptyForm() => new() { { new StringContent(""), "x" } };

    /// <summary>Each row: the role that may call the endpoint, the endpoint, whether it takes a form, the body to send, and the expected message.</summary>
    private static readonly (string Role, string Url, bool Form, object? Body, string Message)[] _endpoints =
    [
        (Role.Admin, "/Management/CreateAccount", false, null, Invalid),
        (Role.Admin, "/Management/UpdateAccount", false, null, Invalid),
        (Role.Admin, "/Management/CreateCategory", false, null, Invalid),
        (Role.Admin, "/Management/CreateSubCategory", false, null, Invalid),
        (Role.Admin, "/Management/UpdateCategory", false, null, Invalid),
        (Role.Admin, "/Management/UpdateSubCategory", false, null, Invalid),
        (Role.Admin, "/Management/DeleteCategory", false, null, Invalid),
        (Role.Admin, "/Management/DeleteSubCategory", false, null, Invalid),
        (Role.Admin, "/Management/EditPrivateNoteBoard", true, null, Invalid),
        (Role.User, "/Calendar/CreateCalendarEvent", true, null, Invalid),
        (Role.User, "/Calendar/UpdateCalendarEvent", true, null, Invalid),
        (Role.User, "/Calendar/GetCalendarEvents", false, null, Invalid),
        (Role.User, "/Notice/CreateFixedIncome", false, null, Invalid),
        (Role.User, "/Notice/UpdateFixedIncome", false, null, Invalid),
        (Role.User, "/Notice/CreateFixedExpenditure", false, null, Invalid),
        (Role.User, "/Notice/UpdateFixedExpenditure", false, null, Invalid),
        (Role.User, "/AccountBook/UpdateAsset", false, null, Invalid),
        (Role.User, "/AccountBook/UpdateIncome", false, null, Invalid),
        (Role.User, "/AccountBook/UpdateExpenditure", false, null, Invalid),

        // a body of the wrong shape fails model binding
        (Role.Admin, "/Management/WritePrivateNoteComment", false, new { BoardId = "abc" }, Invalid),
        (Role.User, "/Forum/WriteFreeComment", false, new { BoardId = "abc" }, Invalid),
        (Role.User, "/Calendar/UpdateCalendar", false, new { Calendars = "abc" }, Invalid),
        (Role.User, "/Calendar/GetCalendarEvents", false, new { Calendars = "abc" }, Invalid),
        (Role.Admin, "/Calendar/UpdateCalendarShared", false, new { CalendarId = "abc" }, Invalid),
        (Role.User, "/Calendar/UpdateOtherCalendar", false, new { CalendarId = "abc" }, Invalid),
        // a well-formed but empty body gets the endpoint's own, more specific message
        (Role.Admin, "/Management/WritePrivateNoteComment", false, null, "Please enter a comment."),
        (Role.User, "/Forum/WriteFreeComment", false, null, "Please enter a comment."),
        (Role.User, "/Calendar/UpdateCalendar", false, null, "The calendar could not be found."),
    ];

    /// <summary>A body that is empty or of the wrong shape is refused — with the generic message unless the endpoint has a more specific one — by every write endpoint listed.</summary>
    [Fact]
    public async Task AnEmptyOrMalformedBody_IsRefused_ByEveryListedWriteEndpoint()
    {
        var client = factory.CreateTestClient();
        var sessions = new Dictionary<string, AuthenticatedSession>();
        foreach (string role in new[] { Role.User, Role.Admin })
            sessions[role] = await AuthenticatedSessionHelper.LoginAsync(factory, client, $"invalid-input-{role.ToLowerInvariant()}@test.com", "UserPassword1!", role, TestContext.Current.CancellationToken);

        List<string> problems = [];
        foreach (var (role, url, form, body, message) in _endpoints)
        {
            using var request = form
                ? sessions[role].BuildFormPostRequest(url, EmptyForm())
                : sessions[role].BuildJsonPostRequest(url, body ?? new { });
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            string text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            if (response.StatusCode != HttpStatusCode.OK) { problems.Add($"{url}: HTTP {(int)response.StatusCode} {text[..Math.Min(text.Length, 100)]}"); continue; }
            using JsonDocument doc = JsonDocument.Parse(text);
            if (doc.RootElement.GetProperty("result").GetBoolean() || !text.Contains(message)) problems.Add($"{url}: {text[..Math.Min(text.Length, 140)]}");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>The id of the category named <paramref name="name"/>.</summary>
    private long CategoryIdByName(string name)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Categories.Single(c => c.Name == name).Id;
    }

    /// <summary>A category JSON body (id 0 creates).</summary>
    private static object Category(string name, long id = 0) => new
    {
        Id = id, Name = name, DisplayName = name, IconPath = "/icons/test.png", Controller = "Notice", Action = "", Role = Role.User, Order = 500
    };

    /// <summary>A sub-category JSON body under <paramref name="categoryId"/> (id 0 creates).</summary>
    private static object SubCategory(string name, long categoryId, long id = 0) => new
    {
        Id = id, CategoryId = categoryId, Name = name, DisplayName = name, IconPath = "/icons/test.png", Controller = "", Action = "Index", Role = Role.User, Order = 500
    };

    /// <summary>
    /// A menu write the domain refuses (an over-long name) answers with that rule; one only the database refuses
    /// (a sub-category of a category that is not there) answers with the temporary-error message.
    /// </summary>
    [Fact]
    public async Task MenuWrites_TheDatabaseRefuses_AreAnsweredWithTheGenericMessage()
    {
        var client = factory.CreateTestClient();
        var admin = await AuthenticatedSessionHelper.LoginAsync(factory, client, "invalid-input-menu-admin@test.com", "AdminPassword1!", Role.Admin, TestContext.Current.CancellationToken);
        string name = $"Refused-{Guid.NewGuid():N}";

        string created = await Post("/Management/CreateCategory", Category(name));
        Assert.Contains("\"result\":true", created);
        long categoryId = CategoryIdByName(name);
        Assert.Contains("\"result\":true", await Post("/Management/CreateSubCategory", SubCategory(name + "-sub", categoryId)));

        string createTooLong = await Post("/Management/CreateCategory", Category(new string('n', 5000)));
        string createSubOfMissing = await Post("/Management/CreateSubCategory", SubCategory(name, 987654321));

        Assert.False(JsonDocument.Parse(createTooLong).RootElement.GetProperty("result").GetBoolean());
        Assert.Equal("'Name' must be 255 characters or fewer.", JsonDocument.Parse(createTooLong).RootElement.GetProperty("error").GetString());
        Assert.False(JsonDocument.Parse(createSubOfMissing).RootElement.GetProperty("result").GetBoolean());
        Assert.Equal("A temporary error occurred. Please try again later.", JsonDocument.Parse(createSubOfMissing).RootElement.GetProperty("error").GetString());
        return;

        // Posts body as JSON as the admin, asserts 200, and returns the response body.
        async Task<string> Post(string url, object body)
        {
            using var request = admin.BuildJsonPostRequest(url, body);
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>The admin exports return real .xlsx workbooks named after the requested file.</summary>
    [Theory]
    [InlineData("/Management/ExportExcelAccount")]
    [InlineData("/Management/ExportExcelMenu")]
    public async Task AdminExportExcel_ReturnsAnXlsxWorkbook_NamedAfterTheRequestedFile(string url)
    {
        var client = factory.CreateTestClient();
        var admin = await AuthenticatedSessionHelper.LoginAsync(factory, client, $"invalid-input-export{url.Replace("/", "-").ToLowerInvariant()}@test.com", "AdminPassword1!", Role.Admin, TestContext.Current.CancellationToken);
        using var request = admin.BuildJsonPostRequest($"{url}?fileName=admin report");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        string? fileName = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        Assert.StartsWith("admin report-", fileName);
        Assert.EndsWith(".xlsx", fileName);
        Assert.True(bytes.Length > 4 && bytes[0] == 'P' && bytes[1] == 'K', "an .xlsx file is a zip container starting with 'PK'");
    }

    /// <summary>A culture the site does not support is refused and never written to the cookie.</summary>
    [Fact]
    public async Task CultureManagement_RefusesAnUnsupportedCulture_AndSetsNoCookie()
    {
        var client = factory.CreateTestClient();
        var user = await AuthenticatedSessionHelper.LoginAsync(factory, client, "invalid-input-culture@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        using var request = user.BuildJsonPostRequest("/Dashboard/CultureManagement", new { Culture = "xx-NOPE" });

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"result\":false", body);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.Contains(".AspNetCore.Culture")));
    }

    /// <summary>A time-zone change without a time zone sends the user back to the profile page and changes nothing.</summary>
    [Fact]
    public async Task UpdateProfileTimeZone_WithoutATimeZone_RedirectsBackToTheProfile()
    {
        var client = factory.CreateTestClient();
        var user = await AuthenticatedSessionHelper.LoginAsync(factory, client, "invalid-input-tz@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        using var request = user.BuildFormPostRequest("/Management/UpdateProfileTimeZone", new MultipartFormDataContent { { new StringContent("x"), "Unrelated" } });

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/Management/Profile", response.Headers.Location?.ToString());
    }

    /// <summary>
    /// A request with no form fields at all (an empty multipart body) leaves the model unbound. Every form-posting endpoint must answer
    /// it as an invalid / empty request — never with a server error.
    /// </summary>
    [Fact]
    public async Task AnEmptyMultipartBody_NeverCausesAServerError()
    {
        var client = factory.CreateTestClient();
        var user = await AuthenticatedSessionHelper.LoginAsync(factory, client, "invalid-input-emptymulti-user@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        var admin = await AuthenticatedSessionHelper.LoginAsync(factory, client, "invalid-input-emptymulti-admin@test.com", "UserPassword1!", Role.Admin, TestContext.Current.CancellationToken);
        (AuthenticatedSession Session, string Url)[] endpoints =
        [
            (user, "/Management/UpdateProfileAvatar"), (user, "/Management/UpdateProfileTimeZone"),
            (user, "/Forum/WriteFreeBoard"), (user, "/Forum/EditFreeBoard"),
            (user, "/Management/WritePrivateNoteBoard"), (user, "/Management/EditPrivateNoteBoard"),
            (user, "/Calendar/CreateCalendarEvent"), (user, "/Calendar/UpdateCalendarEvent"),
            (admin, "/Forum/WriteFreeBoard"), (admin, "/Management/UpdateProfileAvatar"),
        ];

        List<string> problems = [];
        foreach (var (session, url) in endpoints)
        {
            using var request = session.BuildFormPostRequest(url, []);
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            if ((int)response.StatusCode >= 500) problems.Add($"{url}: HTTP {(int)response.StatusCode}");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}

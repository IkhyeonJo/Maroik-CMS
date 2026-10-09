using Maroik.Core.Domain.Account;
using Maroik.Website.Tests.Infrastructure;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for <c>DashboardController</c>. The anonymous/admin/user dashboard index
/// pages and the culture/monetary-unit update actions were previously covered only by an
/// anonymous-route smoke test.
/// </summary>
[Collection("Website Integration")]
public class DashboardControllerTests
{
    /// <summary>The shared test host.</summary>
    private readonly MaroikWebApplicationFactory _factory;
    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client;

    /// <summary>Initializes a new instance of DashboardControllerTests, sharing the MaroikWebApplicationFactory fixture across the tests in this class.</summary>
    public DashboardControllerTests(MaroikWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateTestClient(followRedirects: false);
    }

    /// <summary>Seeds (if missing) a User account for <paramref name="email"/> and signs in as it.</summary>
    private Task<AuthenticatedSession> LoginAsUserAsync(string email = "dashboard-user@test.com") =>
        AuthenticatedSessionHelper.LoginAsync(_factory, _client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

    /// <summary>Seeds (if missing) an Admin account for <paramref name="email"/> and signs in as it.</summary>
    private Task<AuthenticatedSession> LoginAsAdminAsync(string email = "dashboard-admin@test.com") =>
        AuthenticatedSessionHelper.LoginAsync(_factory, _client, email, "AdminPassword1!", Role.Admin, TestContext.Current.CancellationToken);

    // -- AnonymousIndex -------------------------------------------------------

    /// <summary>Verifies that <c>AnonymousIndex</c> get when returns200.</summary>
    [Fact]
    public async Task AnonymousIndex_Get_Returns200()
    {
        var response = await _client.GetAsync("/Dashboard/AnonymousIndex", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// The shared layout renders the generic "temporary error" text for the client scripts' runtime checks
    /// (<c>window.onReply</c> shows it when a reply does not have the expected shape), in the visitor's culture.
    /// </summary>
    [Theory]
    [InlineData("en-US", "A temporary error occurred. Please try again later.")]
    [InlineData("ko-KR", "일시적인 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.")]
    public async Task AnonymousIndex_Get_RendersTheTemporaryErrorTextForTheClientScripts(string culture, string text)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Dashboard/AnonymousIndex");
        request.Headers.Add("Cookie", $".AspNetCore.Culture=c={culture}|uic={culture}");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        // Razor's encoder writes non-ASCII text as character references, so compare the decoded markup.
        string html = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Contains($"<input id=\"_LocalizerTemporaryError\" type=\"hidden\" value=\"{text}\" />", html);
    }

    /// <summary>Verifies that <c>DefaultRoute</c> get when returns200.</summary>
    [Fact]
    public async Task DefaultRoute_Get_Returns200()
    {
        // The default route pattern is {controller=Dashboard}/{action=AnonymousIndex}/{id?}
        var response = await _client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Anonymous index logged-in user redirects to user index.</summary>
    [Fact]
    public async Task AnonymousIndex_LoggedInUser_RedirectsToUserIndex()
    {
        var session = await LoginAsUserAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Dashboard/AnonymousIndex");
        request.Headers.Add("Cookie", session.CookieHeader);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("UserIndex", response.Headers.Location!.ToString());
    }

    /// <summary>Anonymous index logged in admin redirects to admin index.</summary>
    [Fact]
    public async Task AnonymousIndex_LoggedInAdmin_RedirectsToAdminIndex()
    {
        var session = await LoginAsAdminAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Dashboard/AnonymousIndex");
        request.Headers.Add("Cookie", session.CookieHeader);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AdminIndex", response.Headers.Location!.ToString());
    }

    // -- AdminIndex -------------------------------------------------------------

    /// <summary>Admin index logged in admin returns200.</summary>
    [Fact]
    public async Task AdminIndex_LoggedInAdmin_Returns200()
    {
        var session = await LoginAsAdminAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Dashboard/AdminIndex");
        request.Headers.Add("Cookie", session.CookieHeader);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        // Reads real host/docker resource files that don't exist in this sandbox
        // (DashboardService.GetServerResourceSummary gracefully treats missing files as empty),
        // so this proves the whole page renders even with no resource data available.
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- UserIndex ----------------------------------------------------------------

    /// <summary>User index logged in user returns200.</summary>
    [Fact]
    public async Task UserIndex_LoggedInUser_Returns200()
    {
        var session = await LoginAsUserAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Dashboard/UserIndex?year=&month=");
        request.Headers.Add("Cookie", session.CookieHeader);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- UserUpdateDefaultMonetary --------------------------------------------------

    /// <summary>User update default monetary logged-in user returns success result.</summary>
    [Fact]
    public async Task UserUpdateDefaultMonetary_LoggedInUser_ReturnsSuccessResult()
    {
        var session = await LoginAsUserAsync();
        using var request = session.BuildJsonPostRequest("/Dashboard/UserUpdateDefaultMonetary", new { DefaultMonetaryUnit = "USD" });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
    }

    /// <summary>User update default monetary anonymous session is forbidden.</summary>
    [Fact]
    public async Task UserUpdateDefaultMonetary_AnonymousSession_IsForbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Dashboard/UserUpdateDefaultMonetary");
        request.Content = System.Net.Http.Json.JsonContent.Create(new { DefaultMonetaryUnit = "USD" });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- CultureManagement ----------------------------------------------------------

    /// <summary>Culture management anonymous session sets culture cookie.</summary>
    [Fact]
    public async Task CultureManagement_AnonymousSession_SetsCultureCookie()
    {
        // Allowed for Anonymous/User/Admin alike — it's just a UI locale preference. Still
        // [ValidateAntiForgeryToken]-gated, so an anonymous caller needs the cookie+token pair
        // from a real page load first (there's no logged-in session to source it from).
        var getResponse = await _client.GetAsync("/Dashboard/AnonymousIndex", TestContext.Current.CancellationToken);
        string html = await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var match = System.Text.RegularExpressions.Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Could not find __RequestVerificationToken on the anonymous dashboard page.");
        string antiForgeryToken = match.Groups[1].Value;
        string? antiForgeryCookie = null;
        if (getResponse.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (string header in setCookies)
            {
                string namePart = header.Split(';', 2)[0];
                if (namePart.StartsWith("__Secure-.AspNetCore.Antiforgery.", StringComparison.Ordinal))
                    antiForgeryCookie = namePart;
            }
        }
        Assert.False(string.IsNullOrEmpty(antiForgeryCookie));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Dashboard/CultureManagement");
        request.Headers.Add("Cookie", antiForgeryCookie);
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);
        request.Content = System.Net.Http.Json.JsonContent.Create(new { Culture = "en-US" });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.Contains(".AspNetCore.Culture")));
    }
}

using System.Net;
using System.Text.RegularExpressions;
using Maroik.Core.Domain.Accounts;
using Maroik.Website.Constants;
using Maroik.Website.Tests.Fixtures;

namespace Maroik.Website.Tests.Views;

/// <summary>
/// The app version in <c>Views/Shared/_MainFooter.cshtml</c>, the footer every role's page gets from <c>_Layout</c> (the Account
/// pages' own layout and the exception layout have no footer): the text is <see cref="AppVersion.Display"/> ("v1.4.2"), and the
/// same element's <c>title</c> carries the whole <see cref="AppVersion.Informational"/> ("1.4.2+2ecebf469478").
/// </summary>
[Collection("Website Integration")]
public partial class MainFooterTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    /// <summary>The footer's version element: its <c>title</c> and its text, both still HTML-encoded.</summary>
    [GeneratedRegex("<footer class=\"main-footer[^\"]*\">.*?<div id=\"mainFooterVersion\"[^>]*title=\"(?<title>[^\"]*)\"[^>]*>(?<text>[^<]*)</div>.*?</footer>", RegexOptions.Singleline)]
    private static partial Regex FooterVersion();

    /// <summary>GETs <paramref name="url"/> (with <paramref name="session"/>'s cookie when there is one), asserts 200, and returns the HTML.</summary>
    private async Task<string> GetHtmlAsync(string url, AuthenticatedSession? session)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (session is not null)
        {
            request.Headers.Add("Cookie", session.CookieHeader);
        }
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Asserts the footer shows the short version and keeps the full one in its title.</summary>
    private static void AssertFooterVersion(string html)
    {
        var match = FooterVersion().Match(html);
        Assert.True(match.Success, "the footer has no #mainFooterVersion element");

        string title = WebUtility.HtmlDecode(match.Groups["title"].Value);
        string text = WebUtility.HtmlDecode(match.Groups["text"].Value).Trim();
        Assert.Equal(AppVersion.Informational, title);
        Assert.Equal(AppVersion.Display, text);
        Assert.DoesNotContain('+', text);
    }

    /// <summary>A signed-out visitor's page shows the version in the footer.</summary>
    [Fact]
    public async Task AnonymousPage_ShowsTheVersionInTheFooter() =>
        AssertFooterVersion(await GetHtmlAsync("/Dashboard/AnonymousIndex", session: null));

    /// <summary>A user's page shows the version in the footer.</summary>
    [Fact]
    public async Task UserPage_ShowsTheVersionInTheFooter()
    {
        var session = await AuthenticatedSessionHelper.LoginAsync(factory, _client, "footer-user@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

        AssertFooterVersion(await GetHtmlAsync("/Dashboard/UserIndex", session));
    }

    /// <summary>An administrator's page shows the version in the footer.</summary>
    [Fact]
    public async Task AdminPage_ShowsTheVersionInTheFooter()
    {
        var session = await AuthenticatedSessionHelper.LoginAsync(factory, _client, "footer-admin@test.com", "AdminPassword1!", Role.Admin, TestContext.Current.CancellationToken);

        AssertFooterVersion(await GetHtmlAsync("/Dashboard/AdminIndex", session));
    }
}

using Maroik.Core.Domain.Account;
using Maroik.Website.Tests.Infrastructure;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration smoke tests for <c>AccountBookController</c>.
/// Asset/Income/Expenditure are User-role-gated in the real production menu (Category
/// Controller="AccountBook", Role=User — loaded from the real Init.sql this test factory now
/// seeds from), so an anonymous GET is correctly redirected rather than rendering; these tests
/// authenticate as a User first.
/// </summary>
[Collection("Website Integration")]
public class AccountBookControllerTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);
    
    /// <summary>Signs in as a User and sends a GET to <paramref name="url"/> with the session cookie.</summary>
    private async Task<HttpResponseMessage> GetAsUserAsync(string url)
    {
        var session = await AuthenticatedSessionHelper.LoginAsync(
            factory, _client, "accountbook-smoke-user@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", session.CookieHeader);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // -- Asset ----------------------------------------------------------------

    /// <summary>Asset get logged in user returns200.</summary>
    [Fact]
    public async Task Asset_Get_LoggedInUser_Returns200()
    {
        var response = await GetAsUserAsync("/AccountBook/Asset");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Asset get anonymous redirects to login.</summary>
    [Fact]
    public async Task Asset_Get_Anonymous_RedirectsToLogin()
    {
        var response = await _client.GetAsync("/AccountBook/Asset", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
    }

    // -- Income ---------------------------------------------------------------

    /// <summary>Income get logged in user returns200.</summary>
    [Fact]
    public async Task Income_Get_LoggedInUser_Returns200()
    {
        var response = await GetAsUserAsync("/AccountBook/Income");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- Expenditure ------------------------------------------------------------

    /// <summary>Expenditure get logged in user returns200.</summary>
    [Fact]
    public async Task Expenditure_Get_LoggedInUser_Returns200()
    {
        var response = await GetAsUserAsync("/AccountBook/Expenditure");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}

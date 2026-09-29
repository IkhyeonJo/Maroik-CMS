using Maroik.Core.Domain.Account;
using Maroik.Website.Tests.Infrastructure;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration smoke tests for <c>ManagementController</c>.
/// Account/Menu are Admin-role-gated in the real production menu (Category
/// Controller="Management", Role=Admin — loaded from the real Init.sql this test factory now
/// seeds from), so an anonymous GET is correctly redirected rather than rendering; these tests
/// authenticate as an Admin first.
/// </summary>
[Collection("Website Integration")]
public class ManagementControllerTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    /// <summary>Signs in as an Admin and sends a GET to <paramref name="url"/> with the session cookie.</summary>
    private async Task<HttpResponseMessage> GetAsAdminAsync(string url)
    {
        var session = await AuthenticatedSessionHelper.LoginAsync(
            factory, _client, "management-smoke-admin@test.com", "AdminPassword1!", Role.Admin, TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", session.CookieHeader);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // -- Account --------------------------------------------------------------

    /// <summary>Account get logged in admin returns200.</summary>
    [Fact]
    public async Task Account_Get_LoggedInAdmin_Returns200()
    {
        var response = await GetAsAdminAsync("/Management/Account");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Account get anonymous redirects to login.</summary>
    [Fact]
    public async Task Account_Get_Anonymous_RedirectsToLogin()
    {
        var response = await _client.GetAsync("/Management/Account", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
    }

    // -- Menu -----------------------------------------------------------------

    /// <summary>Menu get logged in admin returns200.</summary>
    [Fact]
    public async Task Menu_Get_LoggedInAdmin_Returns200()
    {
        var response = await GetAsAdminAsync("/Management/Menu");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}

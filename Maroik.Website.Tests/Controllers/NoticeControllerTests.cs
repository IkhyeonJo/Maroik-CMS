using Maroik.Core.Domain.Account;
using Maroik.Website.Tests.Infrastructure;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration smoke tests for <c>NoticeController</c>.
/// FixedIncome/FixedExpenditure are User-role-gated in the real production menu (Category
/// Controller="Notice", Role=User — loaded from the real Init.sql this test factory now seeds
/// from), so an anonymous GET is correctly redirected rather than rendering; these tests
/// authenticate as a User first.
/// </summary>
[Collection("Website Integration")]
public class NoticeControllerTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    /// <summary>Signs in as a User and sends a GET to <paramref name="url"/> with the session cookie.</summary>
    private async Task<HttpResponseMessage> GetAsUserAsync(string url)
    {
        var session = await AuthenticatedSessionHelper.LoginAsync(
            factory, _client, "notice-smoke-user@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", session.CookieHeader);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // -- FixedIncome ------------------------------------------------------------

    /// <summary>Fixed income get logged in user returns200.</summary>
    [Fact]
    public async Task FixedIncome_Get_LoggedInUser_Returns200()
    {
        var response = await GetAsUserAsync("/Notice/FixedIncome");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Fixed income get anonymous redirects to login.</summary>
    [Fact]
    public async Task FixedIncome_Get_Anonymous_RedirectsToLogin()
    {
        var response = await _client.GetAsync("/Notice/FixedIncome", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
    }

    // -- FixedExpenditure ---------------------------------------------------------

    /// <summary>Fixed expenditure get logged in user returns200.</summary>
    [Fact]
    public async Task FixedExpenditure_Get_LoggedInUser_Returns200()
    {
        var response = await GetAsUserAsync("/Notice/FixedExpenditure");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- CreateFixedIncome: malformed MaturityDate -----------------------------------
    // Regression test: MaturityDate only has [Required] (no format validation), so
    // ModelState.IsValid passes for any non-empty string. DateTime.Parse used to run
    // unguarded, so an unparseable date crashed with an unhandled 500 instead of the
    // {result:false} JSON every other failure path in this controller returns.

    /// <summary>Create fixed income with an unparseable maturity date returns a graceful failure result instead of a 500.</summary>
    [Fact]
    public async Task CreateFixedIncome_MalformedMaturityDate_ReturnsFailureResult_NotServerError()
    {
        var session = await AuthenticatedSessionHelper.LoginAsync(
            factory, _client, "notice-create-badmaturity@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

        using var request = session.BuildJsonPostRequest("/Notice/CreateFixedIncome", new
        {
            Id = 0,
            MainClass = "RegularIncome",
            SubClass = "LaborIncome",
            Content = "Salary",
            Amount = 1000,
            DepositMonth = 1,
            DepositDay = 1,
            MaturityDate = "13/45/2026", // unparseable
            DepositMyAssetProductName = "MyBank",
            Unpunctuality = false
        });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"result\":false", json);
    }
}

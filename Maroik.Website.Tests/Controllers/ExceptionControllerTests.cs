using System.Text.RegularExpressions;
using Maroik.Core.Contract.Interfaces;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for <c>ExceptionController</c>. <c>Error</c> is explicitly allow-listed in
/// <see cref="Maroik.Website.Filters.AuthorizationFilter"/> for both anonymous and logged-in GET
/// requests, so it must render regardless of session state. <c>AccessDenied</c> is not in that
/// allow-list, so a direct GET is redirected away by the filter before the view ever renders.
/// </summary>
[Collection("Website Integration")]
public class ExceptionControllerTests(MaroikWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    // -- Error ----------------------------------------------------------------

    /// <summary>Error anonymous session returns200 and request id.</summary>
    [Fact]
    public async Task Error_AnonymousSession_Returns200AndRequestId()
    {
        var response = await _client.GetAsync("/Exception/Error", TestContext.Current.CancellationToken);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        // No Activity.Current in a plain HTTP request, so ErrorViewModel falls back to
        // HttpContext.TraceIdentifier, which is always populated -> ShowRequestId is true.
        Assert.Contains("Request ID:", html);
    }

    /// <summary>Error logged in user returns200.</summary>
    [Fact]
    public async Task Error_LoggedInUser_Returns200()
    {
        var session = await AuthenticatedSessionHelper.LoginAsync(
            factory, _client, "exception-error-user@test.com", "UserPassword1!", Core.Domain.Account.Role.User,
            TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Exception/Error");
        request.Headers.Add("Cookie", session.CookieHeader);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Error response is not cached.</summary>
    [Fact]
    public async Task Error_ResponseIsNotCached()
    {
        var response = await _client.GetAsync("/Exception/Error", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    /// <summary>
    /// UseExceptionHandler re-executes /Exception/Error with the ORIGINAL request's method, so a
    /// failed POST reaches it as a POST. It must render the error page (not an empty 405, and not an
    /// antiforgery / authorization redirect) for any method.
    /// </summary>
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Error_NonGetMethod_AnonymousSession_RendersErrorPage(string method)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "/Exception/Error");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Request ID:", html);
    }

    /// <summary>
    /// End-to-end regression test: a POST whose action throws (the account service blows up during
    /// login, and Login has no try/catch) must end in the error page — previously the handler
    /// re-ran it as a POST against a GET-only action and produced an empty 405.
    /// </summary>
    [Fact]
    public async Task UnhandledExceptionOnPost_RendersErrorPage()
    {
        var throwingAccountService = new Mock<IAccountService>();
        throwingAccountService
            .Setup(a => a.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await using var factory1 = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAccountService>();
                services.AddScoped(_ => throwingAccountService.Object);
            }));
        var client = factory1.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        // Real antiforgery pair from the login form, so the POST reaches the action.
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
        var getResponse = await client.SendAsync(getRequest, TestContext.Current.CancellationToken);
        string formHtml = await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
 #pragma warning disable SYSLIB1045
        string token = Regex.Match(formHtml, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
 #pragma warning restore SYSLIB1045
        string cookie = getResponse.Headers.GetValues("Set-Cookie")
            .Select(h => h.Split(';', 2)[0])
            .First(c => c.StartsWith("__Secure-.AspNetCore.Antiforgery.", StringComparison.Ordinal));

        using var post = new HttpRequestMessage(HttpMethod.Post, "/Account/Login");
        post.Headers.Add("Cookie", cookie);
        post.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "boom@test.com",
            ["Password"] = "Whatever1!",
            ["__RequestVerificationToken"] = token
        });

        var response = await client.SendAsync(post, TestContext.Current.CancellationToken);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Request ID:", html);
    }

    // -- AccessDenied -----------------------------------------------------------

    /// <summary>Access denied anonymous session is redirected by authorization filter.</summary>
    [Fact]
    public async Task AccessDenied_AnonymousSession_IsRedirectedByAuthorizationFilter()
    {
        // AccessDenied is not allow-listed in AuthorizationFilter (only "Exception"/"Error" is),
        // and it has no registered menu Category, so an anonymous GET never reaches the view.
        var response = await _client.GetAsync("/Exception/AccessDenied", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Dashboard/AnonymousIndex", response.Headers.Location!.ToString());
    }

    /// <summary>Access denied logged-in user is redirected by authorization filter.</summary>
    [Fact]
    public async Task AccessDenied_LoggedInUser_IsRedirectedByAuthorizationFilter()
    {
        var session = await AuthenticatedSessionHelper.LoginAsync(
            factory, _client, "exception-accessdenied-user@test.com", "UserPassword1!", Core.Domain.Account.Role.User,
            TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Exception/AccessDenied");
        request.Headers.Add("Cookie", session.CookieHeader);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Dashboard/AnonymousIndex", response.Headers.Location!.ToString());
    }
}

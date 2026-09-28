using Maroik.Core.Domain.Account;
using Maroik.Website.Tests.Infrastructure;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration smoke tests for <c>CalendarController</c>.
/// The anonymous index uses an anonymous-default <see cref="Maroik.Core.Contract.Dtos.AccountResponse"/>
/// and requires no session; it must respond 200 OK.
/// </summary>
[Collection("Website Integration")]
public class CalendarControllerTests(MaroikWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    /// <summary>Verifies that <c>AnonymousIndex</c> get when returns200.</summary>
    [Fact]
    public async Task AnonymousIndex_Get_Returns200()
    {
        var response = await _client.GetAsync("/Calendar/AnonymousIndex", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- UploadImageFile: validation only ------------------------------------------------
    // Success requires a live Maroik.FileStorage instance, which isn't available in this test
    // environment — only the pre-upload validation branches are covered.

    /// <summary>
    /// Regression test: the summernoteImageFile parameter must be nullable — posting the form
    /// without a file part used to bind null and crash with a NullReferenceException instead of
    /// returning the friendly "please attach a file" result.
    /// </summary>
    [Fact]
    public async Task UploadImageFile_NoFileAttached_ReturnsFailureResult()
    {
        var session = await AuthenticatedSessionHelper.LoginAsync(factory, _client, "calendar-uploadimage-nofile@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        var form = new MultipartFormDataContent();
        using var request = session.BuildFormPostRequest("/Calendar/UploadImageFile", form);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"result\":false", json);
    }
}

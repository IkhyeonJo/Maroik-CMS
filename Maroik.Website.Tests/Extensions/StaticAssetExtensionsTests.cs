using Maroik.Website.Tests.Infrastructure;

namespace Maroik.Website.Tests.Extensions;

/// <summary>
/// Integration tests for <see cref="Maroik.Website.Extensions.StaticAssetExtensions.MapStaticAssets"/> —
/// verifies the explicit .js/.css MIME mappings and the X-Content-Type-Options: nosniff
/// security header applied to every static-file response.
/// </summary>
[Collection("Website Integration")]
public class StaticAssetExtensionsTests(MaroikWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    /// <summary>Static file JavaScript has explicit application JavaScript content type.</summary>
    [Fact]
    public async Task StaticFile_JavaScript_HasExplicitApplicationJavascriptContentType()
    {
        var response = await _client.GetAsync("/anonymous/dist/js/demo.js", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/javascript", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>Static file CSS has explicit text CSS content type.</summary>
    [Fact]
    public async Task StaticFile_Css_HasExplicitTextCssContentType()
    {
        var response = await _client.GetAsync("/anonymous/css/site.css", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>Static file response includes no sniff header.</summary>
    [Fact]
    public async Task StaticFile_Response_IncludesNoSniffHeader()
    {
        var response = await _client.GetAsync("/anonymous/css/site.css", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var values));
        Assert.Contains("nosniff", values);
    }

    /// <summary>
    /// Regression test: role-restricted asset folders are gated by <c>RoleBasedStaticFileMiddleware</c>.
    /// Endpoint routing matches asset routes case-insensitively, so "/Admin/..." resolved to the same
    /// file as "/admin/..." and used to be served to anonymous visitors (200) while the lower-case
    /// path was blocked (403). Every casing must be blocked.
    /// </summary>
    [Theory]
    [InlineData("/admin/dist/js/adminlte.js")]
    [InlineData("/Admin/dist/js/adminlte.js")]
    [InlineData("/ADMIN/dist/js/adminlte.js")]
    [InlineData("/user/dist/js/adminlte.js")]
    [InlineData("/User/dist/js/adminlte.js")]
    [InlineData("/USER/dist/js/adminlte.js")]
    public async Task StaticFile_RoleRestrictedFolder_AnonymousSession_IsForbidden_RegardlessOfCasing(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Static file unknown path returns404.</summary>
    [Fact]
    public async Task StaticFile_UnknownPath_Returns404()
    {
        var response = await _client.GetAsync("/anonymous/does-not-exist.xyz", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}

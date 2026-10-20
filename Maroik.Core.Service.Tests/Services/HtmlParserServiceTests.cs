using Maroik.Core.Contract.Dtos;
using Maroik.Core.Service.Services;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>Unit tests for <see cref="HtmlParserService"/>.</summary>
public class HtmlParserServiceTests
{
    /// <summary>The service under test.</summary>
    private readonly HtmlParserService _sut = new();

    // -- TransformImageAttributesAsync -------------------------------------------

    /// <summary>Verifies that HasImages is false when the HTML has no &lt;img&gt; tags.</summary>
    [Fact]
    public async Task TransformImageAttributesAsync_ReturnsHasImagesFalse_WhenNoImgTags()
    {
        (_, bool hasImages) = await _sut.TransformImageAttributesAsync(
            "<p>no images</p>", (_, _) => Task.FromResult<HtmlImgPatch?>(null), TestContext.Current.CancellationToken);

        Assert.False(hasImages);
    }

    /// <summary>Verifies that HasImages is true when a &lt;img&gt; tag is present.</summary>
    [Fact]
    public async Task TransformImageAttributesAsync_ReturnsHasImagesTrue_WhenImgTagPresent()
    {
        (_, bool hasImages) = await _sut.TransformImageAttributesAsync(
            "<img alt=\"x\" />", (_, _) => Task.FromResult<HtmlImgPatch?>(null), TestContext.Current.CancellationToken);

        Assert.True(hasImages);
    }

    /// <summary>Verifies that the tag's current alt value is passed to the patch factory.</summary>
    [Fact]
    public async Task TransformImageAttributesAsync_PassesAltValue_ToPatchFactory()
    {
        string? capturedAlt = null;
        await _sut.TransformImageAttributesAsync(
            "<img alt=\"path/to/file.png\" />",
            (alt, _) =>
            {
                capturedAlt = alt;
                return Task.FromResult<HtmlImgPatch?>(null);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("path/to/file.png", capturedAlt);
    }

    /// <summary>Verifies that a non-null patch's fields are applied to the tag's attributes.</summary>
    [Fact]
    public async Task TransformImageAttributesAsync_AppliesPatch_ToAltDataFileDataContentType()
    {
        var patch = new HtmlImgPatch(NewAlt: "encrypted", DataFile: "base64data", DataContentType: "image/png");

        (string html, _) = await _sut.TransformImageAttributesAsync(
            "<img alt=\"original\" />", (_, _) => Task.FromResult<HtmlImgPatch?>(patch), TestContext.Current.CancellationToken);

        Assert.Contains("alt=\"encrypted\"", html, StringComparison.Ordinal);
        Assert.Contains("data-file=\"base64data\"", html, StringComparison.Ordinal);
        Assert.Contains("data-contenttype=\"image/png\"", html, StringComparison.Ordinal);
    }

    /// <summary>Verifies that the tag is left unchanged when the patch factory returns null.</summary>
    [Fact]
    public async Task TransformImageAttributesAsync_LeavesTagUnchanged_WhenPatchIsNull()
    {
        (string html, _) = await _sut.TransformImageAttributesAsync(
            "<img alt=\"original\" />", (_, _) => Task.FromResult<HtmlImgPatch?>(null), TestContext.Current.CancellationToken);

        Assert.Contains("alt=\"original\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-file", html, StringComparison.Ordinal);
    }

    /// <summary>Verifies that a patch with Remove = true deletes the tag from the output (async overload).</summary>
    [Fact]
    public async Task TransformImageAttributesAsync_RemovesTag_WhenPatchRemoveIsTrue()
    {
        (string html, _) = await _sut.TransformImageAttributesAsync(
            "<img alt=\"a\" /><img alt=\"b\" />",
            (alt, _) => Task.FromResult<HtmlImgPatch?>(
                alt == "a" ? new HtmlImgPatch(Remove: true) : new HtmlImgPatch(NewAlt: "B")),
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain("alt=\"a\"", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"B\"", html, StringComparison.Ordinal);
    }

    /// <summary>Verifies that each image's patch is correctly matched back to its own tag when
    /// multiple images are patched concurrently (no index mix-up from out-of-order completion).</summary>
    [Fact]
    public async Task TransformImageAttributesAsync_MatchesEachPatchToItsOwnTag_WhenMultipleImages()
    {
        // Completion order is deliberately reversed (last alt resolves first) to catch any
        // implementation that assumes patches complete in the same order as the tags.
        (string html, bool hasImages) = await _sut.TransformImageAttributesAsync(
            "<img alt=\"a\" /><img alt=\"b\" /><img alt=\"c\" />",
            async (alt, _) =>
            {
                await Task.Delay(alt == "a" ? 30 : alt == "b" ? 15 : 0, TestContext.Current.CancellationToken);
                return new HtmlImgPatch(NewAlt: alt.ToUpperInvariant());
            },
            TestContext.Current.CancellationToken);

        Assert.True(hasImages);
        Assert.Contains("alt=\"A\"", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"B\"", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"C\"", html, StringComparison.Ordinal);
    }

    // -- TransformImageAttributes (sync) -----------------------------------------

    /// <summary>Verifies that the sync overload applies NewAlt from the patch.</summary>
    [Fact]
    public void TransformImageAttributes_AppliesNewAlt_FromPatch()
    {
        string html = _sut.TransformImageAttributes(
            "<img alt=\"original\" />", _ => new HtmlImgPatch(NewAlt: "decrypted"));

        Assert.Contains("alt=\"decrypted\"", html, StringComparison.Ordinal);
    }

    /// <summary>Verifies that the sync overload leaves alt unchanged when the patch factory returns null.</summary>
    [Fact]
    public void TransformImageAttributes_LeavesAlt_WhenPatchIsNull()
    {
        string html = _sut.TransformImageAttributes("<img alt=\"original\" />", _ => null);

        Assert.Contains("alt=\"original\"", html, StringComparison.Ordinal);
    }

    /// <summary>Verifies that the sync overload deletes the tag when the patch has Remove = true,
    /// and leaves surrounding content and other images intact.</summary>
    [Fact]
    public void TransformImageAttributes_RemovesTag_WhenPatchRemoveIsTrue()
    {
        string html = _sut.TransformImageAttributes(
            "<p>keep</p><img alt=\"forged\" /><img alt=\"ok\" />",
            alt => alt == "forged" ? new HtmlImgPatch(Remove: true) : new HtmlImgPatch(NewAlt: "clean"));

        Assert.DoesNotContain("forged", html, StringComparison.Ordinal);
        Assert.Contains("<p>keep</p>", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"clean\"", html, StringComparison.Ordinal);
    }

    // -- Parsed the way the browser (and the sanitizer) parse it ------------------------------
    // The stored HTML went through HtmlContentSanitizerService, whose parser (AngleSharp) follows the HTML5 parsing algorithm, as
    // the browser does. This service must read the markup the same way: a parser that disagrees about what is an element would
    // patch (and re-serialize) markup the sanitizer judged as something else.

    /// <summary>
    /// Inside a &lt;textarea&gt; (RCDATA) "&lt;img&gt;" is text, not an element: the sanitizer left it alone as text, so it is not
    /// handed to the patch factory either.
    /// </summary>
    [Fact]
    public void TransformImageAttributes_LeavesImgMarkupInsideATextareaAlone_AsTheBrowserReadsItAsText()
    {
        List<string> alts = [];

        _sut.TransformImageAttributes("<textarea><img alt=\"inside\"></textarea>", alt => { alts.Add(alt); return null; });

        Assert.Empty(alts);
    }

    /// <summary>The async overload reads a &lt;textarea&gt;'s content as text too.</summary>
    [Fact]
    public async Task TransformImageAttributesAsync_LeavesImgMarkupInsideATextareaAlone_AsTheBrowserReadsItAsText()
    {
        (_, bool hasImages) = await _sut.TransformImageAttributesAsync("<textarea><img alt=\"inside\"></textarea>",
            (_, _) => Task.FromResult<HtmlImgPatch?>(new HtmlImgPatch(DataFile: "x")), TestContext.Current.CancellationToken);

        Assert.False(hasImages);
    }

    /// <summary>
    /// An &lt;img&gt; directly inside a &lt;table&gt; is moved in front of the table by the HTML5 algorithm (foster parenting); the
    /// output is the tree the browser builds, not the source text.
    /// </summary>
    [Fact]
    public void TransformImageAttributes_SerializesTheTreeTheBrowserBuilds()
    {
        string html = _sut.TransformImageAttributes("<table><img alt=\"a\"></table>", _ => new HtmlImgPatch(NewAlt: "A"));

        Assert.Equal("<img alt=\"A\"><table></table>", html);
    }

    /// <summary>
    /// A post's &lt;img&gt; carries its file's Data Protection-protected storage path in <c>alt</c> ("CfDJ8…", base64url). The
    /// factory receives it exactly, and an untouched tag keeps it exactly, in both overloads.
    /// </summary>
    [Fact]
    public async Task TransformImageAttributes_PassesAndKeepsAProtectedAltValueExactly()
    {
        const string protectedAlt = "CfDJ8HTAFH7hn0tPj9LZyUduDPGx1XFZ7ozclO1fPW6E-VxIpDx7wiAkHWtmatuIc6nKb-BHOrEF5IkFem_FZTfR3KQw-_khgyuCgpLjCjdWWtHwCPW74M1wq6y5g_X-V3E2KZ8qD-PSsl1RS0HJdh_H7tMbofS4kjSPYiUZKaZ266WZXgfGxwxxkAIx553m5ZeXr_XAClEg_FeL_ggbVMHZMg4Um7bbU3d0LazJ0-MU0TLu";
        string source = $"<p>text</p><p><img style=\"width: 50%;\" alt=\"{protectedAlt}\"></p>";
        List<string> alts = [];

        string sync = _sut.TransformImageAttributes(source, alt => { alts.Add(alt); return null; });
        (string async, _) = await _sut.TransformImageAttributesAsync(source,
            (alt, _) => { alts.Add(alt); return Task.FromResult<HtmlImgPatch?>(null); }, TestContext.Current.CancellationToken);

        Assert.Equal([protectedAlt, protectedAlt], alts);
        Assert.Equal(source, sync);
        Assert.Equal(source, async);
    }

    /// <summary>
    /// When one fetch fails, the method still waits for the fetches already running before it returns that failure: they
    /// hold the concurrency gate, which the method disposes on the way out, so returning early would dispose it under them.
    /// </summary>
    [Fact]
    public async Task TransformImageAttributesAsync_WaitsForTheOtherFetches_BeforeReportingAFailedOne()
    {
        var slowFetch = new TaskCompletionSource<HtmlImgPatch?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<(string Html, bool HasImages)> transform = _sut.TransformImageAttributesAsync(
            "<img alt=\"slow\"><img alt=\"broken\">",
            (alt, _) => alt == "broken" ? Task.FromException<HtmlImgPatch?>(new InvalidOperationException("fetch failed")) : slowFetch.Task,
            TestContext.Current.CancellationToken);

        await Task.Delay(100, TestContext.Current.CancellationToken);
        bool finishedWhileAFetchWasRunning = transform.IsCompleted;
        slowFetch.SetResult(null);

        Assert.False(finishedWhileAFetchWasRunning);
        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(() => transform);
        Assert.Equal("fetch failed", failure.Message);
    }

    // -- Images without alt, and more images than the fetch limit ----------------------------

    /// <summary>An &lt;img&gt; without an alt reaches the async patch factory as an empty string.</summary>
    [Fact]
    public async Task TransformImageAttributesAsync_PassesAnEmptyAlt_ForAnImageWithoutOne()
    {
        string? capturedAlt = null;

        await _sut.TransformImageAttributesAsync("<img src=\"x\" />",
            (alt, _) => { capturedAlt = alt; return Task.FromResult<HtmlImgPatch?>(null); }, TestContext.Current.CancellationToken);

        Assert.Equal("", capturedAlt);
    }

    /// <summary>An &lt;img&gt; without an alt reaches the sync patch factory as an empty string.</summary>
    [Fact]
    public void TransformImageAttributes_PassesAnEmptyAlt_ForAnImageWithoutOne()
    {
        string? capturedAlt = null;

        _sut.TransformImageAttributes("<img src=\"x\" />", alt => { capturedAlt = alt; return null; });

        Assert.Equal("", capturedAlt);
    }

    /// <summary>
    /// A post with more images than may be fetched at once still gets every image patched: each finished fetch frees
    /// its slot for the next one.
    /// </summary>
    [Fact]
    public async Task TransformImageAttributesAsync_PatchesEveryImage_WhenThereAreMoreThanTheConcurrentFetchLimit()
    {
        string html = string.Concat(Enumerable.Range(0, 9).Select(i => $"<img alt=\"{i}\" />"));

        Task<(string Html, bool HasImages)> transform = _sut.TransformImageAttributesAsync(html,
            async (alt, _) => { await Task.Yield(); return new HtmlImgPatch(NewAlt: $"done-{alt}"); }, TestContext.Current.CancellationToken);
        (string result, _) = await transform.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        for (int i = 0; i < 9; i++)
            Assert.Contains($"alt=\"done-{i}\"", result, StringComparison.Ordinal);
    }
}

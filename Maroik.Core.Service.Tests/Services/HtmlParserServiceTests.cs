using Maroik.Core.Contract.Interfaces;
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

        Assert.Contains("alt=\"encrypted\"", html);
        Assert.Contains("data-file=\"base64data\"", html);
        Assert.Contains("data-contenttype=\"image/png\"", html);
    }

    /// <summary>Verifies that the tag is left unchanged when the patch factory returns null.</summary>
    [Fact]
    public async Task TransformImageAttributesAsync_LeavesTagUnchanged_WhenPatchIsNull()
    {
        (string html, _) = await _sut.TransformImageAttributesAsync(
            "<img alt=\"original\" />", (_, _) => Task.FromResult<HtmlImgPatch?>(null), TestContext.Current.CancellationToken);

        Assert.Contains("alt=\"original\"", html);
        Assert.DoesNotContain("data-file", html);
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

        Assert.DoesNotContain("alt=\"a\"", html);
        Assert.Contains("alt=\"B\"", html);
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
        Assert.Contains("alt=\"A\"", html);
        Assert.Contains("alt=\"B\"", html);
        Assert.Contains("alt=\"C\"", html);
    }

    // -- TransformImageAttributes (sync) -----------------------------------------

    /// <summary>Verifies that the sync overload applies NewAlt from the patch.</summary>
    [Fact]
    public void TransformImageAttributes_AppliesNewAlt_FromPatch()
    {
        string html = _sut.TransformImageAttributes(
            "<img alt=\"original\" />", _ => new HtmlImgPatch(NewAlt: "decrypted"));

        Assert.Contains("alt=\"decrypted\"", html);
    }

    /// <summary>Verifies that the sync overload leaves alt unchanged when the patch factory returns null.</summary>
    [Fact]
    public void TransformImageAttributes_LeavesAlt_WhenPatchIsNull()
    {
        string html = _sut.TransformImageAttributes("<img alt=\"original\" />", _ => null);

        Assert.Contains("alt=\"original\"", html);
    }

    /// <summary>Verifies that the sync overload deletes the tag when the patch has Remove = true,
    /// and leaves surrounding content and other images intact.</summary>
    [Fact]
    public void TransformImageAttributes_RemovesTag_WhenPatchRemoveIsTrue()
    {
        string html = _sut.TransformImageAttributes(
            "<p>keep</p><img alt=\"forged\" /><img alt=\"ok\" />",
            alt => alt == "forged" ? new HtmlImgPatch(Remove: true) : new HtmlImgPatch(NewAlt: "clean"));

        Assert.DoesNotContain("forged", html);
        Assert.Contains("<p>keep</p>", html);
        Assert.Contains("alt=\"clean\"", html);
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
            Assert.Contains($"alt=\"done-{i}\"", result);
    }
}

using HtmlAgilityPack;
using Maroik.Core.Contract.Interfaces;

namespace Maroik.Core.Service.Services;

/// <summary>HtmlAgilityPack-backed implementation of <see cref="IHtmlParserService"/>.</summary>
public class HtmlParserService : IHtmlParserService
{
    // ReSharper disable once InvalidXmlDocComment
    /// Upper bound on how many <paramref name="patchFactory"/> calls run at once in
    /// <see cref="TransformImageAttributesAsync"/>. Each call is typically a request to the
    /// file-storage service, so a post with dozens of images must not fan out into dozens of
    /// simultaneous connections.
    /// </summary>
    private const int MaxConcurrentPatchFetches = 4;

    /// <inheritdoc />
    public async Task<(string Html, bool HasImages)> TransformImageAttributesAsync(
        string html,
        Func<string, CancellationToken, Task<HtmlImgPatch?>> patchFactory,
        CancellationToken ct = default)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        List<HtmlNode> imgTags = [.. doc.DocumentNode.Descendants("img")];

        // Fetch image patches concurrently (patchFactory typically makes a network call to the
        // file-storage service) but cap the fan-out so a many-image post can't storm that service.
        var gate = new SemaphoreSlim(MaxConcurrentPatchFetches, MaxConcurrentPatchFetches);

        Task<HtmlImgPatch?>[] fetchTasks = [.. imgTags.Select(FetchPatchAsync)];
        HtmlImgPatch?[] patches;
        try
        {
            patches = await Task.WhenAll(fetchTasks);
        }
        finally
        {
            // Wait for every sibling task to finish before disposing the semaphore — if one fetch
            // faulted, Task.WhenAll rethrows immediately while the others are still inside
            // gate.WaitAsync()/gate.Release(), and disposing here would turn those into unobserved
            // ObjectDisposedExceptions. Swallow faults from the drain (the original is already
            // propagating).
            try { await Task.WhenAll(fetchTasks); } catch { /* faults already surfaced above */ }
            gate.Dispose();
        }

        for (int i = 0; i < imgTags.Count; i++)
        {
            HtmlImgPatch? patch = patches[i];
            if (patch == null) continue;
            HtmlNode imgTag = imgTags[i];
            if (patch.Remove) { imgTag.Remove(); continue; }
            if (patch.NewAlt != null) imgTag.SetAttributeValue("alt", patch.NewAlt);
            if (patch.DataFile != null) imgTag.SetAttributeValue("data-file", patch.DataFile);
            if (patch.DataContentType != null) imgTag.SetAttributeValue("data-contenttype", patch.DataContentType);
        }

        return (doc.DocumentNode.OuterHtml, imgTags.Count > 0);

        async Task<HtmlImgPatch?> FetchPatchAsync(HtmlNode imgTag)
        {
            // ReSharper disable once AccessToDisposedClosure
            await gate.WaitAsync(ct);
            try { return await patchFactory(imgTag.GetAttributeValue("alt", ""), ct); }
            // ReSharper disable once AccessToDisposedClosure
            finally { _ = gate.Release(); }
        }
    }

    /// <inheritdoc />
    public string TransformImageAttributes(string html, Func<string, HtmlImgPatch?> patchFactory)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Materialize first: a patch may remove its node, which would invalidate the lazy
        // Descendants() enumerator mid-iteration.
        foreach (HtmlNode imgTag in doc.DocumentNode.Descendants("img").ToList())
        {
            string alt = imgTag.GetAttributeValue("alt", "");
            HtmlImgPatch? patch = patchFactory(alt);
            if (patch == null) continue;
            if (patch.Remove) { imgTag.Remove(); continue; }
            if (patch.NewAlt != null) imgTag.SetAttributeValue("alt", patch.NewAlt);
        }

        return doc.DocumentNode.OuterHtml;
    }
}

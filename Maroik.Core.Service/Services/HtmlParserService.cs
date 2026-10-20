using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;

namespace Maroik.Core.Service.Services;

/// <summary>AngleSharp-backed implementation of <see cref="IHtmlParserService"/>.</summary>
/// <remarks>
/// AngleSharp is the parser <see cref="HtmlContentSanitizerService"/> (Ganss.Xss) sanitizes with, and it follows the HTML5
/// parsing algorithm the browser uses. Reading the stored HTML with the same algorithm means this service patches exactly the
/// &lt;img&gt; elements the sanitizer judged and the browser will build — no second parser that could disagree about what is an
/// element (raw text, foster parenting, ...). The HTML is parsed as a fragment in a &lt;body&gt;, as it is shown.
/// </remarks>
public class HtmlParserService : IHtmlParserService
{
    /// <summary>
    /// Upper bound on how many <c>patchFactory</c> calls run at once in
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
        using IDocument document = ParseIntoBody(html, out IElement body);
        List<IElement> imgTags = [.. body.QuerySelectorAll("img")];

        // Fetch image patches concurrently (patchFactory typically makes a network call to the
        // file-storage service) but cap the fan-out so a many-image post can't storm that service.
        // Disposed when the method ends, on every path. That is safe even when a fetch fails: Task.WhenAll completes only once
        // every fetch has completed (a fault included), so no fetch is still inside gate.WaitAsync() / gate.Release() by then.
        using var gate = new SemaphoreSlim(MaxConcurrentPatchFetches, MaxConcurrentPatchFetches);

        HtmlImgPatch?[] patches = await Task.WhenAll(imgTags.Select(FetchPatchAsync));

        for (int i = 0; i < imgTags.Count; i++)
        {
            HtmlImgPatch? patch = patches[i];
            if (patch == null) continue;
            IElement imgTag = imgTags[i];
            if (patch.Remove) { imgTag.Remove(); continue; }
            if (patch.NewAlt != null) imgTag.SetAttribute("alt", patch.NewAlt);
            if (patch.DataFile != null) imgTag.SetAttribute("data-file", patch.DataFile);
            if (patch.DataContentType != null) imgTag.SetAttribute("data-contenttype", patch.DataContentType);
        }

        return (body.InnerHtml, imgTags.Count > 0);

        // Runs patchFactory for one <img>'s alt value once a gate slot is free, releasing the slot
        // afterwards, so no more than MaxConcurrentPatchFetches fetches are ever in flight.
        async Task<HtmlImgPatch?> FetchPatchAsync(IElement imgTag)
        {
            // ReSharper disable once AccessToDisposedClosure
            await gate.WaitAsync(ct);
            try { return await patchFactory(imgTag.GetAttribute("alt") ?? "", ct); }
            // ReSharper disable once AccessToDisposedClosure
            finally { _ = gate.Release(); }
        }
    }

    /// <inheritdoc />
    public string TransformImageAttributes(string html, Func<string, HtmlImgPatch?> patchFactory)
    {
        using IDocument document = ParseIntoBody(html, out IElement body);

        // Materialize first: a patch may remove its node while the loop is still going.
        foreach (IElement imgTag in body.QuerySelectorAll("img").ToList())
        {
            string alt = imgTag.GetAttribute("alt") ?? "";
            HtmlImgPatch? patch = patchFactory(alt);
            if (patch == null) continue;
            if (patch.Remove) { imgTag.Remove(); continue; }
            if (patch.NewAlt != null) imgTag.SetAttribute("alt", patch.NewAlt);
        }

        return body.InnerHtml;
    }

    /// <summary>
    /// Parses <paramref name="html"/> as the content of a &lt;body&gt; (the HTML5 fragment algorithm) and returns the owning
    /// document (the caller disposes it), with <paramref name="body"/> holding the parsed nodes.
    /// </summary>
    private static IDocument ParseIntoBody(string html, out IElement body)
    {
        var parser = new HtmlParser();
        IDocument document = parser.ParseDocument(string.Empty);
        body = document.CreateElement("body");
        body.Append([.. parser.ParseFragment(html, body)]);
        return document;
    }
}

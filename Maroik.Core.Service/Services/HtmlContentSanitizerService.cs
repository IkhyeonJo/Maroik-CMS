using AngleSharp;
using Ganss.Xss;
using Maroik.Core.Contract.Interfaces;

namespace Maroik.Core.Service.Services;

/// <summary>Ganss.Xss-backed implementation of <see cref="IHtmlContentSanitizerService"/>.</summary>
/// <remarks>
/// Registered as a singleton. The <see cref="HtmlSanitizer"/> is configured once at construction
/// and reused for every call: building it (and its allow-lists) per <see cref="Sanitize"/> call
/// was pure per-request allocation. <see cref="HtmlSanitizer.Sanitize(string,string,IMarkupFormatter)"/> does not mutate
/// the configured allow-lists, so concurrent calls on the shared instance are safe.
/// <para>
/// The tag / attribute allow-list is Ganss.Xss's own default (broad enough for everything
/// Summernote emits — headings, lists, tables, spans with inline <c>style</c>, links, images —
/// and actively maintained against new bypass vectors). Two security-critical knobs are then
/// tightened explicitly, so a package upgrade cannot silently widen them:
/// only <c>http</c>/<c>https</c> URL schemes are permitted, and arbitrary <c>data-*</c>
/// attributes are rejected — the display pipeline
/// (<c>AttachmentContentService.PrepareHtmlForDisplayAsync</c>) re-adds the specific
/// <c>data-file</c>/<c>data-contenttype</c> attributes it needs after sanitization. The one widening
/// is the <c>class</c> attribute, which the default list omits and Summernote output relies on.
/// The CSS property allow-list is also narrowed: the positioning / stacking / opacity properties
/// are removed so a post cannot overlay the rest of the page (CSP does not stop that).
/// </para>
/// </remarks>
public class HtmlContentSanitizerService : IHtmlContentSanitizerService
{
    /// <summary>
    /// Inline-style properties that let stored content leave its own box and stack above the page
    /// (a full-screen invisible link or a fake login overlay). The default allow-list includes them;
    /// Summernote never emits them.
    /// </summary>
    private static readonly string[] OverlayCssProperties =
        ["position", "top", "left", "right", "bottom", "z-index", "opacity"];

    /// <summary>The one shared, pre-configured sanitizer used by every <see cref="Sanitize"/> call.</summary>
    private readonly HtmlSanitizer _sanitizer;

    /// <summary>Builds the shared, pre-configured <see cref="HtmlSanitizer"/>.</summary>
    public HtmlContentSanitizerService()
    {
        // Start from the full default allow-lists, then narrow only what matters.
        _sanitizer = new HtmlSanitizer
        {
            AllowDataAttributes = false,
        };

        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");

        _sanitizer.AllowedAttributes.Add("class");

        foreach (string property in OverlayCssProperties)
        {
            _sanitizer.AllowedCssProperties.Remove(property);
        }
    }

    /// <inheritdoc />
    public string Sanitize(string html) => _sanitizer.Sanitize(html);
}

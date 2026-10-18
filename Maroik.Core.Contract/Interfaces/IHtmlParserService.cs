namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Traverses and patches <c>&lt;img&gt;</c> attributes in an HTML string
/// without coupling callers to a specific HTML parsing library.
/// </summary>
public interface IHtmlParserService
{
    /// <summary>
    /// Calls <paramref name="patchFactory"/> for each <c>&lt;img&gt;</c> tag's <c>alt</c> value
    /// and applies the returned <see cref="HtmlImgPatch"/> to that tag's attributes.
    /// Returns the modified HTML and whether any images were found.
    /// </summary>
    Task<(string Html, bool HasImages)> TransformImageAttributesAsync(
        string html,
        Func<string, CancellationToken, Task<HtmlImgPatch?>> patchFactory,
        CancellationToken ct = default);

    /// <summary>Synchronous overload of <see cref="TransformImageAttributesAsync"/>.</summary>
    string TransformImageAttributes(string html, Func<string, HtmlImgPatch?> patchFactory);
}

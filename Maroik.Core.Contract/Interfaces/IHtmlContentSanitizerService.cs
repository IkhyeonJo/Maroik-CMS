namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Strips unsafe HTML markup without coupling callers to a specific sanitization library.
/// </summary>
public interface IHtmlContentSanitizerService
{
    /// <summary>Returns a sanitized copy of <paramref name="html"/> with unsafe tags and attributes removed.</summary>
    string Sanitize(string html);
}

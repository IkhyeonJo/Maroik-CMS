namespace Maroik.Website.Constants;

/// <summary>
/// Keys of the one-request <c>TempData</c> entries a form post leaves for the page it redirects to.
/// </summary>
internal static class TempDataKeys
{
    /// <summary>Why a profile time-zone change was refused, shown beside the time-zone form after the redirect.</summary>
    internal const string TimeZoneError = "TimeZoneError";
}

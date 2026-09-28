namespace Maroik.Core.Domain.Localization;

/// <summary>
/// Locale-derived defaults. Keeps the "which culture implies which default" decisions in one
/// place instead of scattered across views and client scripts.
/// </summary>
public static class CulturePolicy
{
    /// <summary>
    /// The cultures the application supports end to end (request localization, the language
    /// switcher, and server-side validation of a posted culture code). Single source of truth so
    /// Program.cs's <c>RequestLocalizationOptions</c> and any controller/view that validates a
    /// culture value can't drift apart.
    /// </summary>
    public static readonly IReadOnlyList<string> SupportedCultures = ["en-US", "ko-KR"];

    /// <summary>The culture used when none is specified or the requested one isn't supported.</summary>
    public const string DefaultCulture = "en-US";

    /// <summary>
    /// The IANA time zone to pre-select on the registration form for <paramref name="culture"/>,
    /// or <see langword="null"/> when the user should pick one explicitly.
    /// </summary>
    public static string? DefaultTimeZoneIanaId(string? culture) => culture switch
    {
        "ko-KR" => "Asia/Seoul",
        _ => null
    };
}

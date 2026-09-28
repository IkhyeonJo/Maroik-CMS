namespace Maroik.Core.Domain.Calendar;

/// <summary>
/// Canonical calendar-ownership discriminator strings that label a calendar (and its events)
/// as owned by the current user versus subscribed to from another user.
/// This is the single source of truth for these values across every layer — the service,
/// presentation, and test code all reference these constants directly rather than keeping a
/// parallel enum. Living in the domain keeps calendar rules free of any Contract/Website dependency.
/// </summary>
public static class CalendarTypes
{
    /// <summary>Calendar owned by the current user.</summary>
    public const string My = "My";

    /// <summary>Calendar owned by another user that the current user has subscribed to.</summary>
    public const string Other = "Other";
}

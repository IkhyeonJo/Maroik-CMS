using ErrorOr;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Domain.Primitives;
using Maroik.Core.Domain.ValueObjects;

namespace Maroik.Core.Domain.Calendar;

/// <summary>
/// Aggregate root for a calendar.
/// Controls calendar metadata. Sharing permissions (<see cref="CalendarShared"/>) and subscriber
/// relationships (<see cref="OtherCalendar"/>) are persisted independently via their own
/// repositories rather than hydrated as part of this aggregate, so this class does not mediate
/// their persistence.
/// Calendar events are separate aggregate roots (referenced by CalendarId).
/// </summary>
public sealed class Calendar : AggregateRoot<long>
{
    /// <summary>Email of the account that owns this calendar.</summary>
    public Email AccountEmail { get; private set; }

    /// <summary>Calendar display name (e.g. "Work", "Personal").</summary>
    public string Name { get; private set; }

    /// <summary>Optional description of the calendar's purpose.</summary>
    public string? Description { get; private set; }

    /// <summary>Default IANA time-zone for events in this calendar.</summary>
    public TimeZoneId TimeZone { get; private set; }

    /// <summary>HTML color code used to visually distinguish this calendar (e.g. "#FF5733").</summary>
    public HtmlColorCode ColorCode { get; private set; }

    /// <summary>UTC timestamp when the calendar was created.</summary>
    public DateTime Created { get; private set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime Updated { get; private set; }

    /// <summary>Sets the calendar's fields and stamps <see cref="Created"/>/<see cref="Updated"/> with the
    /// current UTC time (<see cref="Reconstitute"/> overwrites both with the stored values).</summary>
    private Calendar(
        long id,
        Email accountEmail,
        string name,
        string? description,
        TimeZoneId timeZone,
        HtmlColorCode colorCode) : base(id)
    {
        AccountEmail = accountEmail;
        Name = name;
        Description = description;
        TimeZone = timeZone;
        ColorCode = colorCode;
        Created = DateTime.UtcNow;
        Updated = DateTime.UtcNow;
    }

    // ------------------------------------------------------------------------
    // Factory / Reconstitution
    // ------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds a <see cref="Calendar"/> from trusted raw values from the repository layer.
    /// Bypasses validation — use only with data from the trusted repository layer.
    /// </summary>
    public static Calendar Reconstitute(
        long id,
        string accountEmail,
        string name,
        string? description,
        string timeZoneIanaId,
        string htmlColorCode,
        DateTime created,
        DateTime updated)
    {
        var calendar = new Calendar(
            id,
            Email.FromTrustedSource(accountEmail),
            name,
            description,
            TimeZoneId.FromTrustedSource(timeZoneIanaId),
            HtmlColorCode.FromTrustedSource(htmlColorCode)) { Created = created, Updated = updated };

        return calendar;
    }

    /// <summary>
    /// Creates a new calendar.
    /// </summary>
    public static ErrorOr<Calendar> Create(
        string? accountEmailValue,
        string? name,
        string? description,
        string? timeZoneValue,
        string? htmlColorCode)
    {
        var nameResult = ValidateName(name);
        if (nameResult.IsError) return nameResult.Errors;

        var emailResult = Email.Create(accountEmailValue);
        if (emailResult.IsError) return emailResult.Errors;

        var tzResult = TimeZoneId.Create(timeZoneValue);
        if (tzResult.IsError) return tzResult.Errors;

        var colorResult = HtmlColorCode.Create(htmlColorCode);
        if (colorResult.IsError) return colorResult.Errors;

        var calendar = new Calendar(0, emailResult.Value, nameResult.Value, description, tzResult.Value, colorResult.Value);
        return calendar;
    }

    // ------------------------------------------------------------------------
    // Domain behaviours
    // ------------------------------------------------------------------------

    /// <summary>Updates the calendar's display fields.</summary>
    public ErrorOr<Success> Update(
        string? name,
        string? description,
        string? timeZoneValue,
        string? htmlColorCode)
    {
        var nameResult = ValidateName(name);
        if (nameResult.IsError) return nameResult.Errors;

        var tzResult = TimeZoneId.Create(timeZoneValue);
        if (tzResult.IsError) return tzResult.Errors;

        var colorResult = HtmlColorCode.Create(htmlColorCode);
        if (colorResult.IsError) return colorResult.Errors;

        Name = nameResult.Value;
        Description = description;
        TimeZone = tzResult.Value;
        ColorCode = colorResult.Value;
        Updated = DateTime.UtcNow;
        return Result.Success;
    }

    /// <summary>
    /// Shared validation for <see cref="Name"/>, run by both <see cref="Create"/> and
    /// <see cref="Update"/>. A calendar name is a plain label, never markup — the
    /// browse / subscribed-calendar lists render it straight into the DOM — so a value carrying
    /// angle brackets or control characters is rejected here rather than trusted at every render
    /// site (matches how <c>Board</c> / comment display text is treated as untrusted). The length
    /// is capped at the <c>character varying(255)</c> column width so an over-long name surfaces as
    /// a clean validation error instead of a raw database exception.
    /// </summary>
    private static ErrorOr<string> ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return LocalizableError.Validation("Calendar.NameEmpty", "Calendar name cannot be empty.");

        if (name.Length > ShortTextPolicy.MaxLength)
            return LocalizableError.Validation("Calendar.NameTooLong", "Calendar name must be {0} characters or fewer.", ShortTextPolicy.MaxLength);

        if (name.Any(c => c is '<' or '>' || char.IsControl(c)))
            return LocalizableError.Validation(
                "Calendar.NameInvalid",
                "Calendar name cannot contain angle brackets or control characters.");

        return name;
    }

}

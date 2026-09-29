using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.Calendar;

/// <summary>
/// Aggregate root that holds the sharing permissions for a <see cref="Calendar"/>.
/// One-to-one with Calendar (CalendarId is both the PK and FK).
/// Persisted independently via its own <c>ICalendarSharedRepository</c> (not hydrated or cascaded
/// through the <see cref="Calendar"/> aggregate), which is why it is an aggregate root rather than
/// a child entity.
/// </summary>
public sealed class CalendarShared : AggregateRoot<long>
{
    /// <summary>When true, registered users can view this calendar.</summary>
    public bool User { get; private set; }

    /// <summary>When true, anonymous (unauthenticated) visitors can view this calendar.</summary>
    public bool Anonymous { get; private set; }

    /// <summary>Sets both sharing flags; reached only through <see cref="Reconstitute"/> / <see cref="CreatePrivate"/>.</summary>
    private CalendarShared(long calendarId, bool user, bool anonymous) : base(calendarId)
    {
        User = user;
        Anonymous = anonymous;
    }

    /// <summary>
    /// Rebuilds a <see cref="CalendarShared"/> from trusted raw values from the repository layer.
    /// Bypasses validation — use only with data from the trusted repository layer.
    /// </summary>
    public static CalendarShared Reconstitute(long calendarId, bool user, bool anonymous)
        => new(calendarId, user, anonymous);

    /// <summary>Creates a sharing-permission record with both flags initially off.</summary>
    public static CalendarShared CreatePrivate(long calendarId) => new(calendarId, false, false);

    /// <summary>Updates the sharing flags and returns self for fluent use.</summary>
    public CalendarShared Update(bool user, bool anonymous)
    {
        User = user;
        Anonymous = anonymous;
        return this;
    }
}

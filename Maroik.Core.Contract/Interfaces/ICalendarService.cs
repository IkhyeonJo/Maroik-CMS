using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Service interface for all calendar-related business logic, including
/// calendar CRUD, event management, sharing, subscriptions, and file uploads.
/// </summary>
public interface ICalendarService
{
    /// <summary>Returns calendars owned by the given account.</summary>
    Task<List<CalendarResponse>> GetCalendarsAsync(string email, CancellationToken ct = default);

    /// <summary>Returns every calendar in the system (used for shared-calendar discovery).</summary>
    Task<List<CalendarResponse>> GetAllCalendarsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns all events belonging to any of the given calendars, fetched in a single query
    /// and grouped by calendar ID for per-calendar lookup. A calendar ID with no events is not
    /// present as a key — look it up with <c>GetValueOrDefault(id, [])</c>.
    /// </summary>
    Task<IReadOnlyDictionary<long, List<CalendarEventResponse>>> GetCalendarEventsAsync(IEnumerable<long> calendarIds, CancellationToken ct = default);

    /// <summary>Returns a single calendar event by ID, or null.</summary>
    Task<CalendarEventResponse?> GetCalendarEventAsync(long id, CancellationToken ct = default);

    /// <summary>Returns the file attached to the given calendar event, or null.</summary>
    Task<CalendarEventAttachedFileDto?> GetCalendarEventAttachedFileAsync(long calendarEventId, CancellationToken ct = default);

    /// <summary>Returns all reminder settings for the given calendar event.</summary>
    Task<IEnumerable<CalendarEventReminderDto>> GetCalendarEventRemindersAsync(long calendarEventId, CancellationToken ct = default);

    /// <summary>Returns calendars that the given account has subscribed to from other users.</summary>
    Task<List<OtherCalendarResponse>> GetOtherCalendarsAsync(string email, CancellationToken ct = default);

    /// <summary>Returns the sharing-permission rows for all calendars.</summary>
    Task<List<CalendarSharedResponse>> GetCalendarSharedAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the calendars the caller can see beyond ones they own: calendars they subscribe
    /// to when logged in, or calendars shared anonymously when not. Used by every "Other
    /// calendars" endpoint (the calendar list, event-existence check, and event fetch) so they
    /// stay in sync instead of each re-implementing the same join.
    /// </summary>
    /// <param name="loggedInAccount">The current session's account, or null when logged out.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<List<CalendarResponse>> GetVisibleOtherCalendarsAsync(AccountResponse? loggedInAccount, CancellationToken ct = default);

    /// <summary>
    /// Creates the CalendarShared row for each of the given calendars that doesn't already have one.
    /// Fetches the sharing table once and reuses it for every ID, instead of one full scan per calendar.
    /// </summary>
    Task<ServiceResult> EnsureCalendarSharedAsync(IEnumerable<long> calendarIds, CancellationToken ct = default);

    /// <summary>
    /// Returns the sharing status (ID/Name/User/Guest) of every calendar owned by
    /// <paramref name="accountEmail"/>, backfilling any missing CalendarShared row first. Backs the
    /// Management &gt; Calendar "shared calendars" grid.
    /// </summary>
    Task<List<CalendarSharedSummaryResponse>> GetCalendarSharedSummariesAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>
    /// Returns every calendar shared with registered users, alongside whether
    /// <paramref name="accountEmail"/> is already subscribed to it. Backs the "browse calendars of
    /// interest" list a user picks new subscriptions from.
    /// </summary>
    Task<List<CalendarBrowseSummaryResponse>> GetBrowseCalendarsOfInterestAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>
    /// Creates a new calendar owned by <paramref name="accountEmail"/>. The owner is taken as an
    /// explicit argument (from the authenticated session), never from the request body, so a
    /// form-bound value cannot make one account create a calendar under another's identity.
    /// </summary>
    Task<ServiceResult> CreateCalendarAsync(string accountEmail, CalendarRequest request, CancellationToken ct = default);

    /// <summary>Updates an existing calendar owned by <paramref name="accountEmail"/>.</summary>
    Task<ServiceResult> UpdateCalendarAsync(string accountEmail, CalendarRequest request, CancellationToken ct = default);

    /// <summary>Deletes a calendar owned by <paramref name="accountEmail"/> and all its events.</summary>
    Task<ServiceResult> DeleteCalendarAsync(string accountEmail, CalendarRequest request, CancellationToken ct = default);

    /// <summary>
    /// Replaces the set of other-user calendars that the account subscribes to
    /// with the provided list (full replace, not a patch).
    /// </summary>
    Task<ServiceResult> UpdateOtherCalendarAsync(string email, IEnumerable<OtherCalendarRequest> requests, CancellationToken ct = default);

    /// <summary>
    /// Bulk-updates the sharing permissions for the provided calendars.
    /// Fails validation if any requested calendar is not owned by <paramref name="email"/>.
    /// </summary>
    Task<ServiceResult> UpdateCalendarSharedAsync(string email, IEnumerable<CalendarSharedRequest> requests, CancellationToken ct = default);

    /// <summary>Creates a default calendar for <paramref name="accountEmail"/> if one does not already exist.</summary>
    Task<ServiceResult> EnsureDefaultCalendarAsync(string accountEmail, CalendarRequest request, CancellationToken ct = default);

    /// <summary>Creates a new calendar event with optional reminders and an optional file attachment.</summary>
    Task<ServiceResult> CreateCalendarEventAsync(CalendarEventRequest request, string email, string roleIndex, List<CalendarReminderDto> reminders, AttachedFileDto? attachedFile, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing calendar event, replacing its reminders with <paramref name="reminders"/> and its
    /// attachment with <paramref name="attachedFile"/> (the existing attachment is removed when none is provided).
    /// </summary>
    Task<ServiceResult> UpdateCalendarEventAsync(CalendarEventRequest request, string email, string roleIndex, List<CalendarReminderDto> reminders, AttachedFileDto? attachedFile, CancellationToken ct = default);

    /// <summary>Deletes a calendar event and its associated reminders and attachment.</summary>
    Task<ServiceResult> DeleteCalendarEventAsync(long calendarEventId, string email, CancellationToken ct = default);

    /// <summary>Validates and stores an image uploaded from the Summernote editor inside a calendar event description.</summary>
    Task<SummernoteUploadResult> UploadSummernoteImageAsync(AttachedFileDto file, string roleIndex, CancellationToken ct = default);

    /// <summary>
    /// Opens the attachment of calendar event <paramref name="calendarEventId"/> for <paramref name="viewer"/>
    /// (null for an anonymous visitor). The event must exist and belong to a calendar the viewer may see —
    /// their own or a still-shared one they subscribe to, or for an anonymous visitor one shared with
    /// anonymous visitors; otherwise nothing is opened and the result says why. On success the file is
    /// streamed from file storage.
    /// </summary>
    Task<(ServiceResult Result, AttachmentDownload? File)> OpenCalendarEventAttachedFileAsync(
        long calendarEventId, AccountResponse? viewer, CancellationToken ct = default);

    /// <summary>
    /// Prepares HTML content for display by downloading each inline Summernote image,
    /// embedding it as Base64 in <c>data-file</c> / <c>data-contenttype</c> attributes,
    /// and replacing the stored file path in <c>alt</c> with its RSA-encrypted form (an image that
    /// cannot be downloaded is removed). Returns the transformed HTML and a flag indicating whether
    /// any images were present.
    /// </summary>
    Task<(string Html, bool HasImages)> PrepareHtmlForDisplayAsync(string html, CancellationToken ct = default);
}

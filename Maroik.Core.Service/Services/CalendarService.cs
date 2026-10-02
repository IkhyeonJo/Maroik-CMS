using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.Domain.ValueObjects;
using Maroik.Core.Service.Extensions;
using Maroik.Core.Service.Mappers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Implementation of <see cref="ICalendarService"/> covering the full calendar feature set:
/// calendars, events, reminders, file attachments, sharing, and subscriptions.
/// Event attachments (zip only, per <c>AttachmentUploadPolicy</c>) are stored in and retrieved from the
/// file-storage microservice, which virus-scans them with ClamAV; Summernote inline images in an event
/// description go through <see cref="IAttachmentContentService"/> (image-validated before upload).
/// All multistep write operations use <see cref="IUnitOfWork"/> transactions.
/// </summary>
public class CalendarService(
    ICalendarRepository calendarRepository,
    ICalendarEventRepository calendarEventRepository,
    ICalendarEventAttachedFileRepository calendarEventAttachedFileRepository,
    ICalendarEventReminderRepository calendarEventReminderRepository,
    ICalendarSharedRepository calendarSharedRepository,
    IOtherCalendarRepository otherCalendarRepository,
    IFileClient fileClient,
    IUnitOfWork unitOfWork,
    IAttachmentContentService attachmentContent,
    IOptions<ServerSetting> settings,
    ILogger<CalendarService> logger,
    TimeProvider timeProvider) : ICalendarService
{
    /// <summary>
    /// The per-account unique calendar-name constraint. Calendar also has a surrogate-key
    /// "Calendar_pk" (on ID) that raises the same SQLSTATE 23505 on a PK collision; naming this
    /// constraint explicitly (via IsPostgresUniqueViolationOn) keeps that unrelated failure mode from
    /// being misreported as "name already exists" below.
    /// </summary>
    private const string NameUniqueConstraint = "Calendar_AccountEmail_Name_unique";

    /// <summary>CalendarShared's primary-key constraint (on CalendarId), for classifying a concurrent-insert race.</summary>
    private const string SharedPkConstraint = "CalendarShared_pk";

    /// <summary>
    /// Bound on retries for a CalendarShared "ensure/upsert" transaction that hits a concurrent
    /// insert on <see cref="SharedPkConstraint"/>. Postgres aborts the whole transaction on any
    /// statement error, so a caught unique-violation cannot be handled in place -- the fix is to
    /// roll back and retry, re-reading which ids are still missing.
    /// </summary>
    private const int SharedRaceRetryLimit = 3;

    /// <inheritdoc />
    public async Task<List<CalendarResponse>> GetCalendarsAsync(string email, CancellationToken ct = default)
        =>
        [
            .. (await calendarRepository.GetByAccountEmailAsync(email, ct)).OrderBy(c => c.Name)
            .Select(CalendarMapper.ToResponse)
        ];

    /// <inheritdoc />
    public async Task<List<CalendarResponse>> GetAllCalendarsAsync(CancellationToken ct = default)
        => [.. (await calendarRepository.GetAllOrderedByNameAsync(ct)).Select(CalendarMapper.ToResponse)];

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<long, List<CalendarEventResponse>>> GetCalendarEventsAsync(IEnumerable<long> calendarIds, CancellationToken ct = default)
        => (await calendarEventRepository.GetByCalendarIdsAsync(calendarIds, ct))
            .Select(CalendarEventMapper.ToResponse)
            .GroupBy(e => e.CalendarId)
            .ToDictionary(g => g.Key, g => g.ToList());

    /// <inheritdoc />
    public async Task<CalendarEventResponse?> GetCalendarEventAsync(long id, CancellationToken ct = default)
    {
        CalendarEvent? evt = await calendarEventRepository.FindByIdAsync(id, ct);
        return evt == null ? null : CalendarEventMapper.ToResponse(evt);
    }

    /// <inheritdoc />
    public async Task<CalendarEventAttachedFileDto?> GetCalendarEventAttachedFileAsync(long calendarEventId, CancellationToken ct = default)
    {
        CalendarEventAttachedFile? file = await calendarEventAttachedFileRepository.FindByCalendarEventIdAsync(calendarEventId, ct);
        return file == null ? null : CalendarEventAttachedFileMapper.ToResponse(file);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CalendarEventReminderDto>> GetCalendarEventRemindersAsync(long calendarEventId, CancellationToken ct = default)
        => (await calendarEventReminderRepository.GetByCalendarEventIdAsync(calendarEventId, ct)).Select(CalendarEventReminderMapper.ToResponse);

    /// <inheritdoc />
    public async Task<List<OtherCalendarResponse>> GetOtherCalendarsAsync(string email, CancellationToken ct = default)
    {
        List<OtherCalendarResponse> subscriptions =
            [.. (await otherCalendarRepository.GetByAccountEmailAsync(email, ct)).Select(OtherCalendarMapper.ToResponse)];

        // Re-check current sharing status at read time (not just at unsubscribe/unshare time) so a
        // calendar that was turned private stops showing up here even if the subscription-cleanup
        // in UpdateCalendarSharedAsync failed to remove the stale OtherCalendar row.
        HashSet<long> currentlySharedCalendarIds =
            [.. (await calendarSharedRepository.GetAllAsync(ct)).Where(s => s.User).Select(s => s.Id)];

        return [.. subscriptions.Where(s => currentlySharedCalendarIds.Contains(s.CalendarId))];
    }

    /// <inheritdoc />
    public async Task<List<CalendarSharedResponse>> GetCalendarSharedAsync(CancellationToken ct = default)
        => [.. (await calendarSharedRepository.GetAllAsync(ct)).Select(CalendarSharedMapper.ToResponse)];

    /// <inheritdoc />
    public async Task<List<CalendarResponse>> GetVisibleOtherCalendarsAsync(AccountResponse? loggedInAccount, CancellationToken ct = default)
    {
        List<CalendarResponse> allCalendars = await GetAllCalendarsAsync(ct);

        if (loggedInAccount != null)
        {
            List<OtherCalendarResponse> subscriptions = await GetOtherCalendarsAsync(loggedInAccount.Email!, ct);
            return
            [
                .. (from c in allCalendars
                    join oc in subscriptions on c.Id equals oc.CalendarId
                    select new CalendarResponse { Id = c.Id, Name = c.Name, HtmlColorCode = c.HtmlColorCode })
                .OrderBy(x => x.Name)
            ];
        }

        List<CalendarSharedResponse> anonymousShares = [.. (await GetCalendarSharedAsync(ct)).Where(x => x.Anonymous)];
        return
        [
            .. (from c in allCalendars
                join cs in anonymousShares on c.Id equals cs.CalendarId
                select new CalendarResponse { Id = c.Id, Name = c.Name, HtmlColorCode = c.HtmlColorCode })
            .OrderBy(x => x.Name)
        ];
    }

    /// <inheritdoc />
    public async Task<ServiceResult> EnsureCalendarSharedAsync(IEnumerable<long> calendarIds, CancellationToken ct = default)
    {
        List<long> ids = [.. calendarIds];

        // The read + the inserts run in one transaction, but that alone does not stop two
        // concurrent callers from both passing the "not present" check for the same id under
        // Postgres's default READ COMMITTED isolation (a plain SELECT takes no lock) and then
        // racing on the CalendarShared primary key. Retry on that specific race: Postgres aborts
        // the whole transaction on any statement error, so the conflicting insert cannot simply be
        // skipped in place -- roll back and re-read, which will see the other caller's now-committed
        // row and only attempt whatever ids are still actually missing.
        for (int attempt = 0; attempt < SharedRaceRetryLimit; attempt++)
        {
            await unitOfWork.BeginAsync(ct);
            try
            {
                // Single fetch of the whole table, reused for every ID instead of one full scan per calendar.
                List<CalendarShared> shares = await calendarSharedRepository.GetAllAsync(ct);
                HashSet<long> existingIds = [.. shares.Select(s => s.Id)];

                foreach (long id in ids.Where(id => !existingIds.Contains(id)))
                    await calendarSharedRepository.CreateAsync(CalendarShared.CreatePrivate(id), ct);

                await unitOfWork.CommitAsync(ct);
                return ServiceResult.Ok();
            }
            catch (Exception ex) when (ex.IsPostgresUniqueViolationOn(SharedPkConstraint))
            {
                await unitOfWork.RollbackAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ensure calendar shared records for calendars {CalendarIds}", string.Join(",", ids));
                await unitOfWork.RollbackAsync(ct);
                return UnexpectedFailure;
            }
        }

        logger.LogError("Failed to ensure calendar shared records for calendars {CalendarIds} after {Retries} retries",
            string.Join(",", ids), SharedRaceRetryLimit);
        return UnexpectedFailure;
    }

    /// <inheritdoc />
    public async Task<List<CalendarSharedSummaryResponse>> GetCalendarSharedSummariesAsync(string accountEmail, CancellationToken ct = default)
    {
        List<CalendarResponse> calendars = await GetCalendarsAsync(accountEmail, ct);

        // A CalendarShared row may not exist yet for calendars created before sharing was
        // configured, so backfill any missing ones (single query) before reading the list below.
        await EnsureCalendarSharedAsync(calendars.Select(c => c.Id), ct);

        List<CalendarSharedResponse> allShared = await GetCalendarSharedAsync(ct);

        return
        [
            .. (from calendar in calendars
                join sharedCalendar in allShared on calendar.Id equals sharedCalendar.CalendarId
                select new CalendarSharedSummaryResponse
                {
                    Id = sharedCalendar.CalendarId,
                    Name = calendar.Name,
                    User = sharedCalendar.User,
                    Guest = sharedCalendar.Anonymous
                })
            .OrderBy(x => x.Name)
        ];
    }

    /// <inheritdoc />
    public async Task<List<CalendarBrowseSummaryResponse>> GetBrowseCalendarsOfInterestAsync(string accountEmail, CancellationToken ct = default)
    {
        List<CalendarResponse> allCalendars = await GetAllCalendarsAsync(ct);
        List<CalendarSharedResponse> userSharedCalendars = [.. (await GetCalendarSharedAsync(ct)).Where(x => x.User)];
        List<OtherCalendarResponse> subscriptions = await GetOtherCalendarsAsync(accountEmail, ct);

        return
        [
            .. (from calendar in allCalendars
                join calendarShared in userSharedCalendars on calendar.Id equals calendarShared.CalendarId
                join subscription in subscriptions on calendar.Id equals subscription.CalendarId into subscriptionGroup
                from subscription in subscriptionGroup.DefaultIfEmpty()
                select new CalendarBrowseSummaryResponse
                {
                    Id = calendar.Id,
                    Name = calendar.Name,
                    Checked = subscription != null
                })
            .OrderBy(x => x.Name)
        ];
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateCalendarAsync(string accountEmail, CalendarRequest request, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.BeginAsync(ct);
        try
        {
            List<Calendar> calendars = await calendarRepository.GetByAccountEmailAsync(accountEmail, ct);

            // Prevent duplicate calendar names for the same account. This is a read-then-write
            // check: two concurrent creates of the same name can both pass it under Postgres's
            // default READ COMMITTED isolation, since a plain SELECT takes no lock. The
            // "Calendar_AccountEmail_Name_unique" constraint (caught below) is what actually
            // closes the race; this check only avoids the round trip in the common case.
            if (calendars.Any(c => c.Name == request.Name))
                return await unitOfWork.FailAsync(ServiceResult.Conflict("Calendar.NameExists", "The calendar already exists."), ct);

            var calendarResult = Calendar.Create(accountEmail, request.Name, request.Description, request.TimeZoneIanaId, request.HtmlColorCode, utcNow);
            if (calendarResult.IsError)
                return await unitOfWork.FailAsync(calendarResult.FirstError, ct);

            var calendar = calendarResult.Value;
            await calendarRepository.CreateAsync(calendar, ct);
            await unitOfWork.CommitAsync(ct);

            // The aggregate is built with ID 0 and the database generates the real one, so hand it back on
            // the request: the client builds the new calendar's checkbox / edit / delete controls from the
            // id the controller echoes (main returned the persisted entity, id included). Names are unique
            // per account (Calendar_AccountEmail_Name_unique), so the row is found by name.
            request.Id = (await calendarRepository.GetByAccountEmailAsync(accountEmail, ct))
                .FirstOrDefault(c => c.Name == calendar.Name)?.Id ?? 0;
            return ServiceResult.Ok();
        }
        catch (Exception ex) when (ex.IsPostgresUniqueViolationOn(NameUniqueConstraint))
        {
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Conflict("Calendar.NameExists", "The calendar already exists.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create calendar {CalendarName} for account {AccountEmail}", request.Name, accountEmail);
            await unitOfWork.RollbackAsync(ct);
            return UnexpectedFailure;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateCalendarAsync(string accountEmail, CalendarRequest request, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.BeginAsync(ct);
        try
        {
            // FOR UPDATE: lock the row for the rest of the transaction so a concurrent update from
            // another session on the same calendar (e.g. two open tabs) cannot commit between this
            // read and this transaction's write and have its change silently overwritten by
            // UpdateEntityAsync's full-row snapshot below.
            Calendar? previous = await calendarRepository.FindByIdForUpdateAsync(request.Id, ct);
            if (previous == null || previous.AccountEmail.Value != accountEmail)
                return await unitOfWork.FailAsync(ServiceResult.NotFound("Calendar.NotFound", "The calendar could not be found."), ct);

            var updateResult = previous.Update(request.Name, request.Description, request.TimeZoneIanaId, request.HtmlColorCode, utcNow);
            if (updateResult.IsError)
                return await unitOfWork.FailAsync(updateResult.FirstError, ct);

            await calendarRepository.UpdateEntityAsync(previous, ct);
            await unitOfWork.CommitAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception ex) when (ex.IsPostgresUniqueViolationOn(NameUniqueConstraint))
        {
            // Renaming to a name already used by another of this account's calendars hits the
            // same "Calendar_AccountEmail_Name_unique" constraint CreateCalendarAsync guards
            // against -- report the same friendly conflict instead of the generic fallback below.
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Conflict("Calendar.NameExists", "The calendar already exists.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update calendar {CalendarId} for account {AccountEmail}", request.Id, accountEmail);
            await unitOfWork.RollbackAsync(ct);
            return UnexpectedFailure;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteCalendarAsync(string accountEmail, CalendarRequest request, CancellationToken ct = default)
    {
        await unitOfWork.BeginAsync(ct);
        try
        {
            List<Calendar> calendars = await calendarRepository.GetByAccountEmailAsync(accountEmail, ct);
            Calendar? existing = calendars.FirstOrDefault(c => c.Id == request.Id);
            if (existing == null)
                return await unitOfWork.FailAsync(ServiceResult.NotFound("Calendar.NotFound", "The calendar could not be found."), ct);

            await calendarRepository.DeleteByIdAsync(existing.Id, ct);
            await unitOfWork.CommitAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete calendar {CalendarId} for account {AccountEmail}", request.Id, accountEmail);
            await unitOfWork.RollbackAsync(ct);
            return UnexpectedFailure;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> EnsureDefaultCalendarAsync(string accountEmail, CalendarRequest request, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.BeginAsync(ct);
        try
        {
            List<Calendar> calendars = await calendarRepository.GetByAccountEmailAsync(accountEmail, ct);

            // Create the default calendar only when the user has none yet (e.g., first login).
            if (calendars.Count != 0)
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.Ok();
            }

            // This first-visit auto-provisioning must always succeed so the calendar grid is never
            // permanently empty — fall back to UTC if the account's own TimeZoneIanaId is missing
            // or unrecognized, rather than letting Calendar.Create's normal (and deliberately
            // strict) validation silently fail an action the user didn't request.
            string timeZoneIanaId = request.TimeZoneIanaId ?? "";
            if (TimeZoneId.Create(timeZoneIanaId).IsError)
                timeZoneIanaId = "UTC";

            var calendarResult = Calendar.Create(accountEmail, request.Name, request.Description, timeZoneIanaId, request.HtmlColorCode, utcNow);
            if (calendarResult.IsError)
                return await unitOfWork.FailAsync(calendarResult.FirstError, ct);

            var calendar = calendarResult.Value;
            await calendarRepository.CreateAsync(calendar, ct);
            await unitOfWork.CommitAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception ex) when (ex.IsPostgresUniqueViolationOn(NameUniqueConstraint))
        {
            // A concurrent request (e.g. two tabs opened on first login) already created this
            // account's default calendar. This method's contract is "some calendar now exists" --
            // already satisfied -- so treat the race as success rather than surfacing a failure.
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ensure default calendar for account {AccountEmail}", accountEmail);
            await unitOfWork.RollbackAsync(ct);
            return UnexpectedFailure;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateOtherCalendarAsync(string email, IEnumerable<OtherCalendarRequest> requests, CancellationToken ct = default)
    {
        List<OtherCalendarRequest> requestList = [.. requests];

        await unitOfWork.BeginAsync(ct);
        try
        {
            // A subscription must only ever be created for a calendar its owner has explicitly
            // shared with Users (CalendarShared.User) — otherwise any account could grant
            // itself read access to any calendar's events by supplying an arbitrary CalendarId.
            // Locked (FOR UPDATE) on exactly the rows this call depends on: this serializes against
            // a concurrent UpdateCalendarSharedAsync unsharing (and deleting subscriptions for) one
            // of these same calendars, in either interleaving — whichever transaction acquires the
            // row lock first, the other waits until it commits, so a subscription can never be
            // created against a permission snapshot a concurrent unshare has already invalidated.
            HashSet<long> shareableCalendarIds =
            [
                .. (await calendarSharedRepository.GetByIdsForUpdateAsync(requestList.Select(r => r.CalendarId), ct))
                .Where(shared => shared.User)
                .Select(shared => shared.Id)
            ];

            // Replace-all strategy: delete existing subscriptions then re-insert from the new list.
            await otherCalendarRepository.DeleteByAccountEmailAsync(email, ct);

            foreach (OtherCalendarRequest req in requestList)
            {
                if (!shareableCalendarIds.Contains(req.CalendarId))
                {
                    await unitOfWork.RollbackAsync(ct);
                    return ServiceResult.Failure("Calendar.UpdateOtherCalendarsFailed", "Input is invalid");
                }

                var createResult = OtherCalendar.Create(email, req.CalendarId);
                if (createResult.IsError)
                {
                    await unitOfWork.RollbackAsync(ct);
                    return ServiceResult.FromError(createResult.FirstError);
                }
                await otherCalendarRepository.CreateAsync(createResult.Value, ct);
            }

            await unitOfWork.CommitAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update other-calendar subscriptions for account {AccountEmail}", email);
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Failure("Calendar.UpdateOtherCalendarsFailed", ServiceResult.TemporaryErrorKey);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateCalendarSharedAsync(string email, IEnumerable<CalendarSharedRequest> requests, CancellationToken ct = default)
    {
        var calendarSharedRequests = requests.ToList();

        List<Calendar> ownedCalendars = await calendarRepository.GetByAccountEmailAsync(email, ct);
        if (calendarSharedRequests.Any(req => ownedCalendars.All(c => c.Id != req.CalendarId)))
        {
            return ServiceResult.Validation("CalendarShared.InvalidTarget", "Input is invalid");
        }

        // As in EnsureCalendarSharedAsync, the unlocked read below cannot stop a concurrent caller
        // (another UpdateCalendarSharedAsync/EnsureCalendarSharedAsync) from creating the same row
        // between our read and our insert. Postgres aborts the whole transaction on that error, so
        // retry: roll back and re-read, which will see the other caller's row and take the Update
        // branch for it instead of re-inserting, still applying this call's requested values.
        for (int attempt = 0; attempt < SharedRaceRetryLimit; attempt++)
        {
            await unitOfWork.BeginAsync(ct);
            try
            {
                // Upsert: a CalendarShared row may not exist yet for a calendar being shared for the
                // first time (EnsureCalendarSharedAsync is not guaranteed to have run). UpdateEntityAsync
                // throws when the row is missing, which previously failed the whole request.
                HashSet<long> existingShareIds = [.. (await calendarSharedRepository.GetAllAsync(ct)).Select(s => s.Id)];

                foreach (CalendarSharedRequest req in calendarSharedRequests)
                {
                    CalendarShared shared = CalendarShared.CreatePrivate(req.CalendarId).Update(req.User, req.Anonymous);
                    if (existingShareIds.Contains(req.CalendarId))
                        await calendarSharedRepository.UpdateEntityAsync(shared, ct);
                    else
                        await calendarSharedRepository.CreateAsync(shared, ct);
                }

                // When a calendar is set to non-user-visible, remove ALL existing subscriptions to it
                // (every account that had subscribed), not just one — otherwise leftover subscription
                // rows silently reactivate the moment the calendar is shared again. One batched DELETE
                // per calendar instead of one round trip per subscriber.
                foreach (CalendarSharedRequest req in calendarSharedRequests.Where(r => !r.User))
                {
                    await otherCalendarRepository.DeleteByCalendarIdAsync(req.CalendarId, ct);
                }

                await unitOfWork.CommitAsync(ct);
                return ServiceResult.Ok();
            }
            catch (Exception ex) when (ex.IsPostgresUniqueViolationOn(SharedPkConstraint))
            {
                await unitOfWork.RollbackAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update calendar shared settings");
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.Failure("Calendar.UpdateSharedFailed", ServiceResult.TemporaryErrorKey);
            }
        }

        logger.LogError("Failed to update calendar shared settings after {Retries} retries", SharedRaceRetryLimit);
        return ServiceResult.Failure("Calendar.UpdateSharedFailed", ServiceResult.TemporaryErrorKey);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateCalendarEventAsync(
        CalendarEventRequest request, string email, string roleIndex,
        List<CalendarReminderDto> reminders, AttachedFileDto? attachedFile, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var fileError = attachmentContent.ValidateAttachedFile(attachedFile);
        if (fileError != null) return fileError;

        string description = attachmentContent.SanitizeAndDecryptContent(request.Description ?? "");

        await unitOfWork.BeginAsync(ct);
        try
        {
            List<Calendar> calendars = await calendarRepository.GetByAccountEmailAsync(email, ct);

            // Verify the target calendar belongs to the requesting user.
            if (calendars.All(c => c.Id != request.CalendarId))
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.NotFound("Calendar.NotFound", "The calendar does not exists.");
            }

            var eventResult = CalendarEvent.Create(request.CalendarId, request.Title, description,
                request.AllDay, request.StartDate, request.EndDate,
                request.StartDateTimeZoneIanaId, request.EndDateTimeZoneIanaId,
                request.Location, request.Status, utcNow, request.RecurrenceId);
            if (eventResult.IsError)
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.FromError(eventResult.FirstError);
            }

            var calendarEvent = eventResult.Value;
            long eventId = await calendarEventRepository.CreateCalendarEventAsync(calendarEvent, ct);

            if (attachedFile != null)
            {
                var attachmentResult = await SaveEventAttachmentAsync(roleIndex, eventId, attachedFile, ct);
                if (!attachmentResult.Success)
                {
                    await unitOfWork.RollbackAsync(ct);
                    return attachmentResult;
                }
            }

            var remindersResult = await CreateEventRemindersAsync(eventId, reminders, ct);
            if (!remindersResult.Success)
            {
                await unitOfWork.RollbackAsync(ct);
                return remindersResult;
            }

            await unitOfWork.CommitAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create calendar event for calendar {CalendarId} and account {AccountEmail}", request.CalendarId, email);
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Failure("CalendarEvent.CreateFailed", ServiceResult.TemporaryErrorKey);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateCalendarEventAsync(
        CalendarEventRequest request, string email, string roleIndex,
        List<CalendarReminderDto> reminders, AttachedFileDto? attachedFile, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var fileError = attachmentContent.ValidateAttachedFile(attachedFile);
        if (fileError != null) return fileError;

        string description = attachmentContent.SanitizeAndDecryptContent(request.Description ?? "");

        await unitOfWork.BeginAsync(ct);
        try
        {
            List<Calendar> calendars = await calendarRepository.GetByAccountEmailAsync(email, ct);

            // Verify the target calendar belongs to the requesting user.
            if (calendars.All(c => c.Id != request.CalendarId))
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.NotFound("Calendar.NotFound", "The calendar does not exists.");
            }

            // Row lock (FOR UPDATE) held until commit/rollback below, so a concurrent update to the
            // same event (e.g. a double-submit/retry) is fully serialized instead of both requests
            // reading the same pre-update reminder/attachment rows and each inserting its own
            // replacement set on top of the other's.
            CalendarEvent? existing = await calendarEventRepository.FindByIdForUpdateAsync(request.Id, ct);
            // Verify the existing event currently belongs to one of the caller's calendars
            // before allowing any mutation or reassignment.
            if (existing == null || existing.Id <= 0 || calendars.All(c => c.Id != existing.CalendarId))
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.Validation("CalendarEvent.InvalidRequest", "Input is invalid");
            }

            var updateResult = existing.Update(request.Title, description, request.AllDay,
                request.StartDate, request.EndDate,
                request.StartDateTimeZoneIanaId, request.EndDateTimeZoneIanaId,
                request.Location, request.Status, utcNow);
            if (updateResult.IsError)
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.FromError(updateResult.FirstError);
            }

            existing.Reassign(request.CalendarId, request.RecurrenceId, utcNow);
            await calendarEventRepository.UpdateEntityAsync(existing, ct);

            // Replace attachment: delete existing record then optionally upload and re-create.
            await calendarEventAttachedFileRepository.DeleteByCalendarEventIdAsync(existing.Id, ct);
            if (attachedFile != null)
            {
                var attachmentResult = await SaveEventAttachmentAsync(roleIndex, existing.Id, attachedFile, ct);
                if (!attachmentResult.Success)
                {
                    await unitOfWork.RollbackAsync(ct);
                    return attachmentResult;
                }
            }

            // Replace reminders: delete all existing then re-create from the updated list.
            await calendarEventReminderRepository.DeleteByCalendarEventIdAsync(existing.Id, ct);
            var remindersResult = await CreateEventRemindersAsync(existing.Id, reminders, ct);
            if (!remindersResult.Success)
            {
                await unitOfWork.RollbackAsync(ct);
                return remindersResult;
            }

            await unitOfWork.CommitAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update calendar event {EventId} for account {AccountEmail}", request.Id, email);
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Failure("CalendarEvent.UpdateFailed", ServiceResult.TemporaryErrorKey);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteCalendarEventAsync(long calendarEventId, string email, CancellationToken ct = default)
    {
        await unitOfWork.BeginAsync(ct);
        try
        {
            List<Calendar> calendars = await calendarRepository.GetByAccountEmailAsync(email, ct);
            if (calendars.Count == 0)
                return await unitOfWork.FailAsync(ServiceResult.NotFound("Calendar.None", "No calendar exists."), ct);

            CalendarEvent? evt = await calendarEventRepository.FindByIdAsync(calendarEventId, ct);

            // Ensure the event exists and belongs to one of the requesting user's calendars.
            if (evt == null || calendars.All(c => c.Id != evt.CalendarId))
                return await unitOfWork.FailAsync(ServiceResult.NotFound("CalendarEvent.NotFound", "The calendar event could not be found."), ct);

            await calendarEventRepository.DeleteByIdAsync(evt.Id, ct);
            await unitOfWork.CommitAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete calendar event {EventId} for account {AccountEmail}", calendarEventId, email);
            await unitOfWork.RollbackAsync(ct);
            return UnexpectedFailure;
        }
    }

    /// <summary>Generic failure returned (after logging) when a calendar write throws unexpectedly.</summary>
    private static ServiceResult UnexpectedFailure => ServiceResult.Failure(
        "Calendar.Unexpected", ServiceResult.TemporaryErrorKey);

    /// <summary>
    /// Builds the remote storage path for a calendar event attachment
    /// (<c>upload/Calendar/{roleIndex}/calendarEventAttachedFiles/{eventId}/{GUID}{ext}</c>). The original
    /// file name is replaced by an upper-case GUID (only its extension is kept), so names cannot collide
    /// or traverse paths, and each event gets its own folder.
    /// </summary>
    private static string BuildEventFilePath(string roleIndex, long eventId, string fileName)
    {
        string guid = Guid.NewGuid().ToString().ToUpper();
        string ext = Path.GetExtension(fileName);
        // A storage key, not a local path: always "/"-separated, whatever OS this runs on.
        return $"upload/Calendar/{roleIndex}/calendarEventAttachedFiles/{eventId}/{guid}{ext}";
    }

    /// <summary>
    /// Uploads <paramref name="attachedFile"/> to file storage and creates its
    /// <see cref="CalendarEventAttachedFile"/> record for <paramref name="calendarEventId"/>.
    /// Used by both create and update (the caller is responsible for deleting any prior
    /// attachment record before calling this on update).
    /// </summary>
    private async Task<ServiceResult> SaveEventAttachmentAsync(string roleIndex, long calendarEventId, AttachedFileDto attachedFile, CancellationToken ct)
    {
        string filePath = BuildEventFilePath(roleIndex, calendarEventId, attachedFile.FileName);
        var fileResult = CalendarEventAttachedFile.Create(calendarEventId, attachedFile.Size,
            Path.GetFileNameWithoutExtension(attachedFile.FileName),
            Path.GetExtension(attachedFile.FileName),
            filePath.Replace('\\', '/')); // Normalize path separators for URL use.
        if (fileResult.IsError)
            return ServiceResult.FromError(fileResult.FirstError);

        // Validated first, uploaded second: a record the domain rejects must not leave an orphaned file.
        bool uploaded = await fileClient.UploadAsync(attachedFile.Bytes, attachedFile.ContentType, filePath, settings.Value.FileStorageBaseUrl ?? "", ct);
        if (!uploaded)
            return ServiceResult.Failure("CalendarEvent.AttachmentUploadFailed", ServiceResult.TemporaryErrorKey);

        await calendarEventAttachedFileRepository.CreateAsync(fileResult.Value, ct);
        return ServiceResult.Ok();
    }

    /// <summary>
    /// Creates a <see cref="CalendarEventReminder"/> for each entry in <paramref name="reminders"/> for
    /// <paramref name="calendarEventId"/>. Any invalid reminder (empty method, wrong lead-time field
    /// count, or an out-of-range lead time — see CalendarEventReminder.Create / CalendarReminderPolicy)
    /// stops immediately so the caller can roll back instead of silently dropping a reminder.
    /// </summary>
    private async Task<ServiceResult> CreateEventRemindersAsync(long calendarEventId, List<CalendarReminderDto> reminders, CancellationToken ct)
    {
        foreach (var reminderResult in reminders.Select(reminderDto => CalendarEventReminder.Create(calendarEventId, reminderDto.Method,
                     reminderDto.MinutesBeforeEvent, reminderDto.HoursBeforeEvent, reminderDto.DaysBeforeEvent, reminderDto.WeeksBeforeEvent,
                     reminderDto.TimesBeforeEvent)))
        {
            if (reminderResult.IsError)
                return ServiceResult.FromError(reminderResult.FirstError);

            await calendarEventReminderRepository.CreateAsync(reminderResult.Value, ct);
        }

        return ServiceResult.Ok();
    }

    /// <inheritdoc />
    public Task<SummernoteUploadResult> UploadSummernoteImageAsync(AttachedFileDto file, string roleIndex, CancellationToken ct = default)
        => attachmentContent.UploadSummernoteImageAsync(file, "Calendar", roleIndex, ct);

    /// <inheritdoc />
    public Task<byte[]?> DownloadFileAsync(string filePath, CancellationToken ct = default)
        => attachmentContent.DownloadFileAsync(filePath, ct);

    /// <inheritdoc />
    public Task<(string Html, bool HasImages)> PrepareHtmlForDisplayAsync(string html, CancellationToken ct = default)
        => attachmentContent.PrepareHtmlForDisplayAsync(html, ct);
}

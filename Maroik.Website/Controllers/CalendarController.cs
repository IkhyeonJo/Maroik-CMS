using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.Domain.Media;
using Maroik.Website.Attributes;
using Maroik.Website.Extensions;
using Maroik.Website.Mappings;
using Maroik.Website.Models.ViewModels.Calendar;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace Maroik.Website.Controllers;

/// <summary>
/// Manages calendars and calendar events: CRUD, sharing, subscriptions,
/// Summernote image uploads, and role-differentiated index views.
/// </summary>
public class CalendarController(
    IHtmlLocalizer<CalendarController> localizer,
    ILogger<CalendarController> logger,
    ICalendarService calendarService,
    IRsaService rsa,
    IOptions<ServerSetting> serverSettings) : Controller
{
    /// <summary>Shared extension-to-MIME-type lookup for attachment downloads (safe for concurrent reads).</summary>
    private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    #region Calendar

    #region Create

    #region Calendar
    /// <summary>Creates a new calendar.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> CreateCalendar([FromBody] CalendarInputViewModel calendarInputViewModel)
    {
        try
        {
            if (!ModelState.IsValid)
                return Json(new { result = false, error = localizer["Input is invalid"].Value });

            CalendarRequest tempCalendar = calendarInputViewModel.Calendars.FirstOrDefault() ?? new CalendarRequest();
            string accountEmail = ((AccountResponse)ViewBag.LoggedInAccount).Email!;

            ServiceResult result = await calendarService.CreateCalendarAsync(accountEmail, tempCalendar, HttpContext.RequestAborted);

            if (!result.Success)
                return Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });

            // Strip fields the client doesn't need back (and shouldn't see echoed), keeping the
            // JSON payload limited to what the calendar picker UI uses to render the new entry.
            tempCalendar.AccountEmail = null;
            tempCalendar.Description = null;
            tempCalendar.TimeZoneIanaId = null;
            return Json(new { result = true, message = localizer["The calendar has been successfully created."].Value, calendar = tempCalendar });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create calendar for account {AccountEmail}", ((AccountResponse)ViewBag.LoggedInAccount).Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }
    #endregion

    #region Calendar Event
    /// <summary>Creates a new calendar event (with optional file attachment and reminders).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> CreateCalendarEvent([FromForm] CalendarEventInputViewModel calendarEventInputViewModel)
    {
        try
        {
            if (!ModelState.IsValid)
                return Json(new { result = false, error = localizer["Input is invalid"].Value });

            calendarEventInputViewModel.Description ??= "";

            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;

            if (loggedInAccount.Role is not (Role.Admin or Role.User))
                return Json(new { result = false, error = localizer["Please Login to create calendar event"].Value });

            ServiceResult createEventResult = await calendarService.CreateCalendarEventAsync(
                calendarEventInputViewModel.ToCalendarEventRequest(),
                loggedInAccount.Email!,
                $"{loggedInAccount.Role}Index",
                calendarEventInputViewModel.SerializedCalendarReminders.ToReminderInfoList(),
                await calendarEventInputViewModel.CalendarEventUploadedFile.ToAttachedFileInfoAsync(serverSettings.Value.MaxAttachedFileSizeBytes, HttpContext.RequestAborted),
                HttpContext.RequestAborted);

            return !createEventResult.Success ? Json(new { result = false, error = localizer[createEventResult.ErrorKey, createEventResult.ErrorArgs].ToPlainString() }) : Json(new { result = true, message = localizer["The calendar event has been successfully created."].Value });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create calendar event for account {AccountEmail}", ((AccountResponse)ViewBag.LoggedInAccount).Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }
    #endregion

    #region Summernote Image File Upload
    /// <summary>
    /// Validates and stores a Summernote inline image for a calendar event description, returning its bytes
    /// (base64) and content type for the editor preview plus its RSA-encrypted storage path (kept in the image's <c>alt</c>).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> UploadImageFile(IFormFile? summernoteImageFile)
    {
        if (summernoteImageFile == null)
            return Ok(new { result = false, errorMessage = localizer["Please attach a file."].Value });

        // A refused upload is a security event: record who sent what (the attachment service logs its own refusals).
        AccountResponse loggedInAccount = ViewBag.LoggedInAccount;
        string uploaderEmail = loggedInAccount.Email!;
        if (summernoteImageFile.Length <= 0 || summernoteImageFile.Length > serverSettings.Value.MaxAttachedFileSizeBytes)
        {
            logger.LogWarning("Editor image upload refused: size {Size} bytes is outside the {MaxBytes}-byte limit for {Email}",
                summernoteImageFile.Length, serverSettings.Value.MaxAttachedFileSizeBytes, uploaderEmail);
            return Ok(new { result = false, errorMessage = localizer["File Size must be smaller than {0}MB.", serverSettings.Value.MaxAttachedFileSizeBytes / (1024 * 1024)].ToPlainString() });
        }

        var ext = Path.GetExtension(summernoteImageFile.FileName).ToLowerInvariant();
        if (!ImageUploadPolicy.IsAllowedExtension(ext))
        {
            logger.LogWarning("Editor image upload refused: extension not allowed ({Extension}) for {Email}", ext, uploaderEmail);
            return Ok(new { result = false, errorMessage = localizer["Only .jpg or jpeg or .png file allowed."].Value });
        }

        string roleIndex = $"{loggedInAccount.Role}Index";

        AttachedFileDto file = (await summernoteImageFile.ToAttachedFileInfoAsync(serverSettings.Value.MaxAttachedFileSizeBytes, HttpContext.RequestAborted))!;

        SummernoteUploadResult uploadResult = await calendarService.UploadSummernoteImageAsync(file, roleIndex, uploaderEmail, HttpContext.RequestAborted);

        if (!uploadResult.Success)
            return Ok(new { result = false, errorMessage = localizer[uploadResult.ErrorKey ?? "Input is invalid"].Value });

        // The Summernote editor only needs the raw bytes (base64) + content type to build an object
        // URL for the inserted <img>; return those explicitly rather than serializing a whole
        // FileContentResult and relying on its property names (matches Forum / Management).
        return Ok(new
        {
            result = true,
            file = new
            {
                fileContents = Convert.ToBase64String(uploadResult.FileBytes!),
                contentType = uploadResult.ContentType!
            },
            filePath = rsa.Encrypt(uploadResult.FilePath!)
        });
    }
    #endregion

    #endregion

    #region Read

    /// <summary>Returns the admin calendar view.</summary>
    [HttpGet]
    public async Task<IActionResult> AdminIndex()
    {
        AccountResponse loggedInAccount = ViewBag.LoggedInAccount;
        string userTimezone = loggedInAccount.TimeZoneIanaId ?? "UTC";

        List<CalendarResponse> calendars = await calendarService.GetCalendarsAsync(loggedInAccount.Email!, HttpContext.RequestAborted);
        if (calendars.Count == 0)
        {
            // First visit for this account: auto-provision a default personal calendar
            // so the grid is never empty and events can be created immediately.
            await calendarService.EnsureDefaultCalendarAsync(loggedInAccount.Email!, new CalendarRequest
            {
                Name = loggedInAccount.Nickname!,
                TimeZoneIanaId = loggedInAccount.TimeZoneIanaId,
                HtmlColorCode = "#fc330e"
            }, HttpContext.RequestAborted);
            calendars = await calendarService.GetCalendarsAsync(loggedInAccount.Email!, HttpContext.RequestAborted);
        }

        CalendarOutputViewModel vm = new()
        {
            Calendars = calendars,
            CalendarEventOutputViewModels = await calendarService.GetCalendarEventViewModelsAsync(calendars, userTimezone, null, HttpContext.RequestAborted),
            LoggedInAccount = loggedInAccount,
            LoggedInAccountTimeZoneIanaId = userTimezone
        };
        vm.PopulateTimeData(userTimezone);
        return View(vm);
    }

    /// <summary>Returns the user calendar view.</summary>
    [HttpGet]
    public async Task<IActionResult> UserIndex()
    {
        AccountResponse loggedInAccount = ViewBag.LoggedInAccount;
        string userTimezone = loggedInAccount.TimeZoneIanaId ?? "UTC";

        List<CalendarResponse> calendars = await calendarService.GetCalendarsAsync(loggedInAccount.Email!, HttpContext.RequestAborted);
        if (calendars.Count == 0)
        {
            // Same first-visit provisioning as AdminIndex (see comment there).
            await calendarService.EnsureDefaultCalendarAsync(loggedInAccount.Email!, new CalendarRequest
            {
                Name = loggedInAccount.Nickname!,
                TimeZoneIanaId = loggedInAccount.TimeZoneIanaId,
                HtmlColorCode = "#fc330e"
            }, HttpContext.RequestAborted);
            calendars = await calendarService.GetCalendarsAsync(loggedInAccount.Email!, HttpContext.RequestAborted);
        }

        List<CalendarResponse> otherCalendars = await calendarService.GetVisibleOtherCalendarsAsync(loggedInAccount, HttpContext.RequestAborted);

        CalendarOutputViewModel vm = new()
        {
            Calendars = calendars,
            CalendarEventOutputViewModels = await calendarService.GetCalendarEventViewModelsAsync(calendars, userTimezone, null, HttpContext.RequestAborted),
            OtherCalendars = otherCalendars,
            OtherCalendarEventOutputViewModels = await calendarService.GetCalendarEventViewModelsAsync(otherCalendars, userTimezone, null, HttpContext.RequestAborted),
            LoggedInAccount = loggedInAccount,
            LoggedInAccountTimeZoneIanaId = userTimezone
        };
        vm.PopulateTimeData(userTimezone);
        return View(vm);
    }

    /// <summary>Returns the anonymous calendar view.</summary>
    [HttpGet]
    public async Task<IActionResult> AnonymousIndex()
    {
        const string userTimezone = "UTC";
        AccountResponse loggedInAccount = new() { Role = Role.Anonymous, TimeZoneIanaId = userTimezone };

        List<CalendarResponse> otherCalendars = await calendarService.GetVisibleOtherCalendarsAsync(null, HttpContext.RequestAborted);

        CalendarOutputViewModel vm = new()
        {
            OtherCalendars = otherCalendars,
            OtherCalendarEventOutputViewModels = await calendarService.GetCalendarEventViewModelsAsync(otherCalendars, userTimezone, null, HttpContext.RequestAborted),
            LoggedInAccount = loggedInAccount,
            LoggedInAccountTimeZoneIanaId = userTimezone
        };
        vm.PopulateTimeData(userTimezone);
        return View(vm);
    }

    /// <summary>Checks whether the caller owns a calendar with the given ID, returning it when so.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> IsCalendarExists(int id)
    {
        try
        {
            var tempCalendars =
                await calendarService.GetCalendarsAsync(((AccountResponse)ViewBag.LoggedInAccount).Email!,
                    HttpContext.RequestAborted);

            if (tempCalendars.Count == 0)
            {
                return Json(new { result = false, error = localizer["No calendar exists"].Value });
            }

            var tempCalendar = tempCalendars.FirstOrDefault(a => a.Id == id);

            return tempCalendar == null
                ? Json(new { result = false, error = localizer["Input is invalid"].Value })
                : (IActionResult)Json(new { result = true, calendar = tempCalendar });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check calendar existence for id {CalendarId}", id);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Returns the list of the current user's calendars as JSON.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> GetCalendars()
    {
        try
        {
            List<CalendarResponse> calendars = await calendarService.GetCalendarsAsync(((AccountResponse)ViewBag.LoggedInAccount).Email!, HttpContext.RequestAborted);

            return Json(new { result = true, calendars });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get calendars for account {AccountEmail}", ((AccountResponse)ViewBag.LoggedInAccount).Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Returns, as JSON, the other accounts' calendars the caller can see (its subscriptions, or the anonymously shared ones).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    [RequiredHttpPostAccess(Role = Role.Anonymous)]
    public async Task<IActionResult> GetOtherCalendars()
    {
        try
        {
            // GetVisibleOtherCalendarsAsync treats a null account as anonymous (returns the calendars
            // shared with anonymous visitors); ViewBag.LoggedInAccount is never null itself, so an
            // actually-anonymous viewer is identified by Role instead and null is passed through explicitly.
            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;
            List<CalendarResponse> tempOtherCalendars = await calendarService.GetVisibleOtherCalendarsAsync(
                loggedInAccount.Role == Role.Anonymous ? null : loggedInAccount, HttpContext.RequestAborted);

            return Json(new { result = true, tempOtherCalendars });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get other calendars for account {AccountEmail}", ((AccountResponse)ViewBag.LoggedInAccount).Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Checks whether a calendar event with the given ID exists and returns its full detail.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> IsCalendarEventExists(int id)
    {
        try
        {
            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;
            List<CalendarResponse> calendars = await calendarService.GetCalendarsAsync(loggedInAccount.Email!, HttpContext.RequestAborted);

            CalendarEventOutputViewModel? calendarEvent = await calendarService.GetCalendarEventDetailViewModelAsync(
                calendars, loggedInAccount.TimeZoneIanaId ?? "UTC", id, HttpContext.RequestAborted);

            return calendarEvent == null
                ? Json(new { result = false, error = localizer["Input is invalid"].Value })
                : Json(new { result = true, calendarEvent });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check calendar event existence for id {CalendarEventId}", id);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Checks whether an event in a shared/other calendar exists and returns its full detail.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    [RequiredHttpPostAccess(Role = Role.Anonymous)]
    public async Task<IActionResult> IsOtherCalendarEventExists(int id)
    {
        try
        {
            // GetVisibleOtherCalendarsAsync treats a null account as anonymous; ViewBag.LoggedInAccount
            // is never null itself, so an actually-anonymous viewer is identified by Role instead.
            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;
            AccountResponse? sessionAccount = loggedInAccount.Role == Role.Anonymous ? null : loggedInAccount;
            List<CalendarResponse> calendars =
                await calendarService.GetVisibleOtherCalendarsAsync(sessionAccount, HttpContext.RequestAborted);

            CalendarEventOutputViewModel? calendarEvent = await calendarService.GetCalendarEventDetailViewModelAsync(
                calendars, loggedInAccount.TimeZoneIanaId ?? "UTC", id, HttpContext.RequestAborted);

            return calendarEvent == null
                ? Json(new { result = false, error = localizer["Input is invalid"].Value })
                : Json(new { result = true, calendarEvent });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check other calendar event existence for id {CalendarEventId}", id);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>
    /// Streams the attachment of calendar event <paramref name="calendarEventId"/>. The event's calendar is
    /// checked again for the caller (the event dialog is not a gate: this endpoint is reachable on its own),
    /// and the file is streamed from file storage rather than embedded in the event detail.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    [RequiredHttpPostAccess(Role = Role.Anonymous)]
    public async Task<IActionResult> DownloadCalendarEventAttachedFile(long calendarEventId)
    {
        try
        {
            // ViewBag.LoggedInAccount is never null itself, so an actually-anonymous viewer is identified by Role.
            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;
            AccountResponse? viewer = loggedInAccount.Role == Role.Anonymous ? null : loggedInAccount;
            (ServiceResult result, AttachmentDownload? file) = await calendarService.OpenCalendarEventAttachedFileAsync(
                calendarEventId, viewer, HttpContext.RequestAborted);
            if (!result.Success)
                return Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });

            string contentType = _contentTypeProvider.TryGetContentType(file!.FileName, out string? type) ? type : "application/octet-stream";
            return File(file.Content, contentType, file.FileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download the attachment of calendar event {CalendarEventId}", calendarEventId);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Returns calendar events for the requested calendars as JSON.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    [RequiredHttpPostAccess(Role = Role.Anonymous)]
    public async Task<IActionResult> GetCalendarEvents([FromBody] CalendarInputViewModel calendarInputViewModel)
    {
        try
        {
            if (!ModelState.IsValid)
                return Json(new { result = false, error = localizer["Input is invalid"].Value });

            // GetVisibleOtherCalendarsAsync treats a null account as anonymous; ViewBag.LoggedInAccount
            // is never null itself, so an actually-anonymous viewer is identified by Role instead.
            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;
            AccountResponse? sessionAccount = loggedInAccount.Role == Role.Anonymous ? null : loggedInAccount;
            List<CalendarResponse> ownedCalendars = sessionAccount != null
                ? await calendarService.GetCalendarsAsync(sessionAccount.Email!, HttpContext.RequestAborted)
                : [];
            List<CalendarResponse> otherCalendars =
                await calendarService.GetVisibleOtherCalendarsAsync(sessionAccount, HttpContext.RequestAborted);

            string userTimezone = loggedInAccount.TimeZoneIanaId ?? "UTC";
            List<CalendarEventOutputViewModel> calendarEvents = [];

            // Ignore any requested calendar the account doesn't own and isn't subscribed to,
            // so a tampered request body can't be used to read another user's private events.
            List<long> authorizedCalendarIds =
            [
                .. (calendarInputViewModel.Calendars)
                .Select(c => c.Id)
                .Where(id => ownedCalendars.Any(x => x.Id == id) || otherCalendars.Any(x => x.Id == id))
            ];

            // Fetch every authorized calendar's events in a single query instead of one round trip per calendar.
            IReadOnlyDictionary<long, List<CalendarEventResponse>> eventsByCalendarId =
                await calendarService.GetCalendarEventsAsync(authorizedCalendarIds, HttpContext.RequestAborted);

            calendarEvents.AddRange(from inputCalendar in calendarInputViewModel.Calendars
                let isOwned = ownedCalendars.Any(x => x.Id == inputCalendar.Id)
                let isOther = !isOwned && otherCalendars.Any(x => x.Id == inputCalendar.Id)
                where isOwned || isOther
                let calendarType = isOwned
                    ? CalendarTypes.My
                    : CalendarTypes.Other
                let htmlColorCode = isOwned
                    ? ownedCalendars.FirstOrDefault(x => x.Id == inputCalendar.Id)?.HtmlColorCode
                    : otherCalendars.FirstOrDefault(x => x.Id == inputCalendar.Id)?.HtmlColorCode
                from evt in eventsByCalendarId.GetValueOrDefault(inputCalendar.Id, [])
                select evt.ToDisplayViewModel(userTimezone, htmlColorCode, calendarType));

            // Matches main: an empty result set for the requested range/calendars is reported the
            // same way as any other "nothing to return" case, not as a distinct success payload.
            return calendarEvents.Count > 0
                ? Json(new { result = true, calendarEvents = JsonSerialization.ToClientJson(calendarEvents) })
                : Json(new { result = false, error = localizer["Input is invalid"].Value });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get calendar events for account {AccountEmail}", ((AccountResponse)ViewBag.LoggedInAccount).Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Returns, as JSON, the sharing flags (user / guest) of each of the caller's own calendars, backfilling missing rows.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    public async Task<IActionResult> GetCalendarShareds()
    {
        try
        {
            List<CalendarSharedSummaryResponse> setCalendarShareds =
                await calendarService.GetCalendarSharedSummariesAsync(((AccountResponse)ViewBag.LoggedInAccount).Email!, HttpContext.RequestAborted);

            return Json(new { result = true, setCalendarShareds });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get calendar shareds for account {AccountEmail}", ((AccountResponse)ViewBag.LoggedInAccount).Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Returns, as JSON, every calendar shared with registered users, marking the ones the caller already subscribes to.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> GetBrowseCalendarsOfInterest()
    {
        try
        {
            List<CalendarBrowseSummaryResponse> browseCalendarsOfInterests =
                await calendarService.GetBrowseCalendarsOfInterestAsync(((AccountResponse)ViewBag.LoggedInAccount).Email!, HttpContext.RequestAborted);

            return Json(new { result = true, browseCalendarsOfInterests });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get browse calendars of interest for account {AccountEmail}", ((AccountResponse)ViewBag.LoggedInAccount).Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }
    #endregion

    #region Update

    #region Calendar
    /// <summary>Updates an existing calendar's properties.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> UpdateCalendar([FromBody] CalendarInputViewModel calendarInputViewModel)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return Json(new { result = false, error = localizer["Input is invalid"].Value });
            }

            CalendarRequest calendarRequest = calendarInputViewModel.Calendars.FirstOrDefault() ?? new CalendarRequest();
            string accountEmail = ((AccountResponse)ViewBag.LoggedInAccount).Email!;

            ServiceResult updateResult = await calendarService.UpdateCalendarAsync(accountEmail, calendarRequest, HttpContext.RequestAborted);

            return !updateResult.Success ? Json(new { result = false, error = localizer[updateResult.ErrorKey, updateResult.ErrorArgs].ToPlainString() }) : Json(new { result = true, message = localizer["The calendar has been successfully updated."].Value, calendar = new CalendarRequest { Id = calendarRequest.Id, Name = calendarRequest.Name, HtmlColorCode = calendarRequest.HtmlColorCode } });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update calendar for account {AccountEmail}", ((AccountResponse)ViewBag.LoggedInAccount).Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }
    #endregion

    #region Calendar Event
    /// <summary>Updates an existing calendar event (with optional file attachment and reminder changes).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> UpdateCalendarEvent([FromForm] CalendarEventInputViewModel calendarEventInputViewModel)
    {
        try
        {
            if (!ModelState.IsValid)
                return Json(new { result = false, error = localizer["Input is invalid"].Value });

            calendarEventInputViewModel.Description ??= "";

            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;

            if (loggedInAccount.Role is not (Role.Admin or Role.User))
                return Json(new { result = false, error = localizer["Please Login to update calendar event"].Value });

            ServiceResult updateEventResult = await calendarService.UpdateCalendarEventAsync(
                calendarEventInputViewModel.ToCalendarEventRequest(),
                loggedInAccount.Email!,
                $"{loggedInAccount.Role}Index",
                calendarEventInputViewModel.SerializedCalendarReminders.ToReminderInfoList(),
                await calendarEventInputViewModel.CalendarEventUploadedFile.ToAttachedFileInfoAsync(serverSettings.Value.MaxAttachedFileSizeBytes, HttpContext.RequestAborted),
                HttpContext.RequestAborted);

            return !updateEventResult.Success ? Json(new { result = false, error = localizer[updateEventResult.ErrorKey, updateEventResult.ErrorArgs].ToPlainString() }) : Json(new { result = true, message = localizer["The calendar event has been successfully updated."].Value });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update calendar event for account {AccountEmail}", ((AccountResponse)ViewBag.LoggedInAccount).Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }
    #endregion

    #region Calendar Shared
    /// <summary>Saves the sharing flags of the posted calendars (all must be the caller's own); unsharing one drops its subscriptions.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    public async Task<IActionResult> UpdateCalendarShared([FromBody] IEnumerable<CalendarSharedRequest> calendarShareds)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return Json(new { result = false, error = localizer["Input is invalid"].Value });
            }

            ServiceResult sharedResult = await calendarService.UpdateCalendarSharedAsync(((AccountResponse)ViewBag.LoggedInAccount).Email!, calendarShareds, HttpContext.RequestAborted);

            return !sharedResult.Success ? Json(new { result = false, error = localizer[sharedResult.ErrorKey, sharedResult.ErrorArgs].ToPlainString() }) : Json(new { result = true, message = localizer["The calendar shared has been successfully updated."].Value });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update calendar shared records for account {AccountEmail}", ((AccountResponse)ViewBag.LoggedInAccount).Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }
    #endregion

    #region Other Calendar
    /// <summary>Replaces the caller's whole set of subscriptions with the posted calendars (each must be shared with users).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> UpdateOtherCalendar([FromBody] IEnumerable<OtherCalendarResponse> otherCalendars)
    {
        try
        {
            if (ModelState.IsValid)
            {
                AccountResponse loggedInAccount = ViewBag.LoggedInAccount;

                if (loggedInAccount.Role is not (Role.Admin or Role.User))
                    return Json(new { result = false, error = localizer["Login required."].Value });

                ServiceResult otherCalendarResult = await calendarService.UpdateOtherCalendarAsync(
                    loggedInAccount.Email!,
                    otherCalendars.Select(c => new OtherCalendarRequest { CalendarId = c.CalendarId }),
                    HttpContext.RequestAborted);

                return !otherCalendarResult.Success ? Json(new { result = false, error = localizer[otherCalendarResult.ErrorKey, otherCalendarResult.ErrorArgs].ToPlainString() }) : Json(new { result = true, message = localizer["The other calendar has been successfully updated."].Value });
            }

            return Json(new { result = false, error = localizer["Input is invalid"].Value });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update other calendar for account {AccountEmail}", ((AccountResponse)ViewBag.LoggedInAccount).Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }
    #endregion

    #endregion

    #region Delete
    /// <summary>Deletes one of the caller's calendars; its events, sharing row and subscriptions go with it (database cascade).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> DeleteCalendar([FromBody] CalendarInputViewModel calendarInputViewModel)
    {
        try
        {
            CalendarRequest tempCalendar = calendarInputViewModel.Calendars.FirstOrDefault() ?? new CalendarRequest();
            string accountEmail = ((AccountResponse)ViewBag.LoggedInAccount).Email!;

            ServiceResult deleteCalendarResult = await calendarService.DeleteCalendarAsync(accountEmail, tempCalendar, HttpContext.RequestAborted);

            return !deleteCalendarResult.Success ? Json(new { result = false, error = localizer[deleteCalendarResult.ErrorKey, deleteCalendarResult.ErrorArgs].ToPlainString() }) : Json(new { result = true, message = localizer["The calendar has been successfully deleted."].Value, calendar = new CalendarRequest { Id = tempCalendar.Id } });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete calendar for account {AccountEmail}", ((AccountResponse)ViewBag.LoggedInAccount).Email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Deletes an event in one of the caller's calendars; its reminders and attachment go with it (database cascade).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> DeleteCalendarEvent(int id)
    {
        try
        {
            string email = ((AccountResponse)ViewBag.LoggedInAccount).Email!;

            ServiceResult deleteEventResult = await calendarService.DeleteCalendarEventAsync(id, email, HttpContext.RequestAborted);

            return !deleteEventResult.Success ? Json(new { result = false, error = localizer[deleteEventResult.ErrorKey, deleteEventResult.ErrorArgs].ToPlainString() }) : Json(new { result = true, message = localizer["The calendar event has been successfully deleted."].Value });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete calendar event {CalendarEventId}", id);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #endregion
}

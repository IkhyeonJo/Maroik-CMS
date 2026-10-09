/**
 * Script for the **anonymous** (public) Calendar page
 * (`Views/Calendar/AnonymousIndex.cshtml`). A read-only FullCalendar view of the
 * "other" (shared / public) calendars — visitors can look at events but not
 * create, edit or delete them.
 *
 * Flow:
 *   • Build the FullCalendar instance and seed it with the events the server
 *     rendered into `#otherCalendarEventOutputViewModels`.
 *   • `eventClick` on an "Other" event pops up a small summary; clicking
 *     "view" in the popup fetches the full event (`IsOtherCalendarEventExists`)
 *     and fills a **read-only** modal (summernote disabled, every input
 *     `disabled`), rebuilding the reminder rows from the event's
 *     `serializedCalendarReminders` — `Email`-method reminders are skipped
 *     because anonymous visitors don't see those.
 *   • Toggling an "other calendars" checkbox reloads the visible events
 *     (`RefreshCalendarEvents`).
 *   • Inline images are base64 payloads turned into object URLs client-side;
 *     the attachment is fetched on click from `DownloadCalendarEventAttachedFile`.
 *
 * The reminder lead-time bounds only feed the (disabled) inputs' `max`
 * attribute here — the authoritative rules live in `CalendarReminderPolicy`.
 * IIFE-wrapped, no `import` / `export`.
 */
(function() {
    // The runtime-check helpers the _Layout script defines (see TypeScripts/global.d.ts).
    const { check, replies, onReply, onReplyText, parseJson, conform, required, byId, fieldValue, optionalFieldValue, attribute } = window;

    // The replies this page reads, mirroring the controllers' Json(...) results (see window.replies).
    const calendarSummary = check.object({ id: check.number, name: check.string, htmlColorCode: check.string });
    // What the scripts put in a FullCalendar event's extendedProps (see the calendar.addEvent calls).
    const calendarEventProps = check.object({
        calendarId: check.number, displayStartDate: check.string, displayEndDate: check.string,
        displayStartDateTimeZone: check.string, displayEndDateTimeZone: check.string, calendarType: check.nullable(check.string),
    });
    const calendarEventJson = check.object({
        Id: check.number, CalendarId: check.number, Title: check.string, AllDay: check.boolean, StartDate: check.string, EndDate: check.string,
        HtmlColorCode: check.string, DisplayStartDate: check.string, DisplayEndDate: check.string, DisplayStartDateTimeZone: check.string,
        DisplayEndDateTimeZone: check.string, CalendarType: check.nullable(check.string),
    });
    const calendarReminderJson = check.object({
        Method: check.string, MinutesBeforeEvent: check.nullable(check.number), HoursBeforeEvent: check.nullable(check.number),
        DaysBeforeEvent: check.nullable(check.number), WeeksBeforeEvent: check.nullable(check.number), TimesBeforeEvent: check.nullable(check.string),
    });
    const calendarEventReply = replies.read({
        calendarEvent: check.object({
            id: check.number, calendarId: check.number, title: check.string, allDay: check.boolean,
            displayStartDate: check.string, displayEndDate: check.string, startDateTimeZoneIanaId: check.string, endDateTimeZoneIanaId: check.string,
            location: check.nullable(check.string), description: check.string, status: check.string,
            serializedCalendarReminders: check.jsonText(check.array(calendarReminderJson)),
            calendarEventAttachedFile: check.nullable(check.object({ name: check.string, extension: check.nullable(check.string), size: check.number })),
        }),
    });
    const calendarEventsReply = replies.read({ calendarEvents: check.jsonText(check.array(calendarEventJson)) });
    const otherCalendarsReply = replies.read({ tempOtherCalendars: check.array(calendarSummary) });
    // Cached element references — the "other event" popup and every field of the
    // read-only "view event" modal, plus the anti-forgery input and the two
    // hidden inputs the view uses to hand data to this script.
    const $viewCalendarEventTaskTabs = $("#viewCalendarEventTaskTabs");
    const $otherCalendarEventPopup = $("#otherCalendarEventPopup");
    const $otherCalendarEventPopupTitle = $("#otherCalendarEventPopupTitle");
    const $divOtherCalendarEventPopupAllDayChecked = $("#divOtherCalendarEventPopupAllDayChecked");
    const $divOtherCalendarEventPopupAllDayUnchecked = $("#divOtherCalendarEventPopupAllDayUnchecked");
    const $otherCalendarEventPopupStartAllDayChecked = $("#otherCalendarEventPopupStartAllDayChecked");
    const $otherCalendarEventPopupEndAllDayChecked = $("#otherCalendarEventPopupEndAllDayChecked");
    const $otherCalendarEventPopupStartAllDayUnchecked = $("#otherCalendarEventPopupStartAllDayUnchecked");
    const $otherCalendarEventPopupStartTimeZoneAllDayUnchecked = $("#otherCalendarEventPopupStartTimeZoneAllDayUnchecked");
    const $otherCalendarEventPopupEndAllDayUnchecked = $("#otherCalendarEventPopupEndAllDayUnchecked");
    const $otherCalendarEventPopupEndTimeZoneAllDayUnchecked = $("#otherCalendarEventPopupEndTimeZoneAllDayUnchecked");
    const $viewOtherCalendarEventPopup = $("#viewOtherCalendarEventPopup");
    const $closeOtherCalendarEventPopup = $("#closeOtherCalendarEventPopup");
    const $__RequestVerificationToken = $("input[name=\"__RequestVerificationToken\"]");
    const $viewCalendarEventId = $("#viewCalendarEventId");
    const $viewCalendarEventName = $("#viewCalendarEventName");
    const $viewCalendarEventAllDay = $("#viewCalendarEventAllDay");
    const $divViewEventAllDayChecked = $("#divViewEventAllDayChecked");
    const $divViewEventAllDayUnchecked = $("#divViewEventAllDayUnchecked");
    const $viewCalendarEventAllDayCheckedStartDate = $("#viewCalendarEventAllDayCheckedStartDate");
    const $viewCalendarEventAllDayCheckedEndDate = $("#viewCalendarEventAllDayCheckedEndDate");
    const $viewCalendarEventAllDayUncheckedStartDate = $("#viewCalendarEventAllDayUncheckedStartDate");
    const $viewCalendarEventAllDayUncheckedStartTime = $("#viewCalendarEventAllDayUncheckedStartTime");
    const $viewCalendarEventAllDayUncheckedStartTimeZone = $("#viewCalendarEventAllDayUncheckedStartTimeZone");
    const $viewCalendarEventAllDayUncheckedEndDate = $("#viewCalendarEventAllDayUncheckedEndDate");
    const $viewCalendarEventAllDayUncheckedEndTime = $("#viewCalendarEventAllDayUncheckedEndTime");
    const $viewCalendarEventAllDayUncheckedEndTimeZone = $("#viewCalendarEventAllDayUncheckedEndTimeZone");
    const $viewCalendarEventLocation = $("#viewCalendarEventLocation");
    const $viewCalendarEventDescription = $("#viewCalendarEventDescription");
    const $divViewCalendarEventAttachedFile = $("#divViewCalendarEventAttachedFile");
    const $aViewCalendarEventAttachedFile = $("#aViewCalendarEventAttachedFile");
    const $spanViewCalendarEventAttachedFile = $("#spanViewCalendarEventAttachedFile");
    const $viewCalendarEventMyCalendar = $("#viewCalendarEventMyCalendar");
    const $viewCalendarEventStatus = $("#viewCalendarEventStatus");
    const $divViewEventNotificationAllDayChecked = $("#divViewEventNotificationAllDayChecked");
    const $divViewEventNotificationAllDayUnchecked = $("#divViewEventNotificationAllDayUnchecked");
    const $viewCalendarEventNotificationAllDayChecked = $("#viewCalendarEventNotificationAllDayChecked");
    const $viewCalendarEventNotificationAllDayUnchecked = $("#viewCalendarEventNotificationAllDayUnchecked");
    const $viewCalendarEventTaskDialogModal = $("#viewCalendarEventTaskDialogModal");
    const $otherCalendarEventOutputViewModels = $("#otherCalendarEventOutputViewModels");
    const $otherCalendars = $("#otherCalendars");

    // Reminder lead-time bounds + time-of-day granularity.
    // Authoritative in CalendarReminderPolicy (server); serialized into these hidden fields by
    // AnonymousIndex.cshtml and mirrored here for form UX only. No hardcoded fallback: the limit
    // is a Domain rule, not a client constant.
    const maxMinutesBeforeEvent = parseInt(optionalFieldValue($("#maxMinutesBeforeEvent")) ?? "", 10);
    const maxHoursBeforeEvent = parseInt(optionalFieldValue($("#maxHoursBeforeEvent")) ?? "", 10);
    const maxDaysBeforeEvent = parseInt(optionalFieldValue($("#maxDaysBeforeEvent")) ?? "", 10);
    const maxWeeksBeforeEvent = parseInt(optionalFieldValue($("#maxWeeksBeforeEvent")) ?? "", 10);
    // "Notify at HH:MM" options for all-day reminders and the default selection, both published
    // by the server (CalendarViewModelMapper / CalendarReminderPolicy). Was regenerated inline
    // as Array.from({length:96}) in several places.
    const reminderTimeIntervals = parseJson(optionalFieldValue($("#reminderTimeIntervals")) || "[]", check.array(check.string), "#reminderTimeIntervals");

    /** base64 -> Blob, for turning the server-embedded inline-image payloads into object URLs. */
    function base64ToBlob(base64: string, mime: string) {
        const byteCharacters = atob(base64);
        const byteNumbers = new Array<number>(byteCharacters.length);
        for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        const byteArray = new Uint8Array(byteNumbers);
        return new Blob([byteArray], { type: mime });
    }

    /**
     * Frees each rebuilt image's object URL once the image has loaded (or failed to): the browser keeps the Blob alive until then.
     * Applied to the images as they are in the live page — the ones rebuilt in the parsed, detached copy are re-serialized into
     * HTML, which drops any handler set on them.
     */
    function ReleaseObjectUrlsOnLoad(root: Element | undefined | null) {
        if (!root) return;
        root.querySelectorAll<HTMLImageElement>("img[src^=\"blob:\"]").forEach(function(img) {
            const url = img.src;
            img.onload = img.onerror = function() {
                URL.revokeObjectURL(url);
            };
        });
    }

    // Localized FullCalendar UI strings + reminder labels + validation messages, each from a
    // hidden input the view rendered from the resource files (asserted `Record<string, string>`:
    // each holds a string, which `.val()` types as a wider union).
    const localizer = {
        Email: fieldValue($("#localizerEmail")),
        Notification: fieldValue($("#localizerNotification")),
        Minutes: fieldValue($("#localizerMinutes")),
        Hours: fieldValue($("#localizerHours")),
        Days: fieldValue($("#localizerDays")),
        Weeks: fieldValue($("#localizerWeeks")),
        BeforeAt: fieldValue($("#localizerBeforeAt")),
        PrevText: fieldValue($("#localizerPrevText")),
        NextText: fieldValue($("#localizerNextText")),
        January: fieldValue($("#localizerJanuary")),
        February: fieldValue($("#localizerFebruary")),
        March: fieldValue($("#localizerMarch")),
        April: fieldValue($("#localizerApril")),
        May: fieldValue($("#localizerMay")),
        June: fieldValue($("#localizerJune")),
        July: fieldValue($("#localizerJuly")),
        August: fieldValue($("#localizerAugust")),
        September: fieldValue($("#localizerSeptember")),
        October: fieldValue($("#localizerOctober")),
        November: fieldValue($("#localizerNovember")),
        December: fieldValue($("#localizerDecember")),
        Jan: fieldValue($("#localizerJan")),
        Feb: fieldValue($("#localizerFeb")),
        Mar: fieldValue($("#localizerMar")),
        Apr: fieldValue($("#localizerApr")),
        Jun: fieldValue($("#localizerJun")),
        Jul: fieldValue($("#localizerJul")),
        Aug: fieldValue($("#localizerAug")),
        Sep: fieldValue($("#localizerSep")),
        Oct: fieldValue($("#localizerOct")),
        Nov: fieldValue($("#localizerNov")),
        Dec: fieldValue($("#localizerDec")),
        Sunday: fieldValue($("#localizerSunday")),
        Monday: fieldValue($("#localizerMonday")),
        Tuesday: fieldValue($("#localizerTuesday")),
        Wednesday: fieldValue($("#localizerWednesday")),
        Thursday: fieldValue($("#localizerThursday")),
        Friday: fieldValue($("#localizerFriday")),
        Saturday: fieldValue($("#localizerSaturday")),
        Sun: fieldValue($("#localizerSun")),
        Mon: fieldValue($("#localizerMon")),
        Tue: fieldValue($("#localizerTue")),
        Wed: fieldValue($("#localizerWed")),
        Thu: fieldValue($("#localizerThu")),
        Fri: fieldValue($("#localizerFri")),
        Sat: fieldValue($("#localizerSat")),
        Su: fieldValue($("#localizerSu")),
        Mo: fieldValue($("#localizerMo")),
        Tu: fieldValue($("#localizerTu")),
        We: fieldValue($("#localizerWe")),
        Th: fieldValue($("#localizerTh")),
        Fr: fieldValue($("#localizerFr")),
        Sa: fieldValue($("#localizerSa")),
        YearSuffix: fieldValue($("#localizerYearSuffix")),
        IETFLanguageTag: fieldValue($("#localizerIETFLanguageTag")),
        Prev: fieldValue($("#localizerPrev")),
        Next: fieldValue($("#localizerNext")),
        PrevYear: fieldValue($("#localizerPrevYear")),
        NextYear: fieldValue($("#localizerNextYear")),
        Today: fieldValue($("#localizerToday")),
        Month: fieldValue($("#localizerMonth")),
        Week: fieldValue($("#localizerWeek")),
        Day: fieldValue($("#localizerDay")),
        List: fieldValue($("#localizerList")),
        DayGridMonth: fieldValue($("#localizerDayGridMonth")),
        DayGridWeek: fieldValue($("#localizerDayGridWeek")),
        DayGridDay: fieldValue($("#localizerDayGridDay")),
        TimeGridWeek: fieldValue($("#localizerTimeGridWeek")),
        TimeGridDay: fieldValue($("#localizerTimeGridDay")),
        ListYear: fieldValue($("#localizerListYear")),
        ListMonth: fieldValue($("#localizerListMonth")),
        ListWeek: fieldValue($("#localizerListWeek")),
        ListDay: fieldValue($("#localizerListDay")),
        ConfirmDelete: fieldValue($("#localizerConfirmDelete")),
        ThisFieldRequired: fieldValue($("#localizerThisFieldRequired")),
        ErrorInvalidNumber: fieldValue($("#localizerErrorInvalidNumber")),
        ErrorRangeMinute: fieldValue($("#localizerErrorRangeMinute")),
        ErrorRangeHour: fieldValue($("#localizerErrorRangeHour")),
        ErrorRangeDay: fieldValue($("#localizerErrorRangeDay")),
        ErrorRangeWeek: fieldValue($("#localizerErrorRangeWeek")),
        FailedToLoadCalendars: fieldValue($("#localizerFailedToLoadCalendars"))
    };

    // Module-scope FullCalendar instance, assigned inside `$(function)` below and
    // used by `RefreshCalendarEvents`.
    let calendar: FullCalendarCalendar;

    $(function() {
        $viewCalendarEventTaskTabs.tabs();

        // --- Build the read-only FullCalendar --------------------------------
        calendar = new FullCalendar.Calendar(byId("calendar", HTMLElement), {
            locale: localizer.IETFLanguageTag,
            headerToolbar: {
                left: "prevYear,prev,next,nextYear today",
                center: "title",
                right: "dayGridMonth"
            },
            buttonText: {
                today: localizer.Today,
                dayGridMonth: localizer.Month
            },
            themeSystem: "bootstrap",
            navLinks: false,
            editable: false,
            selectable: true,
            selectMirror: true,
            // Clicking an "Other" event: show a small summary popup anchored to
            // the event element. Clicking again on the same event closes it.
            eventClick: function(arg: FullCalendarEventClickArg) {
                if (conform(arg.event.extendedProps, calendarEventProps, "the event\'s extendedProps").calendarType === "Other") {
                    let eventEl = $(arg.el);
                    let offset = required(eventEl.offset(), "the event element\'s position");
                    let popup = $otherCalendarEventPopup;
                    let currentEventId = popup.data("event-id");

                    if (popup.is(":visible") && currentEventId === arg.event.id) {
                        popup.hide();
                        return undefined;
                    }

                    // Fill the popup from the event's `extendedProps` (already on
                    // the client) — all-day vs timed layout.
                    $otherCalendarEventPopupTitle.text(arg.event.title);
                    $divOtherCalendarEventPopupAllDayChecked.hide();
                    $divOtherCalendarEventPopupAllDayUnchecked.hide();

                    if (arg.event.allDay === true) {
                        $otherCalendarEventPopupStartAllDayChecked.text(conform(arg.event.extendedProps, calendarEventProps, "the event\'s extendedProps").displayStartDate);
                        $otherCalendarEventPopupEndAllDayChecked.text(conform(arg.event.extendedProps, calendarEventProps, "the event\'s extendedProps").displayEndDate);
                        $divOtherCalendarEventPopupAllDayChecked.show();
                    } else {
                        $otherCalendarEventPopupStartAllDayUnchecked.text(conform(arg.event.extendedProps, calendarEventProps, "the event\'s extendedProps").displayStartDate);
                        $otherCalendarEventPopupStartTimeZoneAllDayUnchecked.text(`(${conform(arg.event.extendedProps, calendarEventProps, "the event\'s extendedProps").displayStartDateTimeZone})`);
                        $otherCalendarEventPopupEndAllDayUnchecked.text(conform(arg.event.extendedProps, calendarEventProps, "the event\'s extendedProps").displayEndDate);
                        $otherCalendarEventPopupEndTimeZoneAllDayUnchecked.text(`(${conform(arg.event.extendedProps, calendarEventProps, "the event\'s extendedProps").displayEndDateTimeZone})`);
                        $divOtherCalendarEventPopupAllDayUnchecked.show();
                    }

                    // Position the popup at the event's bottom-right corner.
                    popup.css({
                        top: offset.top + required(eventEl.outerHeight(), "the event element\'s height"),
                        left: offset.left + required(eventEl.outerWidth(), "the event element\'s width"),
                        display: "block"
                    }).data("event-id", arg.event.id);

                    $viewOtherCalendarEventPopup.trigger("focus");

                    // Click anywhere outside the popup / an event closes it.
                    $(document).off("click.AnonymousIndex").on("click.AnonymousIndex", function(e) {
                        if (!$(e.target).closest($otherCalendarEventPopup).length && !$(e.target).closest(".fc-event").length) {
                            popup.hide();
                        }
                    });

                    $closeOtherCalendarEventPopup.off("click").on("click", function() {
                        popup.hide();
                    });

                    // "View" button: fetch the full event and open the read-only modal.
                    $viewOtherCalendarEventPopup.off("click").on("click", function() {
                        $.ajax({
                            url: "/Calendar/IsOtherCalendarEventExists" + "?id=" + popup.data("event-id"),
                            type: "POST",
                            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
                            dataType: "json",
                            contentType: "application/json; charset=utf-8",
                            success: onReply(calendarEventReply, function(data) {
                                if (data.result) {
                                    // --- Fill the read-only modal from `data.calendarEvent` ---
                                    $viewCalendarEventId.val(data.calendarEvent.id);
                                    $viewCalendarEventName.val(data.calendarEvent.title);
                                    $viewCalendarEventAllDay.val(data.calendarEvent.allDay);

                                    $divViewEventAllDayChecked.hide();
                                    $divViewEventAllDayUnchecked.hide();

                                    if (data.calendarEvent.allDay === true) {
                                        $divViewEventAllDayChecked.show();
                                        $viewCalendarEventAllDay.prop("checked", true);
                                        $viewCalendarEventAllDayCheckedStartDate.val(data.calendarEvent.displayStartDate);
                                        $viewCalendarEventAllDayCheckedEndDate.val(data.calendarEvent.displayEndDate);

                                    } else {
                                        $divViewEventAllDayUnchecked.show();

                                        $viewCalendarEventAllDay.prop("checked", false);

                                        // `displayStartDate` is "yyyy-MM-dd HH:mm" — split
                                        // into the date field and the HH:mm time field.
                                        $viewCalendarEventAllDayUncheckedStartDate.val(required(data.calendarEvent.displayStartDate.split(" ")[0], "part 0 of calendarEvent.displayStartDate"));
                                        $viewCalendarEventAllDayUncheckedStartTime.val(required(data.calendarEvent.displayStartDate.split(" ")[1], "part 1 of calendarEvent.displayStartDate").substring(0, 5));
                                        $viewCalendarEventAllDayUncheckedStartTimeZone.val(data.calendarEvent.startDateTimeZoneIanaId);

                                        $viewCalendarEventAllDayUncheckedEndDate.val(required(data.calendarEvent.displayEndDate.split(" ")[0], "part 0 of calendarEvent.displayEndDate"));
                                        $viewCalendarEventAllDayUncheckedEndTime.val(required(data.calendarEvent.displayEndDate.split(" ")[1], "part 1 of calendarEvent.displayEndDate").substring(0, 5));
                                        $viewCalendarEventAllDayUncheckedEndTimeZone.val(data.calendarEvent.endDateTimeZoneIanaId);
                                    }

                                    $viewCalendarEventLocation.val(data.calendarEvent.location);

                                    // Rehydrate the description's inline base64 images to object
                                    // URLs in a detached document, then load it into a disabled
                                    // summernote (read-only rich text).
                                    const parser = new DOMParser();
                                    const htmlDoc = parser.parseFromString(data.calendarEvent.description, "text/html");

                                    const imgTags = htmlDoc.querySelectorAll<HTMLImageElement>("img[data-file]");

                                    imgTags.forEach(function(imgTag) {
                                        const base64Data = imgTag.getAttribute("data-file");
                                        const contentType = imgTag.getAttribute("data-contenttype");

                                        if (base64Data && contentType) {
                                            const blob = base64ToBlob(base64Data, contentType);
                                            imgTag.src = URL.createObjectURL(blob);
                                            imgTag.removeAttribute("data-file");
                                            imgTag.removeAttribute("data-contenttype");
                                        }
                                    });

                                    const updatedHtml = htmlDoc.body.innerHTML;

                                    $viewCalendarEventDescription.summernote("code", updatedHtml);
                                    ReleaseObjectUrlsOnLoad($viewCalendarEventDescription.next(".note-editor")[0]);
                                    $viewCalendarEventDescription.summernote("disable");

                                    // Attachment: show the link (the event id kept in `data-*` for the
                                    // download handler at the bottom of the file) + a KB size.
                                    $divViewCalendarEventAttachedFile.hide();

                                    if (data.calendarEvent.calendarEventAttachedFile !== null) {
                                        $divViewCalendarEventAttachedFile.show();

                                        $aViewCalendarEventAttachedFile.attr("href", "#");
                                        $aViewCalendarEventAttachedFile.attr("data-calendareventid", data.calendarEvent.id);
                                        $aViewCalendarEventAttachedFile.attr("data-name", `${data.calendarEvent.calendarEventAttachedFile.name}${data.calendarEvent.calendarEventAttachedFile.extension ?? ""}`);

                                        $aViewCalendarEventAttachedFile.text(`${data.calendarEvent.calendarEventAttachedFile.name}${data.calendarEvent.calendarEventAttachedFile.extension ?? ""}`);

                                        $spanViewCalendarEventAttachedFile.text(`${Math.round(data.calendarEvent.calendarEventAttachedFile.size / 1024).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ",")}KB`);
                                    }

                                    // Populate the (disabled) "calendar" select with the
                                    // visitor-visible "other" calendars, then continue building the
                                    // rest of the read-only modal in `showViewCalendarEventModal`
                                    // once this call settles (success or error) — kept asynchronous
                                    // so a slow request doesn't freeze the page; the modal still
                                    // opens either way, just without this one select populated on
                                    // a failed fetch. `data` here is the outer `success(data)`
                                    // param — valid, captured by the closure below.
                                    // Captured from the narrowed reply: the hoisted function below does not see `data.result`.
                                    const calendarEvent = data.calendarEvent;

                                    function showViewCalendarEventModal() {
                                        $viewCalendarEventStatus.val(calendarEvent.status);

                                        // --- Rebuild the reminder rows (read-only) ---
                                        // Every generated control is `disabled`, and `Email`-method
                                        // reminders are skipped (not shown to anonymous visitors).
                                        $divViewEventNotificationAllDayChecked.hide();
                                        $divViewEventNotificationAllDayUnchecked.hide();

                                        if (calendarEvent.allDay === true) {
                                            $divViewEventNotificationAllDayChecked.find(".divViewEventNotificationAllDayCheckedRow").remove();
                                            $divViewEventNotificationAllDayChecked.show();

                                            if (calendarEvent.serializedCalendarReminders !== "[]") {
                                                const timeIntervals = reminderTimeIntervals;

                                                parseJson(calendarEvent.serializedCalendarReminders, check.array(calendarReminderJson), "the event\'s reminders").forEach((reminder) => {
                                                    if (reminder.Method !== "Email") {
                                                        // (a visitor only ever sees Notification reminders, so that is the method shown)
                                                        const selectedMethodOption = `
                                                        <option value='Email'>${localizer.Email}</option>
                                                        <option value='Notification' selected>${localizer.Notification}</option>
                                                    `;

                                                        // All-day reminders use days / weeks lead time.
                                                        let value = reminder.DaysBeforeEvent !== null ? reminder.DaysBeforeEvent : reminder.WeeksBeforeEvent;
                                                        let max = reminder.DaysBeforeEvent !== null ? maxDaysBeforeEvent : maxWeeksBeforeEvent;
                                                        let selectedDays = reminder.DaysBeforeEvent !== null ? "selected" : "";
                                                        let selectedWeeks = reminder.WeeksBeforeEvent !== null ? "selected" : "";

                                                        const selNotificationTimeTypeAllDayChecked = `
                                                    <input type='number' class='form-control-sm viewCalendarEventSelNotificationNumberAllDayChecked' style='width:20%;text-overflow:ellipsis; background-color:#343a40; color:#fff; border-color:#6c757d;' min="0" max="${max}" value="${value}" disabled>
                                                    <select class='form-control-sm viewCalendarEventSelNotificationTimeTypeAllDayChecked' style='width:15%;text-overflow:ellipsis;' disabled>
                                                        <option value='Days' ${selectedDays}>${localizer.Days}</option>
                                                        <option value='Weeks' ${selectedWeeks}>${localizer.Weeks}</option>
                                                    </select>`;

                                                        const htmlString = String.raw`
                                                        <div class="divViewEventNotificationAllDayCheckedRow">
                                                            <select class='form-control-sm viewCalendarEventSelNotificationMethodAllDayChecked' style='width:20%;text-overflow:ellipsis;' disabled>
                                                                ${selectedMethodOption}
                                                            </select>

                                                            ${selNotificationTimeTypeAllDayChecked}

                                                            <label style="padding-left:5px;padding-right:5px;">${localizer.BeforeAt}</label>
                                                            <select class='form-control-sm viewCalendarEventSelNotificationTimeAllDayChecked' style='width:11%;text-overflow:ellipsis;' disabled>
                                                                ${timeIntervals.map((time: string) => time === `${required(reminder.TimesBeforeEvent, "an all-day reminder's time").substring(0, 5)}` ? `<option value="${time}" selected>${time}</option>` : `<option value="${time}">${time}</option>`).join("")}
                                                            </select>

                                                            <br />
                                                            <div class="error-message" style="color: red; font-size: 0.9em; display: none;"></div>
                                                        </div>
                                                    `;

                                                        $(htmlString).insertBefore($viewCalendarEventNotificationAllDayChecked);
                                                    }
                                                });
                                            }
                                        } else {
                                            $divViewEventNotificationAllDayUnchecked.find(".divViewEventNotificationAllDayUncheckedRow").remove();
                                            $divViewEventNotificationAllDayUnchecked.show();

                                            if (calendarEvent.serializedCalendarReminders !== "[]") {

                                                parseJson(calendarEvent.serializedCalendarReminders, check.array(calendarReminderJson), "the event\'s reminders").forEach((reminder) => {
                                                    if (reminder.Method !== "Email") {
                                                        // (a visitor only ever sees Notification reminders, so that is the method shown)
                                                        const selectedMethodOption = `
                                                        <option value='Email'>${localizer.Email}</option>
                                                        <option value='Notification' selected>${localizer.Notification}</option>
                                                    `;

                                                        // Timed reminders can be minutes / hours / days / weeks;
                                                        // pick the first non-null and its matching max.
                                                        let value = reminder.MinutesBeforeEvent !== null ? reminder.MinutesBeforeEvent : reminder.HoursBeforeEvent !== null ? reminder.HoursBeforeEvent : reminder.DaysBeforeEvent !== null ? reminder.DaysBeforeEvent : reminder.WeeksBeforeEvent;
                                                        let max = reminder.MinutesBeforeEvent !== null ? maxMinutesBeforeEvent : reminder.HoursBeforeEvent !== null ? maxHoursBeforeEvent : reminder.DaysBeforeEvent !== null ? maxDaysBeforeEvent : maxWeeksBeforeEvent;

                                                        let selectedMinutes = reminder.MinutesBeforeEvent !== null ? "selected" : "";
                                                        let selectedHours = reminder.HoursBeforeEvent !== null ? "selected" : "";
                                                        let selectedDays = reminder.DaysBeforeEvent !== null ? "selected" : "";
                                                        let selectedWeeks = reminder.WeeksBeforeEvent !== null ? "selected" : "";

                                                        const selNotificationTimeTypeAllDayUnchecked = `
                                                    <input type='number' class='form-control-sm viewCalendarEventSelNotificationNumberAllDayUnchecked' style='width:20%;text-overflow:ellipsis; background-color:#343a40; color:#fff; border-color:#6c757d;' min="0" max="${max}" value="${value}" disabled>
                                                    <select class='form-control-sm viewCalendarEventSelNotificationTimeTypeAllDayUnchecked' style='width:15%;text-overflow:ellipsis;' disabled>
                                                        <option value='Minutes' ${selectedMinutes}>${localizer.Minutes}</option>
                                                        <option value='Hours' ${selectedHours}>${localizer.Hours}</option>
                                                        <option value='Days' ${selectedDays}>${localizer.Days}</option>
                                                        <option value='Weeks' ${selectedWeeks}>${localizer.Weeks}</option>
                                                    </select>`;

                                                        const htmlString = String.raw`
                                                        <div class="divViewEventNotificationAllDayUncheckedRow" style="padding-bottom:5px;">
                                                            <select class='form-control-sm viewCalendarEventSelNotificationMethodAllDayUnchecked' style='width:20%;text-overflow:ellipsis;' disabled>
                                                                ${selectedMethodOption}
                                                            </select>

                                                            ${selNotificationTimeTypeAllDayUnchecked}

                                                            <br />
                                                            <div class="error-message" style="color: red; font-size: 0.9em; display: none;"></div>
                                                        </div>
                                                    `;

                                                        $(htmlString).insertBefore($viewCalendarEventNotificationAllDayUnchecked);
                                                    }
                                                });
                                            }
                                        }

                                        // Force the modal open (static backdrop, no Esc).
                                        $viewCalendarEventTaskDialogModal.modal({
                                            keyboard: false,
                                            backdrop: "static"
                                        });

                                        $viewCalendarEventTaskDialogModal.modal("toggle");
                                        $viewCalendarEventTaskDialogModal.modal("show");
                                    }

                                    $.ajax({
                                        url: "/Calendar/GetOtherCalendars",
                                        method: "POST",
                                        headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
                                        dataType: "json",
                                        contentType: "application/json; charset=utf-8",
                                        success: onReply(otherCalendarsReply, function(response) {
                                            if (response.result) {
                                                $viewCalendarEventMyCalendar.empty();

                                                $.each(response.tempOtherCalendars, function(_, tempOtherCalendar) {
                                                    $viewCalendarEventMyCalendar.append($("<option>", {
                                                        value: tempOtherCalendar.id,
                                                        text: tempOtherCalendar.name
                                                    }));
                                                });

                                                $viewCalendarEventMyCalendar.val(data.calendarEvent.calendarId);
                                            }
                                        }),
                                        error: function() {
                                            toastr.error(localizer.FailedToLoadCalendars);
                                        },
                                        complete: function() {
                                            showViewCalendarEventModal();
                                        }
                                    });
                                } else {
                                    toastr.error(data.error);
                                }
                            })
                        });

                        popup.hide();
                    });

                    return false;
                }
                return undefined;
            }
        });

        // --- Seed the calendar with the server-rendered events ---------------
        parseJson(fieldValue($otherCalendarEventOutputViewModels), check.array(calendarEventJson), "the calendar events").forEach((item) => {
            calendar.addEvent({
                id: String(item.Id),
                title: item.Title,
                allDay: item.AllDay,
                start: item.StartDate,
                end: item.EndDate,
                backgroundColor: item.HtmlColorCode,
                borderColor: item.HtmlColorCode,
                extendedProps: {
                    calendarId: item.CalendarId,
                    displayStartDate: item.DisplayStartDate,
                    displayEndDate: item.DisplayEndDate,
                    displayStartDateTimeZone: item.DisplayStartDateTimeZone,
                    displayEndDateTimeZone: item.DisplayEndDateTimeZone,
                    calendarType: "Other"
                }
            });
        });

        calendar.render();

        // Guards against a stale GetCalendarEvents response (from a checkbox toggle just before
        // this one) landing after a newer one and re-adding events the user already unchecked.
        let calendarEventsRequestSeq = 0;

        /**
         * Reloads the visible events from the set of currently-checked "other
         * calendars": clears everything, gathers the checked calendar ids, POSTs
         * `GetCalendarEvents`, and re-adds whatever comes back.
         */
        function RefreshCalendarEvents() {

            calendar.removeAllEvents();

            const requestSeq = ++calendarEventsRequestSeq;

            let paramValue: CalendarEventsRequest = {
                Calendars: []
            };

            $otherCalendars.find("input[type=\"checkbox\"]").filter(":checked").each(function() {
                // Each checkbox's `<label id="lblOtherCalendar123">` encodes the id.
                let calendarsArray = [
                    { Id: Number(attribute($(this).closest("label"), "id").replace("lblOtherCalendar", "")) }
                ];

                paramValue.Calendars.push(...calendarsArray);
            });

            $.ajax({
                url: "/Calendar/GetCalendarEvents",
                method: "POST",
                headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
                dataType: "json",
                data: JSON.stringify(paramValue),
                contentType: "application/json; charset=utf-8",
                success: onReply(calendarEventsReply, function(response) {
                    // A newer RefreshCalendarEvents call already ran (and removed/re-added events)
                    // since this request went out — applying this stale response now would put
                    // back events the newer call's own removeAllEvents() just cleared.
                    if (requestSeq !== calendarEventsRequestSeq) {
                        return;
                    }
                    if (response.result) {
                        parseJson(response.calendarEvents, check.array(calendarEventJson), "the calendar events").forEach((item) => {
                            calendar.addEvent({
                                id: String(item.Id),
                                title: item.Title,
                                allDay: item.AllDay,
                                start: item.StartDate,
                                end: item.EndDate,
                                backgroundColor: item.HtmlColorCode,
                                borderColor: item.HtmlColorCode,
                                extendedProps: {
                                    calendarId: item.CalendarId,
                                    displayStartDate: item.DisplayStartDate,
                                    displayEndDate: item.DisplayEndDate,
                                    displayStartDateTimeZone: item.DisplayStartDateTimeZone,
                                    displayEndDateTimeZone: item.DisplayEndDateTimeZone,
                                    calendarType: item.CalendarType
                                }
                            });
                        });
                    }
                })
            });
        }

        // Toggling any "other calendar" checkbox reloads the events (delegated).
        $(document).off("change.AnonymousIndex", ".chkOtherCalendar").on("change.AnonymousIndex", ".chkOtherCalendar", function() {
            RefreshCalendarEvents();
        });
    });

    /**
     * Attachment download link: the file is not embedded in the event reply. It is requested from a
     * POST action that checks the event's calendar is visible to the caller again and streams the file;
     * a refusal comes back as JSON `{ result, error }` instead of a file.
     */
    function DownloadCalendarEventAttachedFile(this: HTMLElement, event: JQuery.TriggeredEvent) {
        event.preventDefault();
        // A link with nothing attached yet names no id: nothing to download.
        let calendarEventId = $(this).attr("data-calendareventid");
        if (!calendarEventId) {
            return;
        }
        let name = attribute($(this), "data-name");

        $.ajax({
            url: "/Calendar/DownloadCalendarEventAttachedFile",
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            data: { calendarEventId: calendarEventId },
            xhrFields: { responseType: "blob" },
            // Without a declared dataType jQuery infers "json" from a refusal's Content-Type and fails to
            // parse the Blob (parsererror -> the layout's ajaxError redirect); "binary" hands back the Blob as is.
            dataType: "binary",
            success: onReply(check.instance(Blob), function(data) {
                if (data.type.indexOf("application/json") === 0) {
                    data.text().then(onReplyText(replies.failed, function(refusal) {
                        toastr.error(refusal.error);
                    }));
                    return;
                }

                let url = URL.createObjectURL(data);
                let a = document.createElement("a");
                try {
                    a.href = url;
                    a.download = name;
                    a.click();
                } finally {
                    // Revoke after a tick so the download has started; drop the <a>.
                    setTimeout(() => URL.revokeObjectURL(url), 100);
                    a.remove();
                }
            })
        });
    }

    $aViewCalendarEventAttachedFile.off("click").on("click", DownloadCalendarEventAttachedFile);
})();

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
    const maxMinutesBeforeEvent = parseInt($("#maxMinutesBeforeEvent").val() as string, 10);
    const maxHoursBeforeEvent = parseInt($("#maxHoursBeforeEvent").val() as string, 10);
    const maxDaysBeforeEvent = parseInt($("#maxDaysBeforeEvent").val() as string, 10);
    const maxWeeksBeforeEvent = parseInt($("#maxWeeksBeforeEvent").val() as string, 10);
    // "Notify at HH:MM" options for all-day reminders and the default selection, both published
    // by the server (CalendarViewModelMapper / CalendarReminderPolicy). Was regenerated inline
    // as Array.from({length:96}) in several places.
    const reminderTimeIntervals = JSON.parse(($("#reminderTimeIntervals").val() as string) || "[]");

    /** base64 -> Blob, for turning the server-embedded inline-image payloads into object URLs. */
    function base64ToBlob(base64: string, mime: string) {
        const byteCharacters = atob(base64);
        const byteNumbers = new Array(byteCharacters.length);
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

    // Localized FullCalendar UI strings + reminder labels + validation messages,
    // each from a hidden input the view rendered from the resource files. Typed
    // `any` so the ~70 `.val()` unions don't each need a cast.
    const localizer: any = {
        Email: $("#localizerEmail").val(),
        Notification: $("#localizerNotification").val(),
        Minutes: $("#localizerMinutes").val(),
        Hours: $("#localizerHours").val(),
        Days: $("#localizerDays").val(),
        Weeks: $("#localizerWeeks").val(),
        BeforeAt: $("#localizerBeforeAt").val(),
        PrevText: $("#localizerPrevText").val(),
        NextText: $("#localizerNextText").val(),
        January: $("#localizerJanuary").val(),
        February: $("#localizerFebruary").val(),
        March: $("#localizerMarch").val(),
        April: $("#localizerApril").val(),
        May: $("#localizerMay").val(),
        June: $("#localizerJune").val(),
        July: $("#localizerJuly").val(),
        August: $("#localizerAugust").val(),
        September: $("#localizerSeptember").val(),
        October: $("#localizerOctober").val(),
        November: $("#localizerNovember").val(),
        December: $("#localizerDecember").val(),
        Jan: $("#localizerJan").val(),
        Feb: $("#localizerFeb").val(),
        Mar: $("#localizerMar").val(),
        Apr: $("#localizerApr").val(),
        Jun: $("#localizerJun").val(),
        Jul: $("#localizerJul").val(),
        Aug: $("#localizerAug").val(),
        Sep: $("#localizerSep").val(),
        Oct: $("#localizerOct").val(),
        Nov: $("#localizerNov").val(),
        Dec: $("#localizerDec").val(),
        Sunday: $("#localizerSunday").val(),
        Monday: $("#localizerMonday").val(),
        Tuesday: $("#localizerTuesday").val(),
        Wednesday: $("#localizerWednesday").val(),
        Thursday: $("#localizerThursday").val(),
        Friday: $("#localizerFriday").val(),
        Saturday: $("#localizerSaturday").val(),
        Sun: $("#localizerSun").val(),
        Mon: $("#localizerMon").val(),
        Tue: $("#localizerTue").val(),
        Wed: $("#localizerWed").val(),
        Thu: $("#localizerThu").val(),
        Fri: $("#localizerFri").val(),
        Sat: $("#localizerSat").val(),
        Su: $("#localizerSu").val(),
        Mo: $("#localizerMo").val(),
        Tu: $("#localizerTu").val(),
        We: $("#localizerWe").val(),
        Th: $("#localizerTh").val(),
        Fr: $("#localizerFr").val(),
        Sa: $("#localizerSa").val(),
        YearSuffix: $("#localizerYearSuffix").val(),
        IETFLanguageTag: $("#localizerIETFLanguageTag").val(),
        Prev: $("#localizerPrev").val(),
        Next: $("#localizerNext").val(),
        PrevYear: $("#localizerPrevYear").val(),
        NextYear: $("#localizerNextYear").val(),
        Today: $("#localizerToday").val(),
        Month: $("#localizerMonth").val(),
        Week: $("#localizerWeek").val(),
        Day: $("#localizerDay").val(),
        List: $("#localizerList").val(),
        DayGridMonth: $("#localizerDayGridMonth").val(),
        DayGridWeek: $("#localizerDayGridWeek").val(),
        DayGridDay: $("#localizerDayGridDay").val(),
        TimeGridWeek: $("#localizerTimeGridWeek").val(),
        TimeGridDay: $("#localizerTimeGridDay").val(),
        ListYear: $("#localizerListYear").val(),
        ListMonth: $("#localizerListMonth").val(),
        ListWeek: $("#localizerListWeek").val(),
        ListDay: $("#localizerListDay").val(),
        ConfirmDelete: $("#localizerConfirmDelete").val(),
        ThisFieldRequired: $("#localizerThisFieldRequired").val(),
        ErrorInvalidNumber: $("#localizerErrorInvalidNumber").val(),
        ErrorRangeMinute: $("#localizerErrorRangeMinute").val(),
        ErrorRangeHour: $("#localizerErrorRangeHour").val(),
        ErrorRangeDay: $("#localizerErrorRangeDay").val(),
        ErrorRangeWeek: $("#localizerErrorRangeWeek").val(),
        FailedToLoadCalendars: $("#localizerFailedToLoadCalendars").val()
    };

    // Module-scope FullCalendar instance, assigned inside `$(function)` below and
    // used by `RefreshCalendarEvents`. Typed `any` (the global `<script>` build's
    // `FullCalendar.Calendar` is loosely typed here).
    let calendar: any;

    $(function() {
        $viewCalendarEventTaskTabs.tabs();

        // --- Build the read-only FullCalendar --------------------------------
        calendar = new FullCalendar.Calendar(document.getElementById("calendar")!, {
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
            eventClick: function(arg: any) {
                if (arg.event.extendedProps.calendarType === "Other") {
                    let eventEl = $(arg.el);
                    let offset = eventEl.offset()!;
                    let popup = $otherCalendarEventPopup;
                    let currentEventId = popup.data("event-id");

                    if (popup.is(":visible") && currentEventId === arg.event.id) {
                        popup.hide();
                        return;
                    }

                    // Fill the popup from the event's `extendedProps` (already on
                    // the client) — all-day vs timed layout.
                    $otherCalendarEventPopupTitle.text(arg.event.title);
                    $divOtherCalendarEventPopupAllDayChecked.hide();
                    $divOtherCalendarEventPopupAllDayUnchecked.hide();

                    if (arg.event.allDay === true) {
                        $otherCalendarEventPopupStartAllDayChecked.text(arg.event.extendedProps.displayStartDate);
                        $otherCalendarEventPopupEndAllDayChecked.text(arg.event.extendedProps.displayEndDate);
                        $divOtherCalendarEventPopupAllDayChecked.show();
                    } else {
                        $otherCalendarEventPopupStartAllDayUnchecked.text(arg.event.extendedProps.displayStartDate);
                        $otherCalendarEventPopupStartTimeZoneAllDayUnchecked.text(`(${arg.event.extendedProps.displayStartDateTimeZone})`);
                        $otherCalendarEventPopupEndAllDayUnchecked.text(arg.event.extendedProps.displayEndDate);
                        $otherCalendarEventPopupEndTimeZoneAllDayUnchecked.text(`(${arg.event.extendedProps.displayEndDateTimeZone})`);
                        $divOtherCalendarEventPopupAllDayUnchecked.show();
                    }

                    // Position the popup at the event's bottom-right corner.
                    popup.css({
                        top: offset.top + eventEl.outerHeight()!,
                        left: offset.left + eventEl.outerWidth()!,
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
                            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
                            dataType: "json",
                            data: null as any,
                            contentType: "application/json; charset=utf-8",
                            success: function(data) {
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
                                        $viewCalendarEventAllDayUncheckedStartDate.val(data.calendarEvent.displayStartDate.split(" ")[0]);
                                        $viewCalendarEventAllDayUncheckedStartTime.val(data.calendarEvent.displayStartDate.split(" ")[1].substring(0, 5));
                                        $viewCalendarEventAllDayUncheckedStartTimeZone.val(data.calendarEvent.startDateTimeZoneIanaId);

                                        $viewCalendarEventAllDayUncheckedEndDate.val(data.calendarEvent.displayEndDate.split(" ")[0]);
                                        $viewCalendarEventAllDayUncheckedEndTime.val(data.calendarEvent.displayEndDate.split(" ")[1].substring(0, 5));
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
                                        $aViewCalendarEventAttachedFile.attr("data-name", `${data.calendarEvent.calendarEventAttachedFile.name}${data.calendarEvent.calendarEventAttachedFile.extension}`);

                                        $aViewCalendarEventAttachedFile.text(`${data.calendarEvent.calendarEventAttachedFile.name}${data.calendarEvent.calendarEventAttachedFile.extension}`);

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
                                    function showViewCalendarEventModal() {
                                        $viewCalendarEventStatus.val(data.calendarEvent.status);

                                        // --- Rebuild the reminder rows (read-only) ---
                                        // Every generated control is `disabled`, and `Email`-method
                                        // reminders are skipped (not shown to anonymous visitors).
                                        $divViewEventNotificationAllDayChecked.hide();
                                        $divViewEventNotificationAllDayUnchecked.hide();

                                        if (data.calendarEvent.allDay === true) {
                                            $divViewEventNotificationAllDayChecked.find(".divViewEventNotificationAllDayCheckedRow").remove();
                                            $divViewEventNotificationAllDayChecked.show();

                                            if (data.calendarEvent.serializedCalendarReminders !== "[]") {
                                                const timeIntervals = reminderTimeIntervals;

                                                JSON.parse(data.calendarEvent.serializedCalendarReminders).forEach((reminder: any) => {
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
                                                                ${timeIntervals.map((time: any) => time === `${reminder.TimesBeforeEvent.substring(0, 5)}` ? `<option value="${time}" selected>${time}</option>` : `<option value="${time}">${time}</option>`).join("")}
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

                                            if (data.calendarEvent.serializedCalendarReminders !== "[]") {

                                                JSON.parse(data.calendarEvent.serializedCalendarReminders).forEach((reminder: any) => {
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
                                        headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
                                        dataType: "json",
                                        data: null as any,
                                        contentType: "application/json; charset=utf-8",
                                        success: function(response) {
                                            if (response.result) {
                                                $viewCalendarEventMyCalendar.empty();

                                                $.each(response.tempOtherCalendars, function(_, tempOtherCalendar: any) {
                                                    $viewCalendarEventMyCalendar.append($("<option>", {
                                                        value: tempOtherCalendar.id,
                                                        text: tempOtherCalendar.name
                                                    }));
                                                });

                                                $viewCalendarEventMyCalendar.val(data.calendarEvent.calendarId);
                                            }
                                        },
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
                            }
                        });

                        popup.hide();
                    });

                    return false;
                }
            }
        });

        // --- Seed the calendar with the server-rendered events ---------------
        JSON.parse($otherCalendarEventOutputViewModels.val() as string).forEach((item: any) => {
            calendar.addEvent({
                id: item.Id,
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

            let paramValue: any = {
                Calendars: []
            };

            $otherCalendars.find("input[type=\"checkbox\"]").filter(":checked").each(function() {
                // Each checkbox's `<label id="lblOtherCalendar123">` encodes the id.
                let calendarsArray = [
                    { Id: Number($(this).closest("label").attr("id")!.replace("lblOtherCalendar", "")) }
                ];

                for (let i = 0; i < calendarsArray.length; i++) {
                    paramValue.Calendars.push(calendarsArray[i]);
                }
            });

            $.ajax({
                url: "/Calendar/GetCalendarEvents",
                method: "POST",
                headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
                dataType: "json",
                data: JSON.stringify(paramValue),
                contentType: "application/json; charset=utf-8",
                success: function(response) {
                    // A newer RefreshCalendarEvents call already ran (and removed/re-added events)
                    // since this request went out — applying this stale response now would put
                    // back events the newer call's own removeAllEvents() just cleared.
                    if (requestSeq !== calendarEventsRequestSeq) {
                        return;
                    }
                    if (response.result) {
                        JSON.parse(response.calendarEvents).forEach((item: any) => {
                            calendar.addEvent({
                                id: item.Id,
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
                }
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
        let calendarEventId = $(this).attr("data-calendareventid");
        let name = $(this).attr("data-name");
        if (!calendarEventId) {
            return;
        }

        $.ajax({
            url: "/Calendar/DownloadCalendarEventAttachedFile",
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            data: { calendarEventId: calendarEventId },
            xhrFields: { responseType: "blob" },
            // Without a declared dataType jQuery infers "json" from a refusal's Content-Type and fails to
            // parse the Blob (parsererror -> the layout's ajaxError redirect); "binary" hands back the Blob as is.
            dataType: "binary",
            success: function(data: Blob) {
                if (data.type.indexOf("application/json") === 0) {
                    data.text().then(function(text) {
                        toastr.error(JSON.parse(text).error);
                    });
                    return;
                }

                let url = URL.createObjectURL(data);
                let a = document.createElement("a");
                try {
                    a.href = url;
                    a.download = name!;
                    a.click();
                } finally {
                    // Revoke after a tick so the download has started; drop the <a>.
                    setTimeout(() => URL.revokeObjectURL(url), 100);
                    a.remove();
                }
            }
        });
    }

    $aViewCalendarEventAttachedFile.off("click").on("click", DownloadCalendarEventAttachedFile);
})();

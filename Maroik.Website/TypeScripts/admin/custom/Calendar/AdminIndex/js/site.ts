/**
 * Script for the **admin** Calendar page (`Views/Calendar/AdminIndex.cshtml`).
 * The biggest client script in the app: a full FullCalendar-based calendar with
 *
 *   • create / edit / delete **calendars** (color, timezone, name) — the
 *     colored checkboxes down the side toggle which calendars' events show;
 *   • create / edit / delete **events** in a tabbed modal — all-day vs timed,
 *     rich-text description (summernote) with inline image upload, an
 *     attachment, and any number of **reminders** (email / notification, N
 *     minutes/hours/days/weeks before, and for all-day events a "notify at
 *     HH:MM");
 *   • a drag-select on the grid opens the create-event modal pre-filled;
 *   • clicking an event shows a small popup with edit / delete;
 *   • a "shared calendars" dialog to expose calendars to users / guests.
 *
 * Server contract: every bound / max value (`maxMinutesBeforeEvent`,
 * `reminderTimeIntervals`, …) comes from a hidden input a Domain policy
 * (`CalendarReminderPolicy`) produced; the `Validate*` functions here are UX
 * mirrors and the controller re-validates every write. Dates are sent as the
 * local strings the user entered ("yyyy-M-d" / "yyyy-M-d H:m") together with their
 * time-zone fields; the server converts them to UTC (CalendarViewModelMapper).
 *
 * Structure: a long list of `const $x = $('#x')` element caches, then helpers,
 * then one big `$(function)` that builds the date pickers, the FullCalendar
 * instance and the event-form handlers, then a tail of standalone click
 * handlers (calendar CRUD, reminder-row add/remove, the dropdown, sharing,
 * attachment download). IIFE-wrapped, no `import` / `export`; every jQuery
 * handler is namespaced `.AdminIndex` and re-bound with `.off().on()`.
 */
(function() {
    // Escapes text before it's interpolated into a raw HTML template literal (as opposed to
    // jQuery .text()/.attr(), which already escape on their own). Calendar names are free text a
    // calendar owner controls; the CSP (script-src with no 'unsafe-inline') already blocks any
    // injected markup from executing script, but nothing there stops HTML injection (broken
    // layout, a same-origin phishing overlay) into another viewer's page — escape at the point of
    // insertion instead of relying solely on the response header.
    // Shared with every other admin-area page via admin/_Layout/js/site.ts (loaded first on every
    // admin page) instead of being redefined here.
    const escapeHtml: (value: string) => string = (window as any).escapeHtml;

    // --- Element cache: every field of the creation/edit-calendar,
    // create/edit-event and share modals, plus the event popup and the
    // anti-forgery input. (Names mirror the ids one-to-one.)
    const $createCalendarEventAllDayUncheckedEndDate = $("#createCalendarEventAllDayUncheckedEndDate");
    const $createCalendarEventAllDayCheckedEndDate = $("#createCalendarEventAllDayCheckedEndDate");
    const $editCalendarEventAllDayUncheckedEndDate = $("#editCalendarEventAllDayUncheckedEndDate");
    const $editCalendarEventAllDayCheckedEndDate = $("#editCalendarEventAllDayCheckedEndDate");
    const $divCreateEventAllDayChecked = $("#divCreateEventAllDayChecked");
    const $divCreateEventNotificationAllDayChecked = $("#divCreateEventNotificationAllDayChecked");
    const $divCreateEventAllDayUnchecked = $("#divCreateEventAllDayUnchecked");
    const $divCreateEventNotificationAllDayUnchecked = $("#divCreateEventNotificationAllDayUnchecked");
    const $divEditEventAllDayChecked = $("#divEditEventAllDayChecked");
    const $divEditEventNotificationAllDayChecked = $("#divEditEventNotificationAllDayChecked");
    const $divEditEventAllDayUnchecked = $("#divEditEventAllDayUnchecked");
    const $divEditEventNotificationAllDayUnchecked = $("#divEditEventNotificationAllDayUnchecked");
    const $createCalendarEventMyCalendar = $("#createCalendarEventMyCalendar");
    const $loggedInAccountTimeZoneIanaId = $("#loggedInAccountTimeZoneIanaId");
    const $divCalendarEventPopupAllDayChecked = $("#divCalendarEventPopupAllDayChecked");
    const $divCalendarEventPopupAllDayUnchecked = $("#divCalendarEventPopupAllDayUnchecked");
    const $editCalendarEventPopup = $("#editCalendarEventPopup");
    const $divEditCalendarEventAttachedFile = $("#divEditCalendarEventAttachedFile");
    const $aEditCalendarEventAttachedFile = $("#aEditCalendarEventAttachedFile");
    const $editCalendarEventMyCalendar = $("#editCalendarEventMyCalendar");
    const $editCalendarEventTaskDialogModal = $("#editCalendarEventTaskDialogModal");
    const $createCalendarEventAllDay = $("#createCalendarEventAllDay");
    const $editCalendarEventAllDay = $("#editCalendarEventAllDay");
    const $formEditCalendar = $("#formEditCalendar");
    const $formCreateCalendar = $("#formCreateCalendar");
    const $editCalendarDialogModal = $("#editCalendarDialogModal");
    const $confirmDeleteCalendarDialogModal = $("#confirmDeleteCalendarDialogModal");
    const $createCalendarEventDescription = $("#createCalendarEventDescription");
    const $createCalendarEventTaskDialogModal = $("#createCalendarEventTaskDialogModal");
    const $editCalendarEventDescription = $("#editCalendarEventDescription");
    const $createCalendarEventAttachment = $("#createCalendarEventAttachment");
    const $editCalendarEventAttachment = $("#editCalendarEventAttachment");
    const $createCalendarEventAllDayCheckedStartDate = $("#createCalendarEventAllDayCheckedStartDate");
    const $createCalendarEventAllDayUncheckedStartDate = $("#createCalendarEventAllDayUncheckedStartDate");
    const $createCalendarTabs = $("#createCalendarTabs");
    const $editCalendarTabs = $("#editCalendarTabs");
    const $createCalendarEventTaskTabs = $("#createCalendarEventTaskTabs");
    const $editCalendarEventTaskTabs = $("#editCalendarEventTaskTabs");
    const $setCalendarSharedTabs = $("#setCalendarSharedTabs");
    const $editCalendarEventAllDayUncheckedStartDate = $("#editCalendarEventAllDayUncheckedStartDate");
    const $editCalendarEventAllDayCheckedStartDate = $("#editCalendarEventAllDayCheckedStartDate");
    const $createCalendarEventAllDayUncheckedStartTimeZone = $("#createCalendarEventAllDayUncheckedStartTimeZone");
    const $createCalendarEventAllDayUncheckedEndTimeZone = $("#createCalendarEventAllDayUncheckedEndTimeZone");
    const $calendarEventPopup = $("#calendarEventPopup");
    const $calendarEventPopupStartAllDayChecked = $("#calendarEventPopupStartAllDayChecked");
    const $calendarEventPopupEndAllDayChecked = $("#calendarEventPopupEndAllDayChecked");
    const $calendarEventPopupTitle = $("#calendarEventPopupTitle");
    const $calendarEventPopupStartAllDayUnchecked = $("#calendarEventPopupStartAllDayUnchecked");
    const $calendarEventPopupStartTimeZoneAllDayUnchecked = $("#calendarEventPopupStartTimeZoneAllDayUnchecked");
    const $calendarEventPopupEndAllDayUnchecked = $("#calendarEventPopupEndAllDayUnchecked");
    const $calendarEventPopupEndTimeZoneAllDayUnchecked = $("#calendarEventPopupEndTimeZoneAllDayUnchecked");
    const $closeCalendarEventPopup = $("#closeCalendarEventPopup");
    const $editCalendarEventId = $("#editCalendarEventId");
    const $editCalendarEventNotificationAllDayChecked = $("#editCalendarEventNotificationAllDayChecked");
    const $editCalendarEventName = $("#editCalendarEventName");
    const $editCalendarEventAllDayUncheckedStartTime = $("#editCalendarEventAllDayUncheckedStartTime");
    const $editCalendarEventAllDayUncheckedStartTimeZone = $("#editCalendarEventAllDayUncheckedStartTimeZone");
    const $editCalendarEventAllDayUncheckedEndTime = $("#editCalendarEventAllDayUncheckedEndTime");
    const $editCalendarEventAllDayUncheckedEndTimeZone = $("#editCalendarEventAllDayUncheckedEndTimeZone");
    const $editCalendarEventLocation = $("#editCalendarEventLocation");
    const $spanEditCalendarEventAttachedFile = $("#spanEditCalendarEventAttachedFile");
    const $editCalendarEventStatus = $("#editCalendarEventStatus");
    const $editCalendarEventNotificationAllDayUnchecked = $("#editCalendarEventNotificationAllDayUnchecked");
    const $deleteCalendarEventPopup = $("#deleteCalendarEventPopup");
    const $calendarEventOutputViewModels = $("#calendarEventOutputViewModels");
    const $myCalendars = $("#myCalendars");
    const $createCalendarEventName = $("#createCalendarEventName");
    const $formCreateCalendarEvent = $("#formCreateCalendarEvent");
    const $createCalendarEventAllDayUncheckedStartTime = $("#createCalendarEventAllDayUncheckedStartTime");
    const $createCalendarEventAllDayUncheckedEndTime = $("#createCalendarEventAllDayUncheckedEndTime");
    const $createCalendarEventLocation = $("#createCalendarEventLocation");
    const $createCalendarEventStatus = $("#createCalendarEventStatus");
    const $formEditCalendarEvent = $("#formEditCalendarEvent");
    const $editCalendarId = $("#editCalendarId");
    const $editCalendarName = $("#editCalendarName");
    const $editCalendarDescription = $("#editCalendarDescription");
    const $editCalendarHtmlColorCode = $("#editCalendarHtmlColorCode");
    const $editCalendarTimeZone = $("#editCalendarTimeZone");
    const $btnDeleteCalendar = $("#btnDeleteCalendar");
    const $createCalendarName = $("#createCalendarName");
    const $createCalendarDescription = $("#createCalendarDescription");
    const $createCalendarHtmlColorCode = $("#createCalendarHtmlColorCode");
    const $createCalendarTimeZone = $("#createCalendarTimeZone");
    const $createCalendarDialogModal = $("#createCalendarDialogModal");
    const $aCreateCalendarEvent = $("#aCreateCalendarEvent");
    const $createCalendarEventNotificationAllDayChecked = $("#createCalendarEventNotificationAllDayChecked");
    const $createCalendarEventNotificationAllDayUnchecked = $("#createCalendarEventNotificationAllDayUnchecked");
    const $formUpdateCalendarShared = $("#formUpdateCalendarShared");
    const $setCalendarSharedDialogModal = $("#setCalendarSharedDialogModal");
    const $divSetCalendarShared = $("#divSetCalendarShared");
    const $aUpdateCalendarShared = $("#aUpdateCalendarShared");
    const $aCreateCalendar = $("#aCreateCalendar");
    const $dropdownContent = $("#dropdown-content");
    const $dropdownIcon = $("#dropdown-icon");
    const $__RequestVerificationToken = $("input[name=\"__RequestVerificationToken\"]");
    // Authoritative in ServerSetting.MaxAttachedFileSizeBytes (server); mirrored here for form UX
    // only. Falls back to the shared per-role default (_Layout/site.ts) if the hidden field is
    // missing or unparseable.
    const maxFileSize = parseInt($("#maxAttachedFileSizeBytes").val() as string) || (window as any).MaroikDefaultMaxAttachedFileSizeBytes;
    // Authoritative in CalendarReminderPolicy (server); rendered into hidden inputs by the view.
    const maxMinutesBeforeEvent = parseInt($("#maxMinutesBeforeEvent").val() as string, 10);
    const maxHoursBeforeEvent = parseInt($("#maxHoursBeforeEvent").val() as string, 10);
    const maxDaysBeforeEvent = parseInt($("#maxDaysBeforeEvent").val() as string, 10);
    const maxWeeksBeforeEvent = parseInt($("#maxWeeksBeforeEvent").val() as string, 10);
    // "Notify at HH:MM" options for all-day reminders and the default selection, both published
    // by the server (CalendarViewModelMapper / CalendarReminderPolicy). Was regenerated inline
    // as Array.from({length:96}) in several places.
    const reminderTimeIntervals = JSON.parse(($("#reminderTimeIntervals").val() as string) || "[]");
    const defaultBeforeAt = ($("#defaultReminderTimeOfDay").val() as string) || "09:00";

    /** base64 -> Blob, for turning server-embedded inline-image payloads into object URLs. */
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

    // Localized FullCalendar UI strings, month/day names, reminder labels and
    // validation messages — each from a hidden input the view rendered from the
    // resource files. Typed `any` so the ~80 `.val()` unions don't each need a cast.
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

    // Apply the localized names to every jQuery-UI datepicker on the page.
    $.datepicker.setDefaults({
        dateFormat: "yy-mm-dd",
        prevText: localizer.PrevText,
        nextText: localizer.NextText,
        monthNames: [localizer.January, localizer.February, localizer.March, localizer.April, localizer.May, localizer.June, localizer.July, localizer.August, localizer.September, localizer.October, localizer.November, localizer.December],
        monthNamesShort: [localizer.Jan, localizer.Feb, localizer.Mar, localizer.Apr, localizer.May, localizer.Jun, localizer.Jul, localizer.Aug, localizer.Sep, localizer.Oct, localizer.Nov, localizer.Dec],
        dayNames: [localizer.Sunday, localizer.Monday, localizer.Tuesday, localizer.Wednesday, localizer.Thursday, localizer.Friday, localizer.Saturday],
        dayNamesShort: [localizer.Sun, localizer.Mon, localizer.Tue, localizer.Wed, localizer.Thu, localizer.Fri, localizer.Sat],
        dayNamesMin: [localizer.Su, localizer.Mo, localizer.Tu, localizer.We, localizer.Th, localizer.Fr, localizer.Sa],
        showMonthAfterYear: true,
        yearSuffix: localizer.YearSuffix
    });

    // Rich-text editors for the create / edit event descriptions. `onImageUpload`
    // fires per dropped/pasted image — each is uploaded and re-inserted as an
    // object URL by `CreateUploadImageFile` / `EditUploadImageFile` (near the
    // bottom of the file); the `overflow: scroll` keeps the modal scrollable
    // while Summernote grows.
    $createCalendarEventDescription.summernote({
        lang: localizer.IETFLanguageTag,
        callbacks: {
            onImageUpload: function(files: any) {
                for (let i = 0; i < files.length; i++) {
                    CreateUploadImageFile(files[i]);
                }
                $createCalendarEventTaskDialogModal.css("overflow", "scroll");
            }
        }
    });

    $editCalendarEventDescription.summernote({
        lang: localizer.IETFLanguageTag,
        callbacks: {
            onImageUpload: function(files: any) {
                for (let i = 0; i < files.length; i++) {
                    EditUploadImageFile(files[i]);
                }
                $editCalendarEventTaskDialogModal.css("overflow", "scroll");
            }
        }
    });

    // The attachment chosen in each event form, held until the form is submitted.
    let createCalendarEventUploadedFile: any;
    let editCalendarEventUploadedFile: any;

    /**
     * `change` handler for the create-event attachment input: reject + clear the
     * input if the file exceeds `maxFileSize`, otherwise stash the `File` in
     * `createCalendarEventUploadedFile` for the create-event submit to send.
     */
    function CreateCalendarEventUploadedFile(obj: HTMLInputElement, errorMessage?: string) {
        if (!obj.files || obj.files.length === 0) {
            // The picker was cancelled: the input is empty, so nothing may be sent.
            createCalendarEventUploadedFile = undefined;
            return;
        }
        if (obj.files[0].size > maxFileSize) {
            alert(errorMessage);
            $createCalendarEventAttachment.val("");
            createCalendarEventUploadedFile = undefined;
            return false;
        } else {
            createCalendarEventUploadedFile = obj.files[0];
        }
    }

    /** Same as `CreateCalendarEventUploadedFile` for the edit-event form. */
    function EditCalendarEventUploadedFile(obj: HTMLInputElement, errorMessage?: string) {
        if (!obj.files || obj.files.length === 0) {
            // The picker was cancelled: the input is empty, so nothing may be sent.
            editCalendarEventUploadedFile = undefined;
            return;
        }
        if (obj.files[0].size > maxFileSize) {
            alert(errorMessage);
            $editCalendarEventAttachment.val("");
            editCalendarEventUploadedFile = undefined;
            return false;
        } else {
            editCalendarEventUploadedFile = obj.files[0];
        }
    }

    $createCalendarEventAttachment.off("change").on("change", function(event) {
        return CreateCalendarEventUploadedFile(event.currentTarget as HTMLInputElement, $(event.currentTarget).attr("data-errorMessage"));
    });

    $editCalendarEventAttachment.off("change").on("change", function(event) {
        return EditCalendarEventUploadedFile(event.currentTarget as HTMLInputElement, $(event.currentTarget).attr("data-errorMessage"));
    });

    /**
     * Forgets the create-event attachment and empties its input. The page is not reloaded after a
     * submit, so without this a file chosen for one event would be sent with the next one.
     */
    function ResetCreateCalendarEventAttachment() {
        createCalendarEventUploadedFile = undefined;
        $createCalendarEventAttachment.val("");
    }

    /** Same as `ResetCreateCalendarEventAttachment` for the edit-event form. */
    function ResetEditCalendarEventAttachment() {
        editCalendarEventUploadedFile = undefined;
        $editCalendarEventAttachment.val("");
    }

    // ================================================================
    // DOM-ready: build the date pickers, the FullCalendar instance and the
    // event-form handlers.
    // ================================================================
    $(function() {
        $createCalendarTabs.tabs();
        $editCalendarTabs.tabs();
        $createCalendarEventTaskTabs.tabs();
        $editCalendarEventTaskTabs.tabs();
        $setCalendarSharedTabs.tabs();

        // --- Start/end datepicker pairs (create & edit, all-day & timed) ---
        // Each start picker's `onSelect` bumps the paired end picker forward if it
        // fell behind and sets its `minDate`; each end picker's `onSelect` snaps
        // back to the start if the user picked an earlier day. The empty
        // `setTimeout(fn, 1)` in `beforeShow` / `onChangeMonthYear` is a jQuery-UI
        // repaint nudge. The options object is cast `as any` because
        // `@types/jqueryui` rejects the void-returning callbacks.
        $createCalendarEventAllDayUncheckedStartDate.datepicker({
            showButtonPanel: true,
            beforeShow: function() {
                setTimeout(function() {
                }, 1);
            },
            onChangeMonthYear: function() {
                setTimeout(function() {
                }, 1);
            },
            onSelect: function(this: any) {
                let startDate = $(this).datepicker("getDate");
                let endDate = $createCalendarEventAllDayUncheckedEndDate.datepicker("getDate");
                if (endDate && endDate < startDate) {
                    $createCalendarEventAllDayUncheckedEndDate.datepicker("setDate", startDate);
                }
                $createCalendarEventAllDayUncheckedEndDate.datepicker("option", "minDate", startDate);
            }
        } as any);

        $createCalendarEventAllDayUncheckedEndDate.datepicker({
            showButtonPanel: true,
            beforeShow: function() {
                setTimeout(function() {
                }, 1);
            },
            onChangeMonthYear: function() {
                setTimeout(function() {
                }, 1);
            },
            onSelect: function(this: any) {
                let endDate = $(this).datepicker("getDate");
                let startDate = $createCalendarEventAllDayUncheckedStartDate.datepicker("getDate");
                if (startDate && endDate < startDate) {
                    $(this).datepicker("setDate", startDate);
                }
            }
        } as any);

        $createCalendarEventAllDayCheckedStartDate.datepicker({
            showButtonPanel: true,
            beforeShow: function() {
                setTimeout(function() {
                }, 1);
            },
            onChangeMonthYear: function() {
                setTimeout(function() {
                }, 1);
            },
            onSelect: function(this: any) {
                let startDate = $(this).datepicker("getDate");
                let endDate = $createCalendarEventAllDayCheckedEndDate.datepicker("getDate");
                if (endDate && endDate < startDate) {
                    $createCalendarEventAllDayCheckedEndDate.datepicker("setDate", startDate);
                }
                $createCalendarEventAllDayCheckedEndDate.datepicker("option", "minDate", startDate);
            }
        } as any);

        $createCalendarEventAllDayCheckedEndDate.datepicker({
            showButtonPanel: true,
            beforeShow: function() {
                setTimeout(function() {
                }, 1);
            },
            onChangeMonthYear: function() {
                setTimeout(function() {
                }, 1);
            },
            onSelect: function(this: any) {
                let endDate = $(this).datepicker("getDate");
                let startDate = $createCalendarEventAllDayCheckedStartDate.datepicker("getDate");
                if (startDate && endDate < startDate) {
                    $(this).datepicker("setDate", startDate);
                }
            }
        } as any);

        $editCalendarEventAllDayUncheckedStartDate.datepicker({
            showButtonPanel: true,
            beforeShow: function() {
                setTimeout(function() {
                }, 1);
            },
            onChangeMonthYear: function() {
                setTimeout(function() {
                }, 1);
            },
            onSelect: function(this: any) {
                let startDate = $(this).datepicker("getDate");
                let endDate = $editCalendarEventAllDayUncheckedEndDate.datepicker("getDate");
                if (endDate && endDate < startDate) {
                    $editCalendarEventAllDayUncheckedEndDate.datepicker("setDate", startDate);
                }
                $editCalendarEventAllDayUncheckedEndDate.datepicker("option", "minDate", startDate);
            }
        } as any);

        $editCalendarEventAllDayUncheckedEndDate.datepicker({
            showButtonPanel: true,
            beforeShow: function() {
                setTimeout(function() {
                }, 1);
            },
            onChangeMonthYear: function() {
                setTimeout(function() {
                }, 1);
            },
            onSelect: function(this: any) {
                let endDate = $(this).datepicker("getDate");
                let startDate = $editCalendarEventAllDayUncheckedStartDate.datepicker("getDate");
                if (startDate && endDate < startDate) {
                    $(this).datepicker("setDate", startDate);
                }
            }
        } as any);

        $editCalendarEventAllDayCheckedStartDate.datepicker({
            showButtonPanel: true,
            beforeShow: function() {
                setTimeout(function() {
                }, 1);
            },
            onChangeMonthYear: function() {
                setTimeout(function() {
                }, 1);
            },
            onSelect: function(this: any) {
                let startDate = $(this).datepicker("getDate");
                let endDate = $editCalendarEventAllDayCheckedEndDate.datepicker("getDate");
                if (endDate && endDate < startDate) {
                    $editCalendarEventAllDayCheckedEndDate.datepicker("setDate", startDate);
                }
                $editCalendarEventAllDayCheckedEndDate.datepicker("option", "minDate", startDate);
            }
        } as any);

        $editCalendarEventAllDayCheckedEndDate.datepicker({
            showButtonPanel: true,
            beforeShow: function() {
                setTimeout(function() {
                }, 1);
            },
            onChangeMonthYear: function() {
                setTimeout(function() {
                }, 1);
            },
            onSelect: function(this: any) {
                let endDate = $(this).datepicker("getDate");
                let startDate = $editCalendarEventAllDayCheckedStartDate.datepicker("getDate");
                if (startDate && endDate < startDate) {
                    $(this).datepicker("setDate", startDate);
                }
            }
        } as any);

        // Hide the date picker's built-in "Close" / "Today" buttons via injected CSS.
        $("<style> .ui-datepicker-close { display: none; } </style>").appendTo("head");
        $("<style> .ui-datepicker-current { display: none; } </style>").appendTo("head");

        // --- Initial all-day vs timed layout for both event modals ---
        // Show only the section (date fields + reminder area) that matches the
        // current state of each "all day" checkbox. The click handlers near the
        // bottom of the file repeat this on toggle.
        if ($createCalendarEventAllDay.is(":checked")) {
            $divCreateEventAllDayUnchecked.hide();
            $divCreateEventAllDayChecked.hide();
            $divCreateEventAllDayChecked.show();

            $divCreateEventNotificationAllDayUnchecked.hide();
            $divCreateEventNotificationAllDayChecked.hide();
            $divCreateEventNotificationAllDayChecked.show();
        } else {
            $divCreateEventAllDayUnchecked.hide();
            $divCreateEventAllDayChecked.hide();
            $divCreateEventAllDayUnchecked.show();

            $divCreateEventNotificationAllDayUnchecked.hide();
            $divCreateEventNotificationAllDayChecked.hide();
            $divCreateEventNotificationAllDayUnchecked.show();
        }

        if ($editCalendarEventAllDay.is(":checked")) {
            $divEditEventAllDayUnchecked.hide();
            $divEditEventAllDayChecked.hide();
            $divEditEventAllDayChecked.show();

            $divEditEventNotificationAllDayUnchecked.hide();
            $divEditEventNotificationAllDayChecked.hide();
            $divEditEventNotificationAllDayChecked.show();
        } else {
            $divEditEventAllDayUnchecked.hide();
            $divEditEventAllDayChecked.hide();
            $divEditEventAllDayUnchecked.show();

            $divEditEventNotificationAllDayUnchecked.hide();
            $divEditEventNotificationAllDayChecked.hide();
            $divEditEventNotificationAllDayUnchecked.show();
        }

        // --- The FullCalendar instance --------------------------------------
        // Read-only grid (`editable: false`) but `selectable` so a drag opens the
        // create-event modal. `calendar` is block-scoped here and closed over by
        // the form handlers below and `RefreshCalendarEvents`. Cast `as any` —
        // the global `<script>` build is loosely typed.
        let calendar = new FullCalendar.Calendar(document.getElementById("calendar")!, {
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
            // Drag-select a date range: pre-fill the create-event start/end date
            // fields (UTC, +1 day on the start to match the grid), load the
            // user's calendars into the "my calendar" select, default the
            // timezone to the signed-in account's, and open the modal.
            select: function(arg: any) {
                $createCalendarEventAllDayUncheckedStartDate.val(moment.utc(arg.start).add(1, "days").format("YYYY-MM-DD"));
                $createCalendarEventAllDayCheckedStartDate.val(moment.utc(arg.start).add(1, "days").format("YYYY-MM-DD"));

                $createCalendarEventAllDayUncheckedEndDate.val(moment.utc(arg.end).format("YYYY-MM-DD"));
                $createCalendarEventAllDayCheckedEndDate.val(moment.utc(arg.end).format("YYYY-MM-DD"));

                $.ajax({
                    url: "/Calendar/GetCalendars",
                    method: "POST",
                    headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
                    dataType: "json",
                    data: null as any,
                    contentType: "application/json; charset=utf-8",
                    success: function(response) {
                        if (response.result) {
                            $createCalendarEventMyCalendar.empty();

                            $.each(response.calendars, function(_, calendar: any) {
                                $createCalendarEventMyCalendar.append($("<option>", {
                                    value: calendar.id,
                                    text: calendar.name
                                }));
                            });
                        }
                    },
                    error: function() {
                        toastr.error(localizer.FailedToLoadCalendars);
                    },
                    complete: function() {
                        // Kept out of `success` so the modal still opens (with a stale/empty
                        // calendar list rather than not opening at all) even if this fetch fails.
                        $createCalendarEventAllDayUncheckedStartTimeZone.val($loggedInAccountTimeZoneIanaId.val() as string);
                        $createCalendarEventAllDayUncheckedEndTimeZone.val($loggedInAccountTimeZoneIanaId.val() as string);

                        ResetCreateCalendarEventAttachment();
                        $createCalendarEventTaskDialogModal.modal("show");

                        calendar.unselect();
                    }
                });
            },
            // Click an event: show a small summary popup anchored to the event,
            // with "edit" and "delete" actions. Clicking the same event again
            // closes it.
            //   • edit  -> fetch the full event (`IsCalendarEventExists`), fill the
            //     tabbed edit modal (all-day/timed layout, description into
            //     Summernote with images rehydrated, attachment link, calendars
            //     select, and the reminder rows rebuilt from
            //     `serializedCalendarReminders`).
            //   • delete -> `confirm()` then POST `DeleteCalendarEvent` and remove
            //     the event from the grid.
            eventClick: function(arg: any) {
                let eventEl = $(arg.el);
                let offset = eventEl.offset()!;
                let popup = $calendarEventPopup;
                let currentEventId = popup.data("event-id");

                if (popup.is(":visible") && currentEventId === arg.event.id) {
                    popup.hide();
                    return;
                }

                $calendarEventPopupTitle.text(arg.event.title);
                $divCalendarEventPopupAllDayChecked.hide();
                $divCalendarEventPopupAllDayUnchecked.hide();

                if (arg.event.allDay === true) {
                    $calendarEventPopupStartAllDayChecked.text(arg.event.extendedProps.displayStartDate);
                    $calendarEventPopupEndAllDayChecked.text(arg.event.extendedProps.displayEndDate);
                    $divCalendarEventPopupAllDayChecked.show();
                } else {
                    $calendarEventPopupStartAllDayUnchecked.text(arg.event.extendedProps.displayStartDate);
                    $calendarEventPopupStartTimeZoneAllDayUnchecked.text(`(${arg.event.extendedProps.displayStartDateTimeZone})`);
                    $calendarEventPopupEndAllDayUnchecked.text(arg.event.extendedProps.displayEndDate);
                    $calendarEventPopupEndTimeZoneAllDayUnchecked.text(`(${arg.event.extendedProps.displayEndDateTimeZone})`);
                    $divCalendarEventPopupAllDayUnchecked.show();
                }

                popup.css({
                    top: offset.top + eventEl.outerHeight()!,
                    left: offset.left + eventEl.outerWidth()!,
                    display: "block"
                }).data("event-id", arg.event.id);

                $editCalendarEventPopup.trigger("focus");

                $(document).off("click.AdminIndex").on("click.AdminIndex", function(e) {
                    if (!$(e.target).closest($calendarEventPopup.add(".fc-event")).length) {
                        popup.hide();
                    }
                });

                $closeCalendarEventPopup.off("click").on("click", function() {
                    popup.hide();
                });

                $editCalendarEventPopup.off("click").on("click", function() {

                    $.ajax({
                        url: "/Calendar/IsCalendarEventExists" + "?id=" + popup.data("event-id"),
                        type: "POST",
                        headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
                        dataType: "json",
                        data: null as any,
                        contentType: "application/json; charset=utf-8",
                        success: function(data) {
                            if (data.result) {
                                $editCalendarEventId.val(data.calendarEvent.id);

                                $editCalendarEventName.val(data.calendarEvent.title);
                                $editCalendarEventAllDay.val(data.calendarEvent.allDay);

                                $divEditEventAllDayChecked.hide();
                                $divEditEventAllDayUnchecked.hide();

                                if (data.calendarEvent.allDay === true) {
                                    $divEditEventAllDayChecked.show();
                                    $editCalendarEventAllDay.prop("checked", true);

                                    $editCalendarEventAllDayCheckedStartDate.val(data.calendarEvent.displayStartDate);
                                    $editCalendarEventAllDayCheckedEndDate.val(data.calendarEvent.displayEndDate);
                                } else {
                                    $divEditEventAllDayUnchecked.show();
                                    $editCalendarEventAllDay.prop("checked", false);

                                    $editCalendarEventAllDayUncheckedStartDate.val(data.calendarEvent.displayStartDate.split(" ")[0]);
                                    $editCalendarEventAllDayUncheckedStartTime.val(data.calendarEvent.displayStartDate.split(" ")[1].substring(0, 5));
                                    $editCalendarEventAllDayUncheckedStartTimeZone.val(data.calendarEvent.startDateTimeZoneIanaId);

                                    $editCalendarEventAllDayUncheckedEndDate.val(data.calendarEvent.displayEndDate.split(" ")[0]);
                                    $editCalendarEventAllDayUncheckedEndTime.val(data.calendarEvent.displayEndDate.split(" ")[1].substring(0, 5));
                                    $editCalendarEventAllDayUncheckedEndTimeZone.val(data.calendarEvent.endDateTimeZoneIanaId);
                                }

                                $editCalendarEventLocation.val(data.calendarEvent.location);

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

                                $editCalendarEventDescription.summernote("code", updatedHtml);
                                ReleaseObjectUrlsOnLoad($editCalendarEventDescription.next(".note-editor")[0]);

                                $divEditCalendarEventAttachedFile.hide();

                                if (data.calendarEvent.calendarEventAttachedFile !== null) {
                                    $divEditCalendarEventAttachedFile.show();

                                    $aEditCalendarEventAttachedFile.attr("href", "#");
                                    $aEditCalendarEventAttachedFile.attr("data-calendareventid", data.calendarEvent.id);
                                    $aEditCalendarEventAttachedFile.attr("data-name", `${data.calendarEvent.calendarEventAttachedFile.name}${data.calendarEvent.calendarEventAttachedFile.extension}`);

                                    $aEditCalendarEventAttachedFile.text(`${data.calendarEvent.calendarEventAttachedFile.name}${data.calendarEvent.calendarEventAttachedFile.extension}`);

                                    $spanEditCalendarEventAttachedFile.text(`${Math.round(data.calendarEvent.calendarEventAttachedFile.size / 1024).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ",")}KB`);
                                }

                                function showEditCalendarEventModal() {
                                    $editCalendarEventStatus.val(data.calendarEvent.status);

                                    $divEditEventNotificationAllDayChecked.hide();
                                    $divEditEventNotificationAllDayUnchecked.hide();

                                    if (data.calendarEvent.allDay === true) {
                                        $divEditEventNotificationAllDayChecked.find(".divEditEventNotificationAllDayCheckedRow").remove();
                                        $divEditEventNotificationAllDayChecked.show();

                                        if (data.calendarEvent.serializedCalendarReminders !== "[]") {
                                            const timeIntervals = reminderTimeIntervals;

                                            JSON.parse(data.calendarEvent.serializedCalendarReminders).forEach((reminder: any) => {

                                                const selectedMethodOption = `
                                                <option value='Email' ${reminder.Method === "Email" ? "selected" : ""}>${localizer.Email}</option>
                                                <option value='Notification' ${reminder.Method === "Notification" ? "selected" : ""}>${localizer.Notification}</option>
                                            `;

                                                let value = reminder.DaysBeforeEvent !== null ? reminder.DaysBeforeEvent : reminder.WeeksBeforeEvent;
                                                let max = reminder.DaysBeforeEvent !== null ? maxDaysBeforeEvent : maxWeeksBeforeEvent;
                                                let selectedDays = reminder.DaysBeforeEvent !== null ? "selected" : "";
                                                let selectedWeeks = reminder.WeeksBeforeEvent !== null ? "selected" : "";

                                                const selNotificationTimeTypeAllDayChecked = `
                                            <input type='number' class='form-control-sm editCalendarEventSelNotificationNumberAllDayChecked' style='width:20%;text-overflow:ellipsis; background-color:#343a40; color:#fff; border-color:#6c757d;' min='0' max='${max}' value='${value}'>
                                            <select class='form-control-sm editCalendarEventSelNotificationTimeTypeAllDayChecked' style='width:15%;text-overflow:ellipsis;'>
                                                <option value='Days' ${selectedDays}>${localizer.Days}</option>
                                                <option value='Weeks' ${selectedWeeks}>${localizer.Weeks}</option>
                                            </select>`;

                                                const htmlString = String.raw`
                                            <div class='divEditEventNotificationAllDayCheckedRow'>
                                                <select class='form-control-sm editCalendarEventSelNotificationMethodAllDayChecked' style='width:20%;text-overflow:ellipsis;'>
                                                    ${selectedMethodOption}
                                                </select>

                                                ${selNotificationTimeTypeAllDayChecked}

                                                <label style='padding-left:5px;padding-right:5px;'>${localizer.BeforeAt}</label>
                                                <select class='form-control-sm editCalendarEventSelNotificationTimeAllDayChecked' style='width:11%;text-overflow:ellipsis;'>
                                                    ${timeIntervals.map((time: any) => time === `${reminder.TimesBeforeEvent.substring(0, 5)}` ? `<option value='${time}' selected>${time}</option>` : `<option value='${time}'>${time}</option>`).join("")}
                                                </select>
                                                <a class='hover aEditCalendarDeleteNotificationAllDayChecked' href='#' style='width:10%;'>
                                                    <i class='fa fa-trash' aria-hidden='true' style='margin-left: 7px;'></i>
                                                </a>

                                                <br />
                                                <div class='error-message' style='color: red; font-size: 0.9em; display: none;'></div>
                                            </div>
                                        `;
                                                $(htmlString).insertBefore($editCalendarEventNotificationAllDayChecked);
                                            });
                                        }
                                    } else {
                                        $divEditEventNotificationAllDayUnchecked.find(".divEditEventNotificationAllDayUncheckedRow").remove();
                                        $divEditEventNotificationAllDayUnchecked.show();

                                        if (data.calendarEvent.serializedCalendarReminders !== "[]") {

                                            JSON.parse(data.calendarEvent.serializedCalendarReminders).forEach((reminder: any) => {

                                                const selectedMethodOption = `
                                                <option value='Email' ${reminder.Method === "Email" ? "selected" : ""}>${localizer.Email}</option>
                                                <option value='Notification' ${reminder.Method === "Notification" ? "selected" : ""}>${localizer.Notification}</option>
                                            `;

                                                let value = reminder.MinutesBeforeEvent !== null ? reminder.MinutesBeforeEvent : reminder.HoursBeforeEvent !== null ? reminder.HoursBeforeEvent : reminder.DaysBeforeEvent !== null ? reminder.DaysBeforeEvent : reminder.WeeksBeforeEvent;
                                                let max = reminder.MinutesBeforeEvent !== null ? maxMinutesBeforeEvent : reminder.HoursBeforeEvent !== null ? maxHoursBeforeEvent : reminder.DaysBeforeEvent !== null ? maxDaysBeforeEvent : maxWeeksBeforeEvent;

                                                let selectedMinutes = reminder.MinutesBeforeEvent !== null ? "selected" : "";
                                                let selectedHours = reminder.HoursBeforeEvent !== null ? "selected" : "";
                                                let selectedDays = reminder.DaysBeforeEvent !== null ? "selected" : "";
                                                let selectedWeeks = reminder.WeeksBeforeEvent !== null ? "selected" : "";

                                                const selNotificationTimeTypeAllDayUnchecked = `
                                            <input type='number' class='form-control-sm editCalendarEventSelNotificationNumberAllDayUnchecked' style='width:20%;text-overflow:ellipsis; background-color:#343a40; color:#fff; border-color:#6c757d;' min='0' max='${max}' value='${value}'>
                                            <select class='form-control-sm editCalendarEventSelNotificationTimeTypeAllDayUnchecked' style='width:15%;text-overflow:ellipsis;'>
                                                <option value='Minutes' ${selectedMinutes}>${localizer.Minutes}</option>
                                                <option value='Hours' ${selectedHours}>${localizer.Hours}</option>
                                                <option value='Days' ${selectedDays}>${localizer.Days}</option>
                                                <option value='Weeks' ${selectedWeeks}>${localizer.Weeks}</option>
                                            </select>`;

                                                const htmlString = String.raw`
                                            <div class='divEditEventNotificationAllDayUncheckedRow' style='padding-bottom:5px;'>
                                                <select class='form-control-sm editCalendarEventSelNotificationMethodAllDayUnchecked' style='width:20%;text-overflow:ellipsis;'>
                                                    ${selectedMethodOption}
                                                </select>

                                                ${selNotificationTimeTypeAllDayUnchecked}

                                                <a class='hover aEditCalendarDeleteNotificationAllDayUnchecked' href='#' style='width:10%;'>
                                                    <i class='fa fa-trash' aria-hidden='true' style='margin-left: 7px;'></i>
                                                </a>

                                                <br />
                                                <div class='error-message' style='color: red; font-size: 0.9em; display: none;'></div>
                                            </div>
                                        `;

                                                $(htmlString).insertBefore($editCalendarEventNotificationAllDayUnchecked);
                                            });
                                        }
                                    }

                                    ResetEditCalendarEventAttachment();

                                    $editCalendarEventTaskDialogModal.modal({
                                        keyboard: false,
                                        backdrop: "static"
                                    });

                                    $editCalendarEventTaskDialogModal.modal("toggle");
                                    $editCalendarEventTaskDialogModal.modal("show");
                                }

                                $.ajax({
                                    url: "/Calendar/GetCalendars",
                                    method: "POST",
                                    headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
                                    dataType: "json",
                                    data: null as any,
                                    contentType: "application/json; charset=utf-8",
                                    success: function(response) {
                                        if (response.result) {
                                            $editCalendarEventMyCalendar.empty();

                                            $.each(response.calendars, function(_, calendar: any) {
                                                $editCalendarEventMyCalendar.append($("<option>", {
                                                    value: calendar.id,
                                                    text: calendar.name
                                                }));
                                            });

                                            $editCalendarEventMyCalendar.val(data.calendarEvent.calendarId);
                                        }
                                    },
                                    error: function() {
                                        toastr.error(localizer.FailedToLoadCalendars);
                                    },
                                    complete: function() {
                                        showEditCalendarEventModal();
                                    }
                                });
                            } else {
                                toastr.error(data.error);
                            }
                        }
                    });

                    popup.hide();
                });

                $deleteCalendarEventPopup.off("click").on("click", function() {
                    if (confirm(`${localizer.ConfirmDelete}`)) {
                        $.ajax({
                            url: "/Calendar/DeleteCalendarEvent?id=" + popup.data("event-id"),
                            type: "POST",
                            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
                            dataType: "json",
                            data: null as any,
                            contentType: "application/json; charset=utf-8",
                            success: function(data) {
                                if (data.result) {
                                    arg.event.remove();
                                    toastr.success(data.message);
                                } else {
                                    toastr.error(data.error);
                                }
                            }
                        });
                    }
                    popup.hide();
                });

                return false;
            }
        } as any);

        // Seed the calendar with the events the server rendered into the hidden
        // input, then render. `extendedProps` carries the pre-formatted display
        // strings the popup shows.
        JSON.parse($calendarEventOutputViewModels.val() as string).forEach((item: any) => {
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
                    displayEndDateTimeZone: item.DisplayEndDateTimeZone
                }
            });
        });

        calendar.render();

        // Guards against a stale GetCalendarEvents response (from a checkbox toggle just before
        // this one) landing after a newer one and re-adding events the user already unchecked.
        let calendarEventsRequestSeq = 0;

        /**
         * Reloads the visible events from the set of currently-checked calendar
         * checkboxes: clears the grid, gathers the checked calendar ids (each
         * `<label id="lblCalendar123">` encodes the id), POSTs `GetCalendarEvents`,
         * and re-adds whatever comes back.
         */
        function RefreshCalendarEvents() {
            calendar.removeAllEvents();

            const requestSeq = ++calendarEventsRequestSeq;

            let paramValue: any = {
                Calendars: []
            };

            $myCalendars.find("input[type=\"checkbox\"]:checked").each(function() {
                let calendarsArray = [
                    { Id: Number($(this).closest("label").attr("id")!.replace("lblCalendar", "")) }
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
                                    displayEndDateTimeZone: item.DisplayEndDateTimeZone
                                }
                            });
                        });
                    }
                }
            });
        }

        // --- Reminder-row validators (UX mirrors of CalendarReminderPolicy) ---
        // One per (create/edit) × (all-day/timed). Each reads the "number" input
        // of a reminder row, checks it's a non-negative integer within the max
        // for the chosen unit (`maxDaysBeforeEvent` etc.), underlines it red and
        // shows the row's `.error-message` on failure, and returns the bool. The
        // server re-checks every reminder on save.
        function ValidateCreateEventInputAllDayChecked(row: JQuery) {
            let numberInput = row.find(".createCalendarEventSelNotificationNumberAllDayChecked");
            let value: any = (numberInput.val() as string).trim();
            let timeType = row.find(".createCalendarEventSelNotificationTimeTypeAllDayChecked").val();
            let isValid = true;
            let errorMessage = "";

            if (value === "") {
                isValid = false;
                errorMessage = `${localizer.ThisFieldRequired}`;
            } else if (isNaN(value)) {
                isValid = false;
                errorMessage = `${localizer.ErrorInvalidNumber}`;
            } else {
                value = parseInt(value, 10);
                if (timeType === "Days") {
                    if (value < 0 || value > maxDaysBeforeEvent) {
                        isValid = false;
                        errorMessage = `${localizer.ErrorRangeDay}`;
                    }
                } else if (timeType === "Weeks") {
                    if (value < 0 || value > maxWeeksBeforeEvent) {
                        isValid = false;
                        errorMessage = `${localizer.ErrorRangeWeek}`;
                    }
                }
            }

            if (!isValid) {
                numberInput.css("text-decoration", "underline");
                numberInput.css("text-decoration-color", "red");
            } else {
                numberInput.css("text-decoration", "none");
            }

            row.find(".error-message").text(errorMessage).toggle(!isValid);
            return isValid;
        }

        // Create modal, timed (all-day unchecked) reminder row — see the group comment above.
        function ValidateCreateEventInputAllDayUnchecked(row: JQuery) {
            let numberInput = row.find(".createCalendarEventSelNotificationNumberAllDayUnchecked");
            let value: any = (numberInput.val() as string).trim();
            let timeType = row.find(".createCalendarEventSelNotificationTimeTypeAllDayUnchecked").val();
            let isValid = true;
            let errorMessage = "";

            if (value === "") {
                isValid = false;
                errorMessage = `${localizer.ThisFieldRequired}`;
            } else if (isNaN(value)) {
                isValid = false;
                errorMessage = `${localizer.ErrorInvalidNumber}`;
            } else {
                value = parseInt(value, 10);
                if (timeType === "Minutes") {
                    if (value < 0 || value > maxMinutesBeforeEvent) {
                        isValid = false;
                        errorMessage = `${localizer.ErrorRangeMinute}`;
                    }
                } else if (timeType === "Hours") {
                    if (value < 0 || value > maxHoursBeforeEvent) {
                        isValid = false;
                        errorMessage = `${localizer.ErrorRangeHour}`;
                    }
                } else if (timeType === "Days") {
                    if (value < 0 || value > maxDaysBeforeEvent) {
                        isValid = false;
                        errorMessage = `${localizer.ErrorRangeDay}`;
                    }
                } else if (timeType === "Weeks") {
                    if (value < 0 || value > maxWeeksBeforeEvent) {
                        isValid = false;
                        errorMessage = `${localizer.ErrorRangeWeek}`;
                    }
                }
            }

            if (!isValid) {
                numberInput.css("text-decoration", "underline");
                numberInput.css("text-decoration-color", "red");
            } else {
                numberInput.css("text-decoration", "none");
            }

            row.find(".error-message").text(errorMessage).toggle(!isValid);
            return isValid;
        }

        // Edit modal, timed (all-day unchecked) reminder row — see the group comment above.
        function ValidateEditEventInputAllDayUnchecked(row: JQuery) {
            let numberInput = row.find(".editCalendarEventSelNotificationNumberAllDayUnchecked");
            let value: any = (numberInput.val() as string).trim();
            let timeType = row.find(".editCalendarEventSelNotificationTimeTypeAllDayUnchecked").val();
            let isValid = true;
            let errorMessage = "";

            if (value === "") {
                isValid = false;
                errorMessage = `${localizer.ThisFieldRequired}`;
            } else if (isNaN(value)) {
                isValid = false;
                errorMessage = `${localizer.ErrorInvalidNumber}`;
            } else {
                value = parseInt(value, 10);
                if (timeType === "Minutes") {
                    if (value < 0 || value > maxMinutesBeforeEvent) {
                        isValid = false;
                        errorMessage = `${localizer.ErrorRangeMinute}`;
                    }
                } else if (timeType === "Hours") {
                    if (value < 0 || value > maxHoursBeforeEvent) {
                        isValid = false;
                        errorMessage = `${localizer.ErrorRangeHour}`;
                    }
                } else if (timeType === "Days") {
                    if (value < 0 || value > maxDaysBeforeEvent) {
                        isValid = false;
                        errorMessage = `${localizer.ErrorRangeDay}`;
                    }
                } else if (timeType === "Weeks") {
                    if (value < 0 || value > maxWeeksBeforeEvent) {
                        isValid = false;
                        errorMessage = `${localizer.ErrorRangeWeek}`;
                    }
                }
            }

            if (!isValid) {
                numberInput.css("text-decoration", "underline");
                numberInput.css("text-decoration-color", "red");
            } else {
                numberInput.css("text-decoration", "none");
            }

            row.find(".error-message").text(errorMessage).toggle(!isValid);
            return isValid;
        }

        // Edit modal, all-day (all-day checked) reminder row — see the group comment above.
        function ValidateEditEventInputAllDayChecked(row: JQuery) {
            let numberInput = row.find(".editCalendarEventSelNotificationNumberAllDayChecked");
            let value: any = (numberInput.val() as string).trim();
            let timeType = row.find(".editCalendarEventSelNotificationTimeTypeAllDayChecked").val();
            let isValid = true;
            let errorMessage = "";

            if (value === "") {
                isValid = false;
                errorMessage = `${localizer.ThisFieldRequired}`;
            } else if (isNaN(value)) {
                isValid = false;
                errorMessage = `${localizer.ErrorInvalidNumber}`;
            } else {
                value = parseInt(value, 10);
                if (timeType === "Days") {
                    if (value < 0 || value > maxDaysBeforeEvent) {
                        isValid = false;
                        errorMessage = `${localizer.ErrorRangeDay}`;
                    }
                } else if (timeType === "Weeks") {
                    if (value < 0 || value > maxWeeksBeforeEvent) {
                        isValid = false;
                        errorMessage = `${localizer.ErrorRangeWeek}`;
                    }
                }
            }

            if (!isValid) {
                numberInput.css("text-decoration", "underline");
                numberInput.css("text-decoration-color", "red");
            } else {
                numberInput.css("text-decoration", "none");
            }

            row.find(".error-message").text(errorMessage).toggle(!isValid);
            return isValid;
        }

        // Toggling a calendar checkbox reloads the visible events.
        $(document).off("change.AdminIndex", ".chkCalendar").on("change.AdminIndex", ".chkCalendar", function() {
            RefreshCalendarEvents();
        });

        // Live-validate each reminder row as its unit or number changes (the
        // `.error-message` is re-hidden here; the submit handlers show it).
        $(document).off("change.AdminIndex", ".createCalendarEventSelNotificationTimeTypeAllDayChecked").on("change.AdminIndex", ".createCalendarEventSelNotificationTimeTypeAllDayChecked", function() {
            let row = $(this).closest(".divCreateEventNotificationAllDayCheckedRow");
            ValidateCreateEventInputAllDayChecked(row);
            row.find(".error-message").hide();
        });

        $(document).off("input.AdminIndex", ".createCalendarEventSelNotificationNumberAllDayChecked").on("input.AdminIndex", ".createCalendarEventSelNotificationNumberAllDayChecked", function() {
            let row = $(this).closest(".divCreateEventNotificationAllDayCheckedRow");
            ValidateCreateEventInputAllDayChecked(row);
            row.find(".error-message").hide();
        });

        $(document).off("change.AdminIndex", ".createCalendarEventSelNotificationTimeTypeAllDayUnchecked").on("change.AdminIndex", ".createCalendarEventSelNotificationTimeTypeAllDayUnchecked", function() {
            let row = $(this).closest(".divCreateEventNotificationAllDayUncheckedRow");
            ValidateCreateEventInputAllDayUnchecked(row);
            row.find(".error-message").hide();
        });

        $(document).off("input.AdminIndex", ".createCalendarEventSelNotificationNumberAllDayUnchecked").on("input.AdminIndex", ".createCalendarEventSelNotificationNumberAllDayUnchecked", function() {
            let row = $(this).closest(".divCreateEventNotificationAllDayUncheckedRow");
            ValidateCreateEventInputAllDayUnchecked(row);
            row.find(".error-message").hide();
        });

        $(document).off("change.AdminIndex", ".editCalendarEventSelNotificationTimeTypeAllDayChecked").on("change.AdminIndex", ".editCalendarEventSelNotificationTimeTypeAllDayChecked", function() {
            let row = $(this).closest(".divEditEventNotificationAllDayCheckedRow");
            ValidateEditEventInputAllDayChecked(row);
            row.find(".error-message").hide();
        });

        $(document).off("input.AdminIndex", ".editCalendarEventSelNotificationNumberAllDayChecked").on("input.AdminIndex", ".editCalendarEventSelNotificationNumberAllDayChecked", function() {
            let row = $(this).closest(".divEditEventNotificationAllDayCheckedRow");
            ValidateEditEventInputAllDayChecked(row);
            row.find(".error-message").hide();
        });

        $(document).off("change.AdminIndex", ".editCalendarEventSelNotificationTimeTypeAllDayUnchecked").on("change.AdminIndex", ".editCalendarEventSelNotificationTimeTypeAllDayUnchecked", function() {
            let row = $(this).closest(".divEditEventNotificationAllDayUncheckedRow");
            ValidateEditEventInputAllDayUnchecked(row);
            row.find(".error-message").hide();
        });

        $(document).off("input.AdminIndex", ".editCalendarEventSelNotificationNumberAllDayUnchecked").on("input.AdminIndex", ".editCalendarEventSelNotificationNumberAllDayUnchecked", function() {
            let row = $(this).closest(".divEditEventNotificationAllDayUncheckedRow");
            ValidateEditEventInputAllDayUnchecked(row);
            row.find(".error-message").hide();
        });

        // --- Create-event submit -------------------------------------------
        // Validate every reminder row + jquery-validation, then assemble the
        // payload: start/end as "date" or "date time" strings, blank timezones
        // for all-day, the reminder list (one shape per unit), the description,
        // and the stashed attachment — all into `FormData` (multipart). On
        // success, reload the visible events, toast and close the modal.
        // `as any` on the `FormData.append` values because it wants `string|Blob`.
        $formCreateCalendarEvent.off("submit").on("submit", function(event) {
            event.preventDefault();

            let isValidForm = true;

            if ($createCalendarEventAllDay.is(":checked")) {
                $(".divCreateEventNotificationAllDayCheckedRow").each(function() {
                    let isValidRow = ValidateCreateEventInputAllDayChecked($(this));
                    isValidForm = isValidForm && isValidRow;
                });
            } else {
                $(".divCreateEventNotificationAllDayUncheckedRow").each(function() {
                    let isValidRow = ValidateCreateEventInputAllDayUnchecked($(this));
                    isValidForm = isValidForm && isValidRow;
                });
            }

            if (!$formCreateCalendarEvent.valid() || !isValidForm) {
                return false;
            }

            let calendarEventName = $createCalendarEventName.val();
            let allDay = $createCalendarEventAllDay.is(":checked");

            let startDate: any;
            let endDate: any;

            if ($createCalendarEventAllDay.is(":checked")) {
                startDate = $createCalendarEventAllDayCheckedStartDate.val();
                endDate = $createCalendarEventAllDayCheckedEndDate.val();
            } else {
                startDate = $createCalendarEventAllDayUncheckedStartDate.val() + " " + $createCalendarEventAllDayUncheckedStartTime.val();
                endDate = $createCalendarEventAllDayUncheckedEndDate.val() + " " + $createCalendarEventAllDayUncheckedEndTime.val();
            }

            let startDateTimeZoneIanaId: any;
            let endDateTimeZoneIanaId: any;

            if ($createCalendarEventAllDay.is(":checked")) {
                startDateTimeZoneIanaId = "";
                endDateTimeZoneIanaId = "";
            } else {
                startDateTimeZoneIanaId = $createCalendarEventAllDayUncheckedStartTimeZone.val();
                endDateTimeZoneIanaId = $createCalendarEventAllDayUncheckedEndTimeZone.val();
            }

            let location = $createCalendarEventLocation.val();
            let description = $createCalendarEventDescription.val();

            let calendarId = $createCalendarEventMyCalendar.val();
            let status = $createCalendarEventStatus.val();

            let calendarReminders: any[] = [];

            if ($createCalendarEventAllDay.is(":checked")) {
                $(".divCreateEventNotificationAllDayCheckedRow").each(function() {
                    let method = $(this).find(".createCalendarEventSelNotificationMethodAllDayChecked").val();
                    let number = $(this).find(".createCalendarEventSelNotificationNumberAllDayChecked").val();
                    let timeType = $(this).find(".createCalendarEventSelNotificationTimeTypeAllDayChecked").val();
                    let time = $(this).find(".createCalendarEventSelNotificationTimeAllDayChecked").val();

                    if (timeType === "Days") {
                        calendarReminders.push({
                            Method: method,
                            MinutesBeforeEvent: null,
                            HoursBeforeEvent: null,
                            DaysBeforeEvent: number,
                            WeeksBeforeEvent: null,
                            TimesBeforeEvent: time
                        });
                    } else if (timeType === "Weeks") {
                        calendarReminders.push({
                            Method: method,
                            MinutesBeforeEvent: null,
                            HoursBeforeEvent: null,
                            DaysBeforeEvent: null,
                            WeeksBeforeEvent: number,
                            TimesBeforeEvent: time
                        });
                    }
                });
            } else {
                $(".divCreateEventNotificationAllDayUncheckedRow").each(function() {
                    let method = $(this).find(".createCalendarEventSelNotificationMethodAllDayUnchecked").val();
                    let number = $(this).find(".createCalendarEventSelNotificationNumberAllDayUnchecked").val();
                    let timeType = $(this).find(".createCalendarEventSelNotificationTimeTypeAllDayUnchecked").val();

                    if (timeType === "Minutes") {
                        calendarReminders.push({
                            Method: method,
                            MinutesBeforeEvent: number,
                            HoursBeforeEvent: null,
                            DaysBeforeEvent: null,
                            WeeksBeforeEvent: null,
                            TimesBeforeEvent: null
                        });
                    } else if (timeType === "Hours") {
                        calendarReminders.push({
                            Method: method,
                            MinutesBeforeEvent: null,
                            HoursBeforeEvent: number,
                            DaysBeforeEvent: null,
                            WeeksBeforeEvent: null,
                            TimesBeforeEvent: null
                        });
                    } else if (timeType === "Days") {
                        calendarReminders.push({
                            Method: method,
                            MinutesBeforeEvent: null,
                            HoursBeforeEvent: null,
                            DaysBeforeEvent: number,
                            WeeksBeforeEvent: null,
                            TimesBeforeEvent: null
                        });
                    } else if (timeType === "Weeks") {
                        calendarReminders.push({
                            Method: method,
                            MinutesBeforeEvent: null,
                            HoursBeforeEvent: null,
                            DaysBeforeEvent: null,
                            WeeksBeforeEvent: number,
                            TimesBeforeEvent: null
                        });
                    }
                });
            }

            let formData = new FormData();
            formData.append("Title", calendarEventName as any);
            formData.append("AllDay", allDay as any);

            formData.append("StartDate", startDate as any);
            formData.append("EndDate", endDate as any);

            formData.append("StartDateTimeZoneIanaId", startDateTimeZoneIanaId as any);
            formData.append("EndDateTimeZoneIanaId", endDateTimeZoneIanaId as any);

            formData.append("Location", location as any);
            formData.append("Description", description as any);

            formData.append("CalendarEventUploadedFile", createCalendarEventUploadedFile);

            formData.append("CalendarId", calendarId as any);
            formData.append("Status", status as any);

            formData.append("SerializedCalendarReminders", JSON.stringify(calendarReminders));

            $.ajax({
                url: "/Calendar/CreateCalendarEvent",
                type: "POST",
                headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
                dataType: "json",
                data: formData,
                contentType: false,
                processData: false,
                complete: function() {
                    // Accepted or refused, the file has been sent: it must not go out again.
                    ResetCreateCalendarEventAttachment();
                },
                success: function(data) {
                    if (data.result) {

                        calendar.removeAllEvents();

                        let paramValue: any = {
                            Calendars: []
                        };
                        $myCalendars.find("input[type=\"checkbox\"]:checked").each(function() {
                            let calendarsArray = [
                                { Id: Number($(this).closest("label").attr("id")!.replace("lblCalendar", "")) }
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
                                                displayEndDateTimeZone: item.DisplayEndDateTimeZone
                                            }
                                        });
                                    });
                                } else {
                                    toastr.error(response.error);
                                }
                            }
                        });

                        toastr.success(data.message);

                        $createCalendarEventTaskDialogModal.modal("hide");
                    } else {
                        toastr.error(data.error);
                    }
                }
            });

            return false;

        });

        // --- Edit-event submit -------------------------------------------
        // Same shape as create-event submit, plus the event `Id` and
        // `CalendarId`; POSTs `UpdateCalendarEvent`, then reloads the visible
        // events, toasts and closes the modal.
        $formEditCalendarEvent.off("submit").on("submit", function(event) {
            event.preventDefault();

            let isValidForm = true;

            if ($editCalendarEventAllDay.is(":checked")) {
                $(".divEditEventNotificationAllDayCheckedRow").each(function() {
                    let isValidRow = ValidateEditEventInputAllDayChecked($(this));
                    isValidForm = isValidForm && isValidRow;
                });
            } else {
                $(".divEditEventNotificationAllDayUncheckedRow").each(function() {
                    let isValidRow = ValidateEditEventInputAllDayUnchecked($(this));
                    isValidForm = isValidForm && isValidRow;
                });
            }

            if (!$formEditCalendarEvent.valid() || !isValidForm) {
                return false;
            }

            let calendarEventName = $editCalendarEventName.val();
            let allDay = $editCalendarEventAllDay.is(":checked");

            let startDate: any;
            let endDate: any;

            if ($editCalendarEventAllDay.is(":checked")) {
                startDate = $editCalendarEventAllDayCheckedStartDate.val();
                endDate = $editCalendarEventAllDayCheckedEndDate.val();
            } else {
                startDate = $editCalendarEventAllDayUncheckedStartDate.val() + " " + $editCalendarEventAllDayUncheckedStartTime.val();
                endDate = $editCalendarEventAllDayUncheckedEndDate.val() + " " + $editCalendarEventAllDayUncheckedEndTime.val();
            }

            let startDateTimeZoneIanaId: any;
            let endDateTimeZoneIanaId: any;

            if ($editCalendarEventAllDay.is(":checked")) {
                startDateTimeZoneIanaId = "";
                endDateTimeZoneIanaId = "";
            } else {
                startDateTimeZoneIanaId = $editCalendarEventAllDayUncheckedStartTimeZone.val();
                endDateTimeZoneIanaId = $editCalendarEventAllDayUncheckedEndTimeZone.val();
            }

            let location = $editCalendarEventLocation.val();
            let description = $editCalendarEventDescription.val();

            let id = $editCalendarEventId.val();
            let calendarId = $editCalendarEventMyCalendar.val();
            let status = $editCalendarEventStatus.val();

            let calendarReminders: any[] = [];

            if ($editCalendarEventAllDay.is(":checked")) {
                $(".divEditEventNotificationAllDayCheckedRow").each(function() {
                    let method = $(this).find(".editCalendarEventSelNotificationMethodAllDayChecked").val();
                    let number = $(this).find(".editCalendarEventSelNotificationNumberAllDayChecked").val();
                    let timeType = $(this).find(".editCalendarEventSelNotificationTimeTypeAllDayChecked").val();
                    let time = $(this).find(".editCalendarEventSelNotificationTimeAllDayChecked").val();

                    if (timeType === "Days") {
                        calendarReminders.push({
                            Method: method,
                            MinutesBeforeEvent: null,
                            HoursBeforeEvent: null,
                            DaysBeforeEvent: number,
                            WeeksBeforeEvent: null,
                            TimesBeforeEvent: time
                        });
                    } else if (timeType === "Weeks") {
                        calendarReminders.push({
                            Method: method,
                            MinutesBeforeEvent: null,
                            HoursBeforeEvent: null,
                            DaysBeforeEvent: null,
                            WeeksBeforeEvent: number,
                            TimesBeforeEvent: time
                        });
                    }
                });
            } else {
                $(".divEditEventNotificationAllDayUncheckedRow").each(function() {
                    let method = $(this).find(".editCalendarEventSelNotificationMethodAllDayUnchecked").val();
                    let number = $(this).find(".editCalendarEventSelNotificationNumberAllDayUnchecked").val();
                    let timeType = $(this).find(".editCalendarEventSelNotificationTimeTypeAllDayUnchecked").val();

                    if (timeType === "Minutes") {
                        calendarReminders.push({
                            Method: method,
                            MinutesBeforeEvent: number,
                            HoursBeforeEvent: null,
                            DaysBeforeEvent: null,
                            WeeksBeforeEvent: null,
                            TimesBeforeEvent: null
                        });
                    } else if (timeType === "Hours") {
                        calendarReminders.push({
                            Method: method,
                            MinutesBeforeEvent: null,
                            HoursBeforeEvent: number,
                            DaysBeforeEvent: null,
                            WeeksBeforeEvent: null,
                            TimesBeforeEvent: null
                        });
                    } else if (timeType === "Days") {
                        calendarReminders.push({
                            Method: method,
                            MinutesBeforeEvent: null,
                            HoursBeforeEvent: null,
                            DaysBeforeEvent: number,
                            WeeksBeforeEvent: null,
                            TimesBeforeEvent: null
                        });
                    } else if (timeType === "Weeks") {
                        calendarReminders.push({
                            Method: method,
                            MinutesBeforeEvent: null,
                            HoursBeforeEvent: null,
                            DaysBeforeEvent: null,
                            WeeksBeforeEvent: number,
                            TimesBeforeEvent: null
                        });
                    }
                });
            }

            let formData = new FormData();
            formData.append("Id", id as any);
            formData.append("CalendarId", calendarId as any);

            formData.append("Title", calendarEventName as any);
            formData.append("AllDay", allDay as any);

            formData.append("StartDate", startDate as any);
            formData.append("EndDate", endDate as any);

            formData.append("StartDateTimeZoneIanaId", startDateTimeZoneIanaId as any);
            formData.append("EndDateTimeZoneIanaId", endDateTimeZoneIanaId as any);

            formData.append("Location", location as any);
            formData.append("Description", description as any);

            formData.append("CalendarEventUploadedFile", editCalendarEventUploadedFile);
            formData.append("Status", status as any);

            formData.append("SerializedCalendarReminders", JSON.stringify(calendarReminders));

            $.ajax({
                url: "/Calendar/UpdateCalendarEvent",
                type: "POST",
                headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
                dataType: "json",
                data: formData,
                contentType: false,
                processData: false,
                complete: function() {
                    // Accepted or refused, the file has been sent: it must not go out again.
                    ResetEditCalendarEventAttachment();
                },
                success: function(data) {
                    if (data.result) {

                        calendar.removeAllEvents();

                        let paramValue: any = {
                            Calendars: []
                        };

                        $myCalendars.find("input[type=\"checkbox\"]:checked").each(function() {
                            let calendarsArray = [
                                { Id: Number($(this).closest("label").attr("id")!.replace("lblCalendar", "")) }
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
                                                displayEndDateTimeZone: item.DisplayEndDateTimeZone
                                            }
                                        });
                                    });
                                } else {
                                    toastr.error(response.error);
                                }
                            }
                        });

                        toastr.success(data.message);

                        $editCalendarEventTaskDialogModal.modal("hide");
                    } else {
                        toastr.error(data.error);
                    }
                }
            });

            return false;

        });

        /**
         * Edit-calendar submit: POST `{ Calendars: [{ ID, Name, Description,
         * HtmlColorCode, TimeZoneIanaId }] }`. On success update the side-list
         * label + checkbox accent color, recolor that calendar's events in the
         * grid, and re-sort the side list by name.
         */
        function UpdateCalendar() {

            if (!$formEditCalendar.valid()) {
                return false;
            }

            let id = $editCalendarId.val();
            let name = $editCalendarName.val();
            let description = $editCalendarDescription.val();
            let htmlColorCode = $editCalendarHtmlColorCode.val();
            let timeZoneIanaId = $editCalendarTimeZone.val();

            let calendarsArray = [
                { Id: id, Name: name, Description: description, HtmlColorCode: htmlColorCode, TimeZoneIanaId: timeZoneIanaId }
            ];

            let paramValue: any = {
                Calendars: []
            };

            for (let i = 0; i < calendarsArray.length; i++) {
                paramValue.Calendars.push(calendarsArray[i]);
            }

            $.ajax({
                url: "/Calendar/UpdateCalendar",
                type: "POST",
                headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
                dataType: "json",
                data: JSON.stringify(paramValue),
                contentType: "application/json; charset=utf-8",
                success: function(data) {
                    if (data.result) {
                        $editCalendarDialogModal.modal("hide");
                        let $lblCalendarId = $("#lblCalendar" + data.calendar.id);

                        let label = $lblCalendarId.find("label");
                        if (label.length > 0) {
                            label.text(data.calendar.name);
                        }

                        let checkbox = $lblCalendarId.find("input[type=\"checkbox\"]");
                        if (checkbox.length > 0) {
                            checkbox.css("accent-color", data.calendar.htmlColorCode);
                        }

                        calendar.getEvents().forEach(function(event) {
                            if (event.extendedProps.calendarId === data.calendar.id) {
                                event.setProp("backgroundColor", data.calendar.htmlColorCode);
                                event.setProp("borderColor", data.calendar.htmlColorCode);
                            }
                        });

                        setTimeout(function() {
                            let calendars = $myCalendars.find("label[id^=\"lblCalendar\"]").get();

                            calendars.sort(function(a, b) {
                                let nameA = $(a).text().trim().toUpperCase();
                                let nameB = $(b).text().trim().toUpperCase();

                                if (nameA < nameB) return -1;
                                if (nameA > nameB) return 1;
                                return 0;
                            });

                            $.each(calendars, function(_, label: any) {
                                $myCalendars.append(label);
                            });
                        }, 0);

                        toastr.success(data.message);
                    } else {
                        toastr.error(data.error);
                    }
                }
            });
            return false;
        }

        $formEditCalendar.off("submit").on("submit", function() {
            return UpdateCalendar();
        });

        /**
         * Confirmed calendar delete: verify it still exists (`IsCalendarExists`),
         * POST `DeleteCalendar`, then remove its side-list label and all of its
         * events from the grid. The id is stashed on the confirmation modal by the
         * `.delete-calendar` click handler near the bottom of the file.
         */
        function DeleteCalendar() {
            let selectedCalendarId = $confirmDeleteCalendarDialogModal.data("calendar-id");

            $.ajax({
                url: "/Calendar/IsCalendarExists" + "?id=" + selectedCalendarId,
                type: "POST",
                headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
                dataType: "json",
                data: null as any,
                contentType: "application/json; charset=utf-8",
                success: function(data) {
                    if (data.result) {

                        let calendarsArray = [
                            { Id: data.calendar.id }
                        ];

                        let paramValue: any = {
                            Calendars: []
                        };

                        for (let i = 0; i < calendarsArray.length; i++) {
                            paramValue.Calendars.push(calendarsArray[i]);
                        }

                        $.ajax({
                            url: "/Calendar/DeleteCalendar",
                            type: "POST",
                            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
                            dataType: "json",
                            data: JSON.stringify(paramValue),
                            contentType: "application/json; charset=utf-8",
                            success: function(data) {
                                if (data.result) {
                                    $confirmDeleteCalendarDialogModal.modal("hide");

                                    $("#lblCalendar" + data.calendar.id).remove();

                                    calendar.getEvents().forEach(function(event) {
                                        if (event.extendedProps.calendarId === data.calendar.id) {
                                            event.remove();
                                        }
                                    });

                                    toastr.success(data.message);
                                } else {
                                    toastr.error(data.error);
                                }
                            }
                        });
                    } else {
                        toastr.error(data.error);
                    }
                }
            });
        }

        $btnDeleteCalendar.off("click").on("click", function() {
            DeleteCalendar();
        });
    });

    // ================================================================
    // Standalone handlers (outside the DOM-ready block above).
    // ================================================================

    /**
     * summernote `onImageUpload` for the create-event editor: POST the image,
     * get `{ file: { fileContents(base64), contentType }, filePath }` back, and
     * insert it as a size-capped `<img>` on an object URL (revoked once decoded).
     * `filePath` goes in `alt` so the server resolves the stored image on save.
     */
    function CreateUploadImageFile(file: File) {

        let formData = new FormData();
        formData.append("summernoteImageFile", file);

        $.ajax({
            url: "/Calendar/UploadImageFile",
            data: formData,
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            type: "POST",
            enctype: "multipart/form-data",
            processData: false,
            contentType: false,
            dataType: "json",
            cache: false,
            success: function(data) {
                if (data.result) {
                    const imgURL = URL.createObjectURL(base64ToBlob(data.file.fileContents, data.file.contentType));
                    const imgNode = document.createElement("img");
                    imgNode.src = imgURL;
                    imgNode.style.maxWidth = "170px";
                    imgNode.style.maxHeight = "209px";
                    imgNode.setAttribute("alt", data.filePath);

                    $createCalendarEventDescription.summernote("insertNode", imgNode);

                    imgNode.onload = function() {
                        URL.revokeObjectURL(imgURL);
                    };
                    imgNode.onerror = function() {
                        URL.revokeObjectURL(imgURL);
                    };
                } else {
                    alert(data.errorMessage);
                }
            }
        });
    }

    /** Same as `CreateUploadImageFile` for the edit-event editor. */
    function EditUploadImageFile(file: File) {

        let formData = new FormData();
        formData.append("summernoteImageFile", file);

        $.ajax({
            url: "/Calendar/UploadImageFile",
            data: formData,
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            type: "POST",
            enctype: "multipart/form-data",
            processData: false,
            contentType: false,
            dataType: "json",
            cache: false,
            success: function(data) {
                if (data.result) {
                    const imgURL = URL.createObjectURL(base64ToBlob(data.file.fileContents, data.file.contentType));
                    const imgNode = document.createElement("img");
                    imgNode.src = imgURL;
                    imgNode.style.maxWidth = "170px";
                    imgNode.style.maxHeight = "209px";
                    imgNode.setAttribute("alt", data.filePath);

                    $editCalendarEventDescription.summernote("insertNode", imgNode);

                    imgNode.onload = function() {
                        URL.revokeObjectURL(imgURL);
                    };
                    imgNode.onerror = function() {
                        URL.revokeObjectURL(imgURL);
                    };
                } else {
                    alert(data.errorMessage);
                }
            }
        });
    }

    /**
     * Create-calendar submit: POST `{ Calendars: [{ Name, Description,
     * HtmlColorCode, TimeZoneIanaId }] }`. On success append a new side-list
     * `<label id="lblCalendar{id}">` (checkbox colored by the calendar, plus
     * edit / delete icons) and re-sort the list by name.
     */
    function CreateCalendar() {

        if (!$formCreateCalendar.valid()) {
            return false;
        }

        let name = $createCalendarName.val();
        let description = $createCalendarDescription.val();
        let htmlColorCode = $createCalendarHtmlColorCode.val();
        let timeZoneIanaId = $createCalendarTimeZone.val();

        let calendarsArray = [
            { Name: name, Description: description, HtmlColorCode: htmlColorCode, TimeZoneIanaId: timeZoneIanaId }
        ];

        let paramValue: any = {
            Calendars: []
        };

        for (let i = 0; i < calendarsArray.length; i++) {
            paramValue.Calendars.push(calendarsArray[i]);
        }

        $.ajax({
            url: "/Calendar/CreateCalendar",
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: JSON.stringify(paramValue),
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {
                    $createCalendarDialogModal.modal("hide");

                    $myCalendars.append(`<label id='lblCalendar${data.calendar.id}' style='position: relative; border:1px solid #ccc; padding:10px; margin:0 0 10px; display:block'><input type='checkbox' style='accent-color:${data.calendar.htmlColorCode}' checked /><label style='padding-left:0.5em;'> ${escapeHtml(data.calendar.name)}</label><i id='${data.calendar.id}' class='edit-calendar fas fa-edit' style='position: absolute; right: 40px; top: 50%; transform: translateY(-50%); cursor: pointer;'></i><i id='${data.calendar.id}' class='delete-calendar fas fa-trash' style='position: absolute; right: 15px; top: 50%; transform: translateY(-50%); cursor: pointer;'></i></label>`);

                    let calendars = $myCalendars.find("label[id^=\"lblCalendar\"]").get();

                    calendars.sort(function(a, b) {
                        let nameA = $(a).text().trim().toUpperCase();
                        let nameB = $(b).text().trim().toUpperCase();

                        if (nameA < nameB) return -1;
                        if (nameA > nameB) return 1;
                        return 0;
                    });

                    $.each(calendars, function(_, label: any) {
                        $myCalendars.append(label);
                    });

                    toastr.success(data.message);
                } else {
                    toastr.error(data.error);
                }
            }
        });
        return false;
    }

    $formCreateCalendar.off("submit").on("submit", function() {
        return CreateCalendar();
    });

    // "New event" button (not via drag-select): load the user's calendars into
    // the select, default the timezone to the account's, open the modal, and set
    // every start/end date field to today. Kept asynchronous — the modal-opening
    // steps run from `complete` once the fetch settles, rather than blocking the
    // page while it's in flight.
    $aCreateCalendarEvent.off("click").on("click", function() {

        $.ajax({
            url: "/Calendar/GetCalendars",
            method: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: null as any,
            contentType: "application/json; charset=utf-8",
            success: function(response) {
                if (response.result) {
                    $createCalendarEventMyCalendar.empty();

                    $.each(response.calendars, function(_, calendar: any) {
                        $createCalendarEventMyCalendar.append($("<option>", {
                            value: calendar.id,
                            text: calendar.name
                        }));
                    });
                }
            },
            error: function() {
                toastr.error(localizer.FailedToLoadCalendars);
            },
            complete: function() {
                $createCalendarEventAllDayUncheckedStartTimeZone.val($loggedInAccountTimeZoneIanaId.val() as string);
                $createCalendarEventAllDayUncheckedEndTimeZone.val($loggedInAccountTimeZoneIanaId.val() as string);

                ResetCreateCalendarEventAttachment();
                $createCalendarEventTaskDialogModal.modal("show");

                $createCalendarEventAllDayUncheckedStartDate.val(moment().format("YYYY-MM-DD"));
                $createCalendarEventAllDayCheckedStartDate.val(moment().format("YYYY-MM-DD"));

                $createCalendarEventAllDayUncheckedEndDate.val(moment().format("YYYY-MM-DD"));
                $createCalendarEventAllDayCheckedEndDate.val(moment().format("YYYY-MM-DD"));
            }
        });
    });

    // "All day" checkboxes: swap between the timed and all-day date/reminder
    // sections of each event modal (same logic as the DOM-ready block ran once).
    $createCalendarEventAllDay.off("click").on("click", function() {
        if ($(this).is(":checked")) {
            $divCreateEventAllDayUnchecked.hide();
            $divCreateEventAllDayChecked.hide();
            $divCreateEventAllDayChecked.show();

            $divCreateEventNotificationAllDayUnchecked.hide();
            $divCreateEventNotificationAllDayChecked.hide();
            $divCreateEventNotificationAllDayChecked.show();
        } else {
            $divCreateEventAllDayUnchecked.hide();
            $divCreateEventAllDayChecked.hide();
            $divCreateEventAllDayUnchecked.show();

            $divCreateEventNotificationAllDayUnchecked.hide();
            $divCreateEventNotificationAllDayChecked.hide();
            $divCreateEventNotificationAllDayUnchecked.show();
        }
    });

    $editCalendarEventAllDay.off("click").on("click", function() {
        if ($(this).is(":checked")) {
            $divEditEventAllDayUnchecked.hide();
            $divEditEventAllDayChecked.hide();
            $divEditEventAllDayChecked.show();

            $divEditEventNotificationAllDayUnchecked.hide();
            $divEditEventNotificationAllDayChecked.hide();
            $divEditEventNotificationAllDayChecked.show();
        } else {
            $divEditEventAllDayUnchecked.hide();
            $divEditEventAllDayChecked.hide();
            $divEditEventAllDayUnchecked.show();

            $divEditEventNotificationAllDayUnchecked.hide();
            $divEditEventNotificationAllDayChecked.hide();
            $divEditEventNotificationAllDayUnchecked.show();
        }
    });

    // "Add reminder" buttons (one per create/edit × all-day/timed): build a new
    // reminder row (method select, number input capped at the relevant max, unit
    // select, and for all-day a "notify at" select defaulted to `defaultBeforeAt`)
    // and insert it before the button. Rows are removed by the delegated
    // `.a*DeleteNotification*` handlers just below.
    $createCalendarEventNotificationAllDayChecked.off("click").on("click", function() {
        const htmlString = String.raw`
        <div class='divCreateEventNotificationAllDayCheckedRow'>
            <select class='form-control-sm createCalendarEventSelNotificationMethodAllDayChecked' style='width:20%;text-overflow:ellipsis;'>
                <option value='Email'>${localizer.Email}</option>
                <option value='Notification'>${localizer.Notification}</option>
            </select>

            <input type='number' class='form-control-sm createCalendarEventSelNotificationNumberAllDayChecked' style='width:20%;text-overflow:ellipsis; background-color:#343a40; color:#fff; border-color:#6c757d;' min='0' max='${maxDaysBeforeEvent}'>

            <select class='form-control-sm createCalendarEventSelNotificationTimeTypeAllDayChecked' style='width:15%;text-overflow:ellipsis;'>
                <option value='Days' selected>${localizer.Days}</option>
                <option value='Weeks'>${localizer.Weeks}</option>
            </select>
            <label style='padding-left:5px;padding-right:5px;'>${localizer.BeforeAt}</label>
            <select class='form-control-sm createCalendarEventSelNotificationTimeAllDayChecked' style='width:11%;text-overflow:ellipsis;'>
                ${reminderTimeIntervals.map((time: any) => time === `${defaultBeforeAt}` ? `<option value='${time}' selected>${time}</option>` : `<option value='${time}'>${time}</option>`).join("")}
            </select>
            <a class='hover aCreateCalendarDeleteNotificationAllDayChecked' href='#' style='width:10%;'>
                <i class='fa fa-trash' aria-hidden='true' style='margin-left: 7px;'></i>
            </a>

            <br />
            <div class='error-message' style='color: red; font-size: 0.9em; display: none;'></div>
        </div>
    `;

        $(htmlString).insertBefore(this);
    });

    $createCalendarEventNotificationAllDayUnchecked.off("click").on("click", function() {
        const htmlString = String.raw`
        <div class='divCreateEventNotificationAllDayUncheckedRow' style='padding-bottom:5px;'>
            <select class='form-control-sm createCalendarEventSelNotificationMethodAllDayUnchecked' style='width:20%;text-overflow:ellipsis;'>
                <option value='Email'>${localizer.Email}</option>
                <option value='Notification'>${localizer.Notification}</option>
            </select>

            <input type='number' class='form-control-sm createCalendarEventSelNotificationNumberAllDayUnchecked' style='width:20%;text-overflow:ellipsis; background-color:#343a40; color:#fff; border-color:#6c757d;' min='0' max='${maxMinutesBeforeEvent}'>

            <select class='form-control-sm createCalendarEventSelNotificationTimeTypeAllDayUnchecked' style='width:15%;text-overflow:ellipsis;'>
                <option value='Minutes' selected>${localizer.Minutes}</option>
                <option value='Hours'>${localizer.Hours}</option>
                <option value='Days'>${localizer.Days}</option>
                <option value='Weeks'>${localizer.Weeks}</option>
            </select>
            <a class='hover aCreateCalendarDeleteNotificationAllDayUnchecked' href='#' style='width:10%;'>
                <i class='fa fa-trash' aria-hidden='true' style='margin-left: 7px;'></i>
            </a>

            <br />
            <div class='error-message' style='color: red; font-size: 0.9em; display: none;'></div>
        </div>
    `;

        $(htmlString).insertBefore(this);
    });

    $editCalendarEventNotificationAllDayChecked.off("click").on("click", function() {
        const htmlString = String.raw`
        <div class='divEditEventNotificationAllDayCheckedRow'>
            <select class='form-control-sm editCalendarEventSelNotificationMethodAllDayChecked' style='width:20%;text-overflow:ellipsis;'>
                <option value='Email'>${localizer.Email}</option>
                <option value='Notification'>${localizer.Notification}</option>
            </select>

            <input type='number' class='form-control-sm editCalendarEventSelNotificationNumberAllDayChecked' style='width:20%;text-overflow:ellipsis; background-color:#343a40; color:#fff; border-color:#6c757d;' min='0' max='${maxDaysBeforeEvent}'>

            <select class='form-control-sm editCalendarEventSelNotificationTimeTypeAllDayChecked' style='width:15%;text-overflow:ellipsis;'>
                <option value='Days' selected>${localizer.Days}</option>
                <option value='Weeks'>${localizer.Weeks}</option>
            </select>
            <label style='padding-left:5px;padding-right:5px;'>${localizer.BeforeAt}</label>
            <select class='form-control-sm editCalendarEventSelNotificationTimeAllDayChecked' style='width:11%;text-overflow:ellipsis;'>
                ${reminderTimeIntervals.map((time: any) => time === `${defaultBeforeAt}` ? `<option value='${time}' selected>${time}</option>` : `<option value='${time}'>${time}</option>`).join("")}
            </select>
            <a class='hover aEditCalendarDeleteNotificationAllDayChecked' href='#' style='width:10%;'>
                <i class='fa fa-trash' aria-hidden='true' style='margin-left: 7px;'></i>
            </a>

            <br />
            <div class='error-message' style='color: red; font-size: 0.9em; display: none;'></div>
        </div>
    `;

        $(htmlString).insertBefore(this);
    });

    $editCalendarEventNotificationAllDayUnchecked.off("click").on("click", function() {

        const htmlString = String.raw`
        <div class='divEditEventNotificationAllDayUncheckedRow' style='padding-bottom:5px;'>
            <select class='form-control-sm editCalendarEventSelNotificationMethodAllDayUnchecked' style='width:20%;text-overflow:ellipsis;'>
                <option value='Email'>${localizer.Email}</option>
                <option value='Notification'>${localizer.Notification}</option>
            </select>

            <input type='number' class='form-control-sm editCalendarEventSelNotificationNumberAllDayUnchecked' style='width:20%;text-overflow:ellipsis; background-color:#343a40; color:#fff; border-color:#6c757d;' min='0' max='${maxMinutesBeforeEvent}'>

            <select class='form-control-sm editCalendarEventSelNotificationTimeTypeAllDayUnchecked' style='width:15%;text-overflow:ellipsis;'>
                <option value='Minutes' selected>${localizer.Minutes}</option>
                <option value='Hours'>${localizer.Hours}</option>
                <option value='Days'>${localizer.Days}</option>
                <option value='Weeks'>${localizer.Weeks}</option>
            </select>
            <a class='hover aEditCalendarDeleteNotificationAllDayUnchecked' href='#' style='width:10%;'>
                <i class='fa fa-trash' aria-hidden='true' style='margin-left: 7px;'></i>
            </a>

            <br />
            <div class='error-message' style='color: red; font-size: 0.9em; display: none;'></div>
        </div>
    `;

        $(htmlString).insertBefore(this);
    });

    // "Remove reminder" (the trash icon in each row) — delegated because rows are
    // added dynamically.
    $(document).off("click.AdminIndex", ".aCreateCalendarDeleteNotificationAllDayChecked").on("click.AdminIndex", ".aCreateCalendarDeleteNotificationAllDayChecked", function() {
        $(this).parent().remove();
    });

    $(document).off("click.AdminIndex", ".aCreateCalendarDeleteNotificationAllDayUnchecked").on("click.AdminIndex", ".aCreateCalendarDeleteNotificationAllDayUnchecked", function() {
        $(this).parent().remove();
    });

    $(document).off("click.AdminIndex", ".aEditCalendarDeleteNotificationAllDayChecked").on("click.AdminIndex", ".aEditCalendarDeleteNotificationAllDayChecked", function() {
        $(this).parent().remove();
    });

    $(document).off("click.AdminIndex", ".aEditCalendarDeleteNotificationAllDayUnchecked").on("click.AdminIndex", ".aEditCalendarDeleteNotificationAllDayUnchecked", function() {
        $(this).parent().remove();
    });

    // --- The "+" dropdown menu (new calendar / new event / share) ---
    $dropdownIcon.off("click").on("click", function() {
        $dropdownContent.toggle();
    });

    $(window).off("click.AdminIndex").on("click.AdminIndex", function(event) {
        if (!$(event.target).closest($dropdownIcon.add($dropdownContent)).length) {
            $dropdownContent.hide();
        }
    });

    $dropdownContent.find("a").off("click").on("click", function() {
        $dropdownContent.hide();
    });

    $aCreateCalendar.off("click").on("click", function() {
        $createCalendarDialogModal.modal("show");
    });

    // --- "Shared calendars" dialog ---
    // Opening it fetches the share state of each of the admin's own calendars
    // (`GetCalendarShareds`) and builds a row per calendar with "share to user"
    // / "share to guest" checkboxes.
    $aUpdateCalendarShared.off("click").on("click", function() {

        $.ajax({
            url: "/Calendar/GetCalendarShareds",
            method: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: null as any,
            contentType: "application/json; charset=utf-8",
            success: function(response) {
                if (response.result) {
                    let htmlString = "";
                    $.each(response.setCalendarShareds, function(_, setCalendarShared: any) {
                        htmlString += String.raw`
                            <div class='form-row setCalendarShared' style='margin-top: 20px;'>
                                <div id='divSetCalendarShared${setCalendarShared.id}' class='form-group col-md-4 mb-4 text-center'>
                                    <label class='shareToCalendar' style='word-break: break-all;'>${escapeHtml(setCalendarShared.name)}</label>
                                </div>
                                <div class='form-group col-md-4 mb-4 text-center'>
                                    <input class='form-control form-control-sm shareToUser' type='checkbox' ${setCalendarShared.user === true ? "checked" : ""}/>
                                </div>
                                <div class='form-group col-md-4 mb-4 text-center'>
                                    <input class='form-control form-control-sm shareToGuest' type='checkbox' ${setCalendarShared.guest === true ? "checked" : ""}/>
                                </div>
                            </div>

                        `;
                    });

                    $divSetCalendarShared.html(htmlString);
                }
            },
            error: function() {
                toastr.error(localizer.FailedToLoadCalendars);
            },
            complete: function() {
                // Kept out of `success` so the modal still opens (with a stale/empty list
                // rather than not opening at all) even if this fetch fails.
                $setCalendarSharedDialogModal.modal("show");
            }
        });
    });

    // Submitting the share dialog: collect the per-calendar checkbox state into
    // `{ CalendarId, User, Anonymous }` rows and POST `UpdateCalendarShared`.
    $formUpdateCalendarShared.off("submit").on("submit", function(event) {
        event.preventDefault();

        let calendarBeShared: any[] = [];

        $divSetCalendarShared.children(".setCalendarShared").each(function() {
            calendarBeShared.push({
                CalendarId: $(this).find(".shareToCalendar").parent().attr("id")!.replace("divSetCalendarShared", ""),
                User: $(this).find(".shareToUser").prop("checked"),
                Anonymous: $(this).find(".shareToGuest").prop("checked")
            });
        });

        $.ajax({
            url: "/Calendar/UpdateCalendarShared",
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: JSON.stringify(calendarBeShared),
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {
                    $setCalendarSharedDialogModal.modal("hide");
                    toastr.success(data.message);
                } else {
                    toastr.error(data.error);
                }
            }
        });

        return false;
    });

    // --- Side-list calendar edit / delete icons ---
    // The pencil icon fetches the calendar (`IsCalendarExists`) and fills the
    // edit-calendar modal; the trash icon stashes the id on the confirmation modal
    // and opens it (the actual delete runs in `DeleteCalendar`). Both are
    // delegated and `stopPropagation` so they don't toggle the checkbox.
    $(document).off("click.AdminIndex", ".edit-calendar").on("click.AdminIndex", ".edit-calendar", function(e) {
        e.preventDefault();
        e.stopPropagation();

        let selectedCalendarId = $(this).parent().attr("id")!.replace("lblCalendar", "");

        $.ajax({
            url: "/Calendar/IsCalendarExists" + "?id=" + selectedCalendarId,
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: null as any,
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {
                    $editCalendarId.val(data.calendar.id);
                    $editCalendarName.val(data.calendar.name);
                    $editCalendarDescription.val(data.calendar.description);
                    $editCalendarHtmlColorCode.val(data.calendar.htmlColorCode);
                    $editCalendarTimeZone.val(data.calendar.timeZoneIanaId).trigger("change");

                    $editCalendarDialogModal.modal({
                        keyboard: false,
                        backdrop: "static"
                    });

                    $editCalendarDialogModal.modal("toggle");
                    $editCalendarDialogModal.modal("show");
                } else {
                    toastr.error(data.error);
                }
            }
        });
    });

    $(document).off("mouseenter.AdminIndex", ".edit-calendar").on("mouseenter.AdminIndex", ".edit-calendar", function() {
        $(this).css("color", "blue");
    });

    $(document).off("mouseleave.AdminIndex", ".edit-calendar").on("mouseleave.AdminIndex", ".edit-calendar", function() {
        $(this).css("color", "");
    });

    $(document).off("click.AdminIndex", ".delete-calendar").on("click.AdminIndex", ".delete-calendar", function(e) {
        e.preventDefault();
        e.stopPropagation();

        let calendarId = $(this).parent().attr("id")!.replace("lblCalendar", "");
        $confirmDeleteCalendarDialogModal.data("calendar-id", calendarId);

        $confirmDeleteCalendarDialogModal.modal({
            keyboard: false,
            backdrop: "static"
        });

        $confirmDeleteCalendarDialogModal.modal("toggle");
        $confirmDeleteCalendarDialogModal.modal("show");
    });

    $(document).off("mouseenter.AdminIndex", ".delete-calendar").on("mouseenter.AdminIndex", ".delete-calendar", function() {
        $(this).css("color", "blue");
    });

    $(document).off("mouseleave.AdminIndex", ".delete-calendar").on("mouseleave.AdminIndex", ".delete-calendar", function() {
        $(this).css("color", "");
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

    $aEditCalendarEventAttachedFile.off("click").on("click", DownloadCalendarEventAttachedFile);
})();

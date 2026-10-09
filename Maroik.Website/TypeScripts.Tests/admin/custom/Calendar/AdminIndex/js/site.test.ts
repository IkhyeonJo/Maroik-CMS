import { describe, it, expect } from "vitest";
import { describeMissingServerConstants, describeRequiredServerConstants } from "@tests/_common/missingConfigSuite";
import { loadSite, antiForgery, hidden, eventProps, present, successOf, completeOf, errorOf, calendarSelect, calendarEventClick, firstCalendar } from "@tests/_common/harness";
import {
    describeCalendarCommon,
    describeEditFormDetail,
    describeCalendarExtras,
    evt,
    mine,
    extras,
    json,
    spyModal,
    eventCalls
} from "@tests/_common/calendarSuite";

// wwwroot/admin/custom/Calendar/AdminIndex/js/site.js

/** Suffixes of the `localizer*` hidden inputs the page publishes. */
const LOCALIZERS = [
    "Email", "Notification", "Minutes", "Hours", "Days", "Weeks", "BeforeAt", "IETFLanguageTag",
    "Today", "Month", "ConfirmDelete", "ThisFieldRequired", "ErrorInvalidNumber",
    "ErrorRangeMinute", "ErrorRangeHour", "ErrorRangeDay", "ErrorRangeWeek", "FailedToLoadCalendars",
];

/** The admin calendar page DOM with empty calendar and event data. */
function fixture(): string {
    return (
        antiForgery +
        hidden("maxAttachedFileSizeBytes", "1048576") +
        hidden("minLeadTimeBeforeEvent", "0") +
        hidden("maxMinutesBeforeEvent", "40320") +
        hidden("maxHoursBeforeEvent", "672") +
        hidden("maxDaysBeforeEvent", "28") +
        hidden("maxWeeksBeforeEvent", "4") +
        hidden("reminderTimeIntervals", "[&quot;09:00&quot;,&quot;18:30&quot;]") +
        hidden("defaultReminderTimeOfDay", "09:00") +
        hidden("calendarNameCulture", "en-US") +
        hidden("loggedInAccountTimeZoneIanaId", "Asia/Seoul") +
        hidden("calendarEventOutputViewModels", "[]") +
        hidden("otherCalendarEventOutputViewModels", "[]") +
        LOCALIZERS.map((n) => hidden(`localizer${n}`, `L_${n}`)).join("") +
        `<div id="calendar"></div>
     <div id="createCalendarTabs"></div><div id="editCalendarTabs"></div>
     <div id="createCalendarEventTaskTabs"></div><div id="editCalendarEventTaskTabs"></div>
     <div id="viewCalendarEventTaskTabs"></div><div id="browseCalendarsOfInterestTabs"></div>
     <div id="createCalendarEventDescription"></div><div id="editCalendarEventDescription"></div>
     <div id="viewCalendarEventDescription"></div>
     <input type="file" id="createCalendarEventAttachment" data-errorMessage="big" />
     <input type="file" id="editCalendarEventAttachment" data-errorMessage="big" />
     <input type="checkbox" id="createCalendarEventAllDay" /><input type="checkbox" id="editCalendarEventAllDay" />
     ${["createCalendarEventAllDayUncheckedStartDate", "createCalendarEventAllDayUncheckedEndDate",
            "createCalendarEventAllDayCheckedStartDate", "createCalendarEventAllDayCheckedEndDate",
            "editCalendarEventAllDayUncheckedStartDate", "editCalendarEventAllDayUncheckedEndDate",
            "editCalendarEventAllDayCheckedStartDate", "editCalendarEventAllDayCheckedEndDate"]
            .map((id) => `<input id="${id}" />`).join("")}
     ${["divCreateEventAllDayUnchecked", "divCreateEventAllDayChecked",
            "divCreateEventNotificationAllDayUnchecked", "divCreateEventNotificationAllDayChecked",
            "divEditEventAllDayUnchecked", "divEditEventAllDayChecked",
            "divEditEventNotificationAllDayUnchecked", "divEditEventNotificationAllDayChecked"]
            .map((id) => `<div id="${id}"></div>`).join("")}
     <select id="createCalendarEventMyCalendar"></select><select id="editCalendarEventMyCalendar"></select>
     <div id="dropdown-content"></div><i id="dropdown-icon"></i>
     <a id="aCreateCalendar"></a><a id="aCreateCalendarEvent"></a>
     <form id="formCreateCalendar">
       <input id="createCalendarName" /><input id="createCalendarDescription" />
       <input id="createCalendarHtmlColorCode" value="#3788d8" /><input id="createCalendarTimeZone" value="UTC" />
     </form>
     <form id="formEditCalendar"></form>
     <form id="formCreateCalendarEvent"></form><form id="formEditCalendarEvent"></form>
     <div id="myCalendars"></div>
     <a id="aEditCalendarEventAttachedFile"></a>
     <a id="editCalendarEventPopup" href="#"></a>
     <input id="editCalendarEventStatus" />
     <a id="aUpdateCalendarShared"></a><div id="setCalendarSharedDialogModal"></div><div id="divSetCalendarShared"></div>
     <div class="divCreateEventNotificationAllDayCheckedRow">
       <input class="createCalendarEventSelNotificationNumberAllDayChecked" />
       <select class="createCalendarEventSelNotificationTimeTypeAllDayChecked">
         <option value="Days" selected>Days</option><option value="Weeks">Weeks</option>
       </select>
       <div class="error-message" style="display:none"></div>
     </div>`
    );
}

describe("admin/Calendar/AdminIndex", () => {
    it("loads without throwing (FullCalendar / moment / datepicker stubs suffice)", () => {
        expect(() => loadSite("admin", "Calendar", "AdminIndex", fixture())).not.toThrow();
    });

    it("flags a day-reminder above CalendarReminderPolicy.MaxDaysBeforeEvent (28)", () => {
        const h = loadSite("admin", "Calendar", "AdminIndex", fixture());
        const row = h.$(".divCreateEventNotificationAllDayCheckedRow");
        row.find(".createCalendarEventSelNotificationNumberAllDayChecked").val("99").trigger("input");
        expect(row.find(".error-message").text()).toBe("L_ErrorRangeDay");
    });

    it("accepts an in-range day reminder", () => {
        const h = loadSite("admin", "Calendar", "AdminIndex", fixture());
        const row = h.$(".divCreateEventNotificationAllDayCheckedRow");
        row.find(".createCalendarEventSelNotificationNumberAllDayChecked").val("7").trigger("input");
        expect(row.find(".error-message").text()).toBe("");
    });

    it("'add event' fetches the calendar list from /Calendar/GetCalendars", () => {
        const h = loadSite("admin", "Calendar", "AdminIndex", fixture());
        h.$("#aCreateCalendarEvent").trigger("click");
        expect(h.ajaxCalls.some((c) => c.url === "/Calendar/GetCalendars")).toBe(true);
    });

    // Regression: this call used to set `async: false`, freezing the page for the round trip.
    // It must run asynchronous, with the modal opened from `complete` once the fetch settles.
    it("'add event' fetches the calendar list asynchronously and opens the modal once it settles", () => {
        const h = loadSite("admin", "Calendar", "AdminIndex", fixture());
        h.$("#aCreateCalendarEvent").trigger("click");

        const call = h.lastAjax();
        expect(call.url).toBe("/Calendar/GetCalendars");
        expect(call.async).not.toBe(false);

        call.success?.({ result: true, calendars: [{ id: 1, name: "Cal 1", htmlColorCode: "#123456" }] });
        call.complete?.();

        expect(h.$("#createCalendarEventMyCalendar option").length).toBe(1);
    });

    // Regression: a fetch failure used to have no `error` handler at all; the create-event
    // modal must still open (degraded — empty calendar select) instead of silently hanging.
    it("'add event' shows an error toast and still opens the modal when the fetch fails", () => {
        const h = loadSite("admin", "Calendar", "AdminIndex", fixture());
        h.$("#aCreateCalendarEvent").trigger("click");

        const call = h.lastAjax();
        call.error?.();
        call.complete?.();

        expect(h.toastr.error).toHaveBeenCalledWith("L_FailedToLoadCalendars");
    });

    it("'shared calendars' dialog whose list the server refuses stays empty but still opens", () => {
        const h = loadSite("admin", "Calendar", "AdminIndex", fixture());
        h.$("#aUpdateCalendarShared").trigger("click");
        const call = h.lastAjax();
        call.success?.({ result: false });
        call.complete?.();
        expect(present(h.win.document.getElementById("divSetCalendarShared")).innerHTML.trim()).toBe("");
    });

    // Regression: the "shared calendars" dialog used `async: false` to guarantee the row list
    // was built before the modal opened; it must now defer opening to `complete` instead.
    it("'shared calendars' dialog fetches asynchronously and opens once the fetch settles", () => {
        const h = loadSite("admin", "Calendar", "AdminIndex", fixture());
        h.$("#aUpdateCalendarShared").trigger("click");

        const call = h.lastAjax();
        expect(call.url).toBe("/Calendar/GetCalendarShareds");
        expect(call.async).not.toBe(false);

        call.success?.({ result: true, setCalendarShareds: [{ id: 1, name: "Cal 1", user: true, guest: false }] });
        call.complete?.();

        expect(present(h.win.document.getElementById("divSetCalendarShared")).innerHTML).toContain("Cal 1");
    });

    // Regression: a calendar name is free text the owner controls. It used to be interpolated
    // straight into a raw HTML template literal with no escaping, so a name containing markup would
    // be parsed as HTML by every viewer who saw it (the sidebar list here, and the "shared
    // calendars" dialog below) instead of being displayed as plain text.
    it("escapes a malicious calendar name before appending it to the sidebar list", () => {
        const h = loadSite("admin", "Calendar", "AdminIndex", fixture());
        h.$("#createCalendarName").val(`<img src=x alt="">`);

        h.$("#formCreateCalendar").trigger("submit");
        const call = h.lastAjax();
        expect(call.url).toBe("/Calendar/CreateCalendar");
        call.success?.({
            result: true,
            message: "created",
            calendar: { id: 1, name: "<img src=x alt=\"\">", htmlColorCode: "#3788d8" }
        });

        const myCalendarsHtml = present(h.win.document.getElementById("myCalendars")).innerHTML;
        expect(myCalendarsHtml).not.toContain("<img");
        expect(myCalendarsHtml).toContain("&lt;img src=x alt=\"\"&gt;");
        expect(present(h.win.document.getElementById("myCalendars")).querySelector("img")).toBeNull();
    });

    /** Minimal payload accepted by IsCalendarEventExists's success handler. */
    function makeCalendarEventPayload(overrides: Partial<Record<string, unknown>> = {}) {
        return {
            result: true,
            calendarEvent: {
                id: 1,
                title: "Team sync",
                allDay: true,
                displayStartDate: "2024-01-01",
                displayEndDate: "2024-01-02",
                startDateTimeZoneIanaId: "UTC",
                endDateTimeZoneIanaId: "UTC",
                location: "",
                description: "<p>hello</p>",
                calendarEventAttachedFile: null,
                calendarId: 5,
                status: "Confirmed",
                serializedCalendarReminders: "[]",
                ...overrides,
            },
        };
    }

    // Regression: the drag-select "new event" flow used `async: false` on its GetCalendars fetch.
    // It must run asynchronous, with the modal opened from `complete` once the fetch settles.
    it("drag-selecting a date range fetches the calendar list asynchronously and opens the modal once settled", () => {
        const h = loadSite("admin", "Calendar", "AdminIndex", fixture());

        calendarSelect(h, { start: new Date("2024-01-01"), end: new Date("2024-01-02") });

        const call = h.lastAjax();
        expect(call.url).toBe("/Calendar/GetCalendars");
        expect(call.async).not.toBe(false);

        call.success?.({ result: true, calendars: [{ id: 1, name: "Cal 1", htmlColorCode: "#123456" }] });
        call.complete?.();

        expect(h.$("#createCalendarEventMyCalendar option").length).toBe(1);
    });

    /** Drives the "click an event -> click 'edit' in its popup" flow via the captured `eventClick` callback. */
    function openEditPopup(h: ReturnType<typeof loadSite>): void {
        const eventEl = h.win.document.createElement("div");
        h.win.document.body.appendChild(eventEl);
        calendarEventClick(h, {
            el: eventEl,
            event: {
                id: 1,
                title: "Team sync",
                allDay: true,
                extendedProps: eventProps({ displayStartDate: "2024-01-01", displayEndDate: "2024-01-02" }),
            },
        });
        h.$("#editCalendarEventPopup").trigger("click");
    }

    // Regression: the edit-event modal's GetCalendars fetch used `async: false` too; it must run
    // asynchronous, with the rest of the edit modal built from `complete` once settled.
    it("editing an event fetches the calendar list asynchronously and still builds the modal once settled", () => {
        const h = loadSite("admin", "Calendar", "AdminIndex", fixture());
        openEditPopup(h);

        const outerCall = h.lastAjax();
        expect(outerCall.url).toContain("/Calendar/IsCalendarEventExists");
        outerCall.success?.(makeCalendarEventPayload());

        const innerCall = h.lastAjax();
        expect(innerCall.url).toBe("/Calendar/GetCalendars");
        expect(innerCall.async).not.toBe(false);

        innerCall.success?.({ result: true, calendars: [{ id: 5, name: "Cal 5", htmlColorCode: "#123456" }] });
        innerCall.complete?.();

        // Only set inside showEditCalendarEventModal, which now runs from `complete`.
        expect(h.$("#editCalendarEventStatus").val()).toBe("Confirmed");
        expect(h.$("#editCalendarEventMyCalendar option").length).toBe(1);
    });

    // Regression: a fetch failure used to have no `error` handler; the edit modal must still be
    // built (degraded — empty calendar select) instead of silently never opening.
    it("editing an event shows an error toast and still builds the modal when GetCalendars fails", () => {
        const h = loadSite("admin", "Calendar", "AdminIndex", fixture());
        openEditPopup(h);

        h.lastAjax().success?.(makeCalendarEventPayload());

        const innerCall = h.lastAjax();
        innerCall.error?.();
        innerCall.complete?.();

        expect(h.toastr.error).toHaveBeenCalledWith("L_FailedToLoadCalendars");
        expect(h.$("#editCalendarEventStatus").val()).toBe("Confirmed");
    });

    it("escapes a malicious calendar name in the 'shared calendars' dialog", () => {
        const h = loadSite("admin", "Calendar", "AdminIndex", fixture());

        h.$("#aUpdateCalendarShared").trigger("click");
        const call = h.lastAjax();
        expect(call.url).toBe("/Calendar/GetCalendarShareds");
        call.success?.({
            result: true,
            setCalendarShareds: [{ id: 1, name: `<img src=x alt="">`, user: true, guest: false }],
        });

        const html = present(h.win.document.getElementById("divSetCalendarShared")).innerHTML;
        expect(html).not.toContain("<img");
        expect(html).toContain("&lt;img src=x alt=\"\"&gt;");
    });

    it("CreateCalendarEventUploadedFile/EditCalendarEventUploadedFile do not throw when change fires with no file selected", () => {
        const h = loadSite("admin", "Calendar", "AdminIndex", fixture());
        expect(() => h.$("#createCalendarEventAttachment").trigger("change")).not.toThrow();
        expect(() => h.$("#editCalendarEventAttachment").trigger("change")).not.toThrow();
    });
});

// ==== extended coverage ===================================================================================

/** Loads the admin calendar page with the "my calendars" list, `opts.my` as its events and the shared extras (the admin page has no "other" calendars). */
function build(opts: { my?: unknown[]; other?: unknown[]; body?: (html: string) => string } = {}) {
    let html = fixture()
            .replace("<div id=\"myCalendars\"></div>", mine)
            .replace(hidden("calendarEventOutputViewModels", "[]"), hidden("calendarEventOutputViewModels", json(opts.my ?? [])))
        + extras + "<form id=\"formUpdateCalendarShared\"></form>";
    if (opts.body) html = opts.body(html);
    return loadSite("admin", "Calendar", "AdminIndex", html);
}

// the admin page has no "other calendars": only its own checked calendars (1 and 3) are ever requested
describeCalendarCommon({ label: "admin/Calendar/AdminIndex", build, checkedIds: [{ Id: 1 }, { Id: 3 }] });
describeEditFormDetail({ label: "admin/Calendar/AdminIndex", build });
describeCalendarExtras({ label: "admin/Calendar/AdminIndex", build });

describe("admin/Calendar/AdminIndex — events on the grid", () => {
    it("seeds the grid with the admin's own events, coloured by their calendar", () => {
        const h = build({ my: [evt(1, 1), evt(2, 3)] });
        const cal = firstCalendar(h);
        expect(cal.addEvent.mock.calls.map((c) => [c[0]["id"], c[0]["title"], c[0]["backgroundColor"], c[0].extendedProps["calendarId"]])).toEqual([
            ["1", "Event 1", "#123456", 1], ["2", "Event 2", "#123456", 3]]);
        expect(cal.render).toHaveBeenCalled();
    });

    it("toggling a calendar clears the grid and requests exactly the checked calendars", () => {
        const h = build();
        h.$(".chkCalendar").first().trigger("change");
        expect(firstCalendar(h).removeAllEvents).toHaveBeenCalledTimes(1);
        expect(JSON.parse(String(present(eventCalls(h)[0]).data))).toEqual({ Calendars: [{ Id: 1 }, { Id: 3 }] });
        expect(present(eventCalls(h)[0]).headers).toEqual({ RequestVerificationToken: "tok" });
    });

    it("the reply's events are added; a stale reply that lands after a newer toggle is ignored", () => {
        const h = build();
        const cal = firstCalendar(h);
        h.$(".chkCalendar").first().trigger("change");
        h.$(".chkCalendar").first().trigger("change");
        const [first, second] = eventCalls(h);
        successOf(second)({ result: true, calendarEvents: JSON.stringify([evt(20, 1)]) });
        successOf(first)({ result: true, calendarEvents: JSON.stringify([evt(10, 1)]) });
        expect(cal.addEvent.mock.calls.map((c) => c[0]["id"])).toEqual(["20"]);
    });

    it("a refused reply adds nothing", () => {
        const h = build();
        h.$(".chkCalendar").first().trigger("change");
        successOf(present(eventCalls(h)[0]))({ result: false });
        expect(firstCalendar(h).addEvent).not.toHaveBeenCalled();
    });
});

describe("admin/Calendar/AdminIndex — sharing settings", () => {
    /** Opens the sharing dialog and answers it with two calendars (one with an HTML name). */
    const open = (h: ReturnType<typeof build>) => {
        h.$("#aUpdateCalendarShared").trigger("click");
        successOf(h.lastAjax())({
            result: true,
            setCalendarShareds: [{ id: 1, name: "<b>Team</b>", user: true, guest: false }, { id: 2, name: "Private", user: false, guest: false }],
        });
    };

    it("the dialog lists every calendar with its current user/guest flags, HTML-escaped, and opens once the fetch settles", () => {
        const h = build();
        const modal = spyModal(h);
        h.$("#aUpdateCalendarShared").trigger("click");
        expect(h.lastAjax().url).toBe("/Calendar/GetCalendarShareds");
        successOf(h.lastAjax())({
            result: true,
            setCalendarShareds: [{ id: 1, name: "<b>Team</b>", user: true, guest: false }, { id: 2, name: "Private", user: false, guest: true }]
        });
        completeOf(h.lastAjax())();

        expect(h.$(".setCalendarShared")).toHaveLength(2);
        expect(h.$(".setCalendarShared").map((_, r) => [h.$(r).find(".shareToUser").prop("checked"), h.$(r).find(".shareToGuest").prop("checked")].join()).get()).toEqual(["true,false", "false,true"]);
        expect(h.$("#divSetCalendarShared b")).toHaveLength(0); // escaped, not interpreted
        expect(modal).toHaveBeenCalledWith("show");
    });

    it("a failed fetch toasts and still opens the (empty) dialog", () => {
        const h = build();
        const modal = spyModal(h);
        h.$("#aUpdateCalendarShared").trigger("click");
        errorOf(h.lastAjax())();
        completeOf(h.lastAjax())();
        expect(h.toastr.error).toHaveBeenCalledWith("L_FailedToLoadCalendars");
        expect(modal).toHaveBeenCalledWith("show");
    });

    it("saving posts one {CalendarId, User, Anonymous} row per calendar with the ticked flags; success closes the dialog and toasts", () => {
        const h = build();
        const modal = spyModal(h);
        open(h);
        h.$(".setCalendarShared").last().find(".shareToGuest").prop("checked", true);
        const ev = h.$.Event("submit");
        h.$("#formUpdateCalendarShared").trigger(ev);

        const call = h.lastAjax();
        expect(ev.isDefaultPrevented()).toBe(true);
        expect(call.url).toBe("/Calendar/UpdateCalendarShared");
        expect(JSON.parse(String(call.data))).toEqual([
            { CalendarId: "1", User: true, Anonymous: false },
            { CalendarId: "2", User: false, Anonymous: true },
        ]);

        successOf(call)({ result: true, message: "shared" });
        expect(modal).toHaveBeenCalledWith("hide");
        expect(h.toastr.success).toHaveBeenCalledWith("shared");
    });

    it("a refused save toasts and keeps the dialog open", () => {
        const h = build();
        const modal = spyModal(h);
        open(h);
        h.$("#formUpdateCalendarShared").trigger("submit");
        successOf(h.lastAjax())({ result: false, error: "not yours" });
        expect(h.toastr.error).toHaveBeenCalledWith("not yours");
        expect(modal).not.toHaveBeenCalledWith("hide");
    });
});

describeMissingServerConstants("admin", "Calendar", "AdminIndex", () => fixture());
describeRequiredServerConstants("admin", "Calendar", "AdminIndex", () => fixture(), ["maxAttachedFileSizeBytes", "defaultReminderTimeOfDay"]);

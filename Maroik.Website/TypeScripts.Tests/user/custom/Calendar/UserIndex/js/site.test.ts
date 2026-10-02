import { describe, it, expect, vi } from "vitest";
import { describeMissingServerConstants } from "@tests/_common/missingConfigSuite";
import { loadSite, antiForgery, hidden } from "@tests/_common/harness";
import {
    describeCalendarCommon,
    describeEditFormDetail,
    describeCalendarExtras,
    evt,
    mine,
    extras,
    json,
    spyModal,
    eventCalls,
    type Handle
} from "@tests/_common/calendarSuite";

// wwwroot/user/custom/Calendar/UserIndex/js/site.js

/** Suffixes of the `localizer*` hidden inputs the page publishes. */
const LOCALIZERS = [
    "Email", "Notification", "Minutes", "Hours", "Days", "Weeks", "BeforeAt", "IETFLanguageTag",
    "Today", "Month", "ConfirmDelete", "ThisFieldRequired", "ErrorInvalidNumber",
    "ErrorRangeMinute", "ErrorRangeHour", "ErrorRangeDay", "ErrorRangeWeek", "FailedToLoadCalendars",
];

/** The user calendar page DOM with empty calendar and event data. */
function fixture(): string {
    return (
        antiForgery +
        hidden("maxAttachedFileSizeBytes", "1048576") +
        hidden("maxMinutesBeforeEvent", "40320") +
        hidden("maxHoursBeforeEvent", "672") +
        hidden("maxDaysBeforeEvent", "28") +
        hidden("maxWeeksBeforeEvent", "4") +
        hidden("reminderTimeIntervals", "[&quot;09:00&quot;,&quot;18:30&quot;]") +
        hidden("defaultReminderTimeOfDay", "09:00") +
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
     <a id="aEditCalendarEventAttachedFile"></a><a id="aViewCalendarEventAttachedFile"></a>
     <a id="editCalendarEventPopup" href="#"></a><input id="editCalendarEventStatus" />
     <a id="viewOtherCalendarEventPopup" href="#"></a><input id="viewCalendarEventStatus" />
     <select id="viewCalendarEventMyCalendar"></select>
     <a id="aBrowseCalendarsOfInterest"></a><input type="checkbox" id="allCheckBrowseCalendarsOfInterest" />
     <div id="otherCalendars"></div><div id="divBrowseCalendarsOfInterest"></div>
     <form id="formUpdateBrowseCalendarsOfInterest"></form>
     <div id="browseCalendarsOfInterestDialogModal"></div>
     <div class="divCreateEventNotificationAllDayCheckedRow">
       <input class="createCalendarEventSelNotificationNumberAllDayChecked" />
       <select class="createCalendarEventSelNotificationTimeTypeAllDayChecked">
         <option value="Days" selected>Days</option><option value="Weeks">Weeks</option>
       </select>
       <div class="error-message" style="display:none"></div>
     </div>`
    );
}

describe("user/Calendar/UserIndex", () => {
    it("loads without throwing", () => {
        expect(() => loadSite("user", "Calendar", "UserIndex", fixture())).not.toThrow();
    });

    it("flags a day-reminder above CalendarReminderPolicy.MaxDaysBeforeEvent (28)", () => {
        const h = loadSite("user", "Calendar", "UserIndex", fixture());
        const row = h.$(".divCreateEventNotificationAllDayCheckedRow");
        row.find(".createCalendarEventSelNotificationNumberAllDayChecked").val("40").trigger("input");
        expect(row.find(".error-message").text()).toBe("L_ErrorRangeDay");
    });

    it("'add event' fetches /Calendar/GetCalendars", () => {
        const h = loadSite("user", "Calendar", "UserIndex", fixture());
        h.$("#aCreateCalendarEvent").trigger("click");
        expect(h.ajaxCalls.some((c) => c.url === "/Calendar/GetCalendars")).toBe(true);
    });

    // Regression: this call used to set `async: false`, freezing the page for the round trip.
    // It must run asynchronous, with the modal opened from `complete` once the fetch settles.
    it("'add event' fetches the calendar list asynchronously and opens the modal once it settles", () => {
        const h = loadSite("user", "Calendar", "UserIndex", fixture());
        h.$("#aCreateCalendarEvent").trigger("click");

        const call = h.lastAjax();
        expect(call.async).not.toBe(false);

        call.success?.({ result: true, calendars: [{ id: 1, name: "Cal 1" }] });
        call.complete?.();

        expect(h.$("#createCalendarEventMyCalendar option").length).toBe(1);
    });

    // Regression: a fetch failure used to have no `error` handler at all; the create-event
    // modal must still open (degraded — empty calendar select) instead of silently hanging.
    it("'add event' shows an error toast and still opens the modal when the fetch fails", () => {
        const h = loadSite("user", "Calendar", "UserIndex", fixture());
        h.$("#aCreateCalendarEvent").trigger("click");

        const call = h.lastAjax();
        call.error?.();
        call.complete?.();

        expect(h.toastr.error).toHaveBeenCalledWith("L_FailedToLoadCalendars");
    });

    // Regression: the "browse calendars of interest" dialog used `async: false` to guarantee
    // the row list was built before the modal opened; it must now defer to `complete` instead.
    it("'browse calendars of interest' dialog fetches asynchronously and opens once settled", () => {
        const h = loadSite("user", "Calendar", "UserIndex", fixture());
        h.$("#aBrowseCalendarsOfInterest").trigger("click");

        const call = h.lastAjax();
        expect(call.url).toBe("/Calendar/GetBrowseCalendarsOfInterest");
        expect(call.async).not.toBe(false);

        call.success?.({ result: true, browseCalendarsOfInterests: [{ id: 1, name: "Cal 1", checked: false }] });
        call.complete?.();

        expect(h.win.document.getElementById("divBrowseCalendarsOfInterest")!.innerHTML).toContain("Cal 1");
    });

    /** Minimal payload accepted by IsCalendarEventExists / IsOtherCalendarEventExists's success handlers. */
    function makeCalendarEventPayload(overrides: Partial<Record<string, unknown>> = {}) {
        return {
            result: true,
            calendarEvent: {
                id: 1,
                title: "Team sync",
                allDay: true,
                displayStartDate: "2024-01-01",
                displayEndDate: "2024-01-02",
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
        const h = loadSite("user", "Calendar", "UserIndex", fixture());

        h.calendarOptions[0].select({ start: new Date("2024-01-01"), end: new Date("2024-01-02") });

        const call = h.lastAjax();
        expect(call.url).toBe("/Calendar/GetCalendars");
        expect(call.async).not.toBe(false);

        call.success?.({ result: true, calendars: [{ id: 1, name: "Cal 1" }] });
        call.complete?.();

        expect(h.$("#createCalendarEventMyCalendar option").length).toBe(1);
    });

    /** Drives the "click a 'My' event -> click 'edit' in its popup" flow via the captured `eventClick` callback. */
    function openEditPopup(h: ReturnType<typeof loadSite>): void {
        const eventEl = h.win.document.createElement("div");
        h.win.document.body.appendChild(eventEl);
        h.calendarOptions[0].eventClick({
            el: eventEl,
            event: {
                id: 1,
                title: "Team sync",
                allDay: true,
                extendedProps: { calendarType: "My", displayStartDate: "2024-01-01", displayEndDate: "2024-01-02" },
            },
        });
        h.$("#editCalendarEventPopup").trigger("click");
    }

    // Regression: the edit-event modal's GetCalendars fetch used `async: false` too; it must run
    // asynchronous, with the rest of the edit modal built from `complete` once settled.
    it("editing a 'My' event fetches the calendar list asynchronously and still builds the modal once settled", () => {
        const h = loadSite("user", "Calendar", "UserIndex", fixture());
        openEditPopup(h);

        const outerCall = h.lastAjax();
        expect(outerCall.url).toContain("/Calendar/IsCalendarEventExists");
        outerCall.success?.(makeCalendarEventPayload());

        const innerCall = h.lastAjax();
        expect(innerCall.url).toBe("/Calendar/GetCalendars");
        expect(innerCall.async).not.toBe(false);

        innerCall.success?.({ result: true, calendars: [{ id: 5, name: "Cal 5" }] });
        innerCall.complete?.();

        expect(h.$("#editCalendarEventStatus").val()).toBe("Confirmed");
        expect(h.$("#editCalendarEventMyCalendar option").length).toBe(1);
    });

    // Regression: a fetch failure used to have no `error` handler; the edit modal must still be
    // built (degraded — empty calendar select) instead of silently never opening.
    it("editing a 'My' event shows an error toast and still builds the modal when GetCalendars fails", () => {
        const h = loadSite("user", "Calendar", "UserIndex", fixture());
        openEditPopup(h);

        h.lastAjax().success?.(makeCalendarEventPayload());

        const innerCall = h.lastAjax();
        innerCall.error?.();
        innerCall.complete?.();

        expect(h.toastr.error).toHaveBeenCalledWith("L_FailedToLoadCalendars");
        expect(h.$("#editCalendarEventStatus").val()).toBe("Confirmed");
    });

    describe.each(["#aViewCalendarEventAttachedFile"])("the attachment link %s", (link) => {
        /** The page with the link naming event 77's attachment "report.zip". */
        const withAttachment = () => {
            const h = loadSite("user", "Calendar", "UserIndex", fixture());
            h.$(link).attr({ "data-calendareventid": "77", "data-name": "report.zip" });
            return h;
        };

        it("asks the download endpoint for the event's attachment as a blob", () => {
            const h = withAttachment();
            const ev = h.$.Event("click");
            h.$(link).trigger(ev);

            expect(ev.isDefaultPrevented()).toBe(true);
            const call = h.lastAjax() as any;
            expect(call.url).toBe("/Calendar/DownloadCalendarEventAttachedFile");
            expect(call.type).toBe("POST");
            expect(call.data).toEqual({ calendarEventId: "77" });
            expect(call.headers).toEqual({ RequestVerificationToken: "tok" });
            expect(call.xhrFields).toEqual({ responseType: "blob" });
        });

        it("saves the returned file under the attachment's name and releases its object URL shortly after", () => {
            const click = vi.spyOn(window.HTMLAnchorElement.prototype, "click").mockImplementation(function() { /* jsdom cannot navigate */
            });
            try {
                const h = withAttachment();
                (h.win as any).URL.createObjectURL = () => "blob:attachment";
                const revoke = ((h.win as any).URL.revokeObjectURL = vi.fn());
                h.$(link).trigger("click");
                vi.useFakeTimers();
                try {
                    h.respond(0, new h.win.Blob(["zip-bytes"], { type: "application/zip" }));
                    expect(click).toHaveBeenCalledTimes(1);
                    expect((click.mock.contexts[0] as HTMLAnchorElement).download).toBe("report.zip");
                    expect(revoke).not.toHaveBeenCalled();
                    vi.advanceTimersByTime(100);
                    expect(revoke).toHaveBeenCalledWith("blob:attachment");
                } finally {
                    vi.useRealTimers();
                }
            } finally {
                click.mockRestore();
            }
        });

        it("shows a refusal as the server's error and saves nothing; without an event id it does nothing", async () => {
            const click = vi.spyOn(window.HTMLAnchorElement.prototype, "click").mockImplementation(function() { /* noop */
            });
            try {
                const h = withAttachment();
                h.$(link).trigger("click");
                h.respond(0, new h.win.Blob([JSON.stringify({ result: false, error: "The calendar event could not be found." })], { type: "application/json; charset=utf-8" }));
                await vi.waitFor(() => expect(h.toastr.error).toHaveBeenCalledWith("The calendar event could not be found."));
                expect(click).not.toHaveBeenCalled();

                const without = loadSite("user", "Calendar", "UserIndex", fixture());
                const before = without.ajaxCalls.length;
                without.$(link).trigger("click");
                expect(without.ajaxCalls).toHaveLength(before);
            } finally {
                click.mockRestore();
            }
        });
    });

    /** Drives the "click an 'Other' event -> click 'view' in its popup" flow via `eventClick`. */
    function openViewPopup(h: ReturnType<typeof loadSite>): void {
        const eventEl = h.win.document.createElement("div");
        h.win.document.body.appendChild(eventEl);
        h.calendarOptions[0].eventClick({
            el: eventEl,
            event: {
                id: "other-1",
                title: "Team sync",
                allDay: true,
                extendedProps: { calendarType: "Other", displayStartDate: "2024-01-01", displayEndDate: "2024-01-02" },
            },
        });
        h.$("#viewOtherCalendarEventPopup").trigger("click");
    }

    // Regression: the read-only view modal's GetOtherCalendars fetch used `async: false` too; it
    // must run asynchronous, with the rest of the modal built from `complete` once settled.
    it("viewing an 'Other' event fetches the calendar list asynchronously and still builds the modal once settled", () => {
        const h = loadSite("user", "Calendar", "UserIndex", fixture());
        openViewPopup(h);

        const outerCall = h.lastAjax();
        expect(outerCall.url).toContain("/Calendar/IsOtherCalendarEventExists");
        outerCall.success?.(makeCalendarEventPayload());

        const innerCall = h.lastAjax();
        expect(innerCall.url).toBe("/Calendar/GetOtherCalendars");
        expect(innerCall.async).not.toBe(false);

        innerCall.success?.({ result: true, tempOtherCalendars: [{ id: 5, name: "Shared" }] });
        innerCall.complete?.();

        expect(h.$("#viewCalendarEventStatus").val()).toBe("Confirmed");
        expect(h.$("#viewCalendarEventMyCalendar option").length).toBe(1);
    });

    // Regression: a fetch failure used to have no `error` handler; the read-only modal must still
    // be built (degraded — empty calendar select) instead of silently never opening.
    it("viewing an 'Other' event shows an error toast and still builds the modal when GetOtherCalendars fails", () => {
        const h = loadSite("user", "Calendar", "UserIndex", fixture());
        openViewPopup(h);

        h.lastAjax().success?.(makeCalendarEventPayload());

        const innerCall = h.lastAjax();
        innerCall.error?.();
        innerCall.complete?.();

        expect(h.toastr.error).toHaveBeenCalledWith("L_FailedToLoadCalendars");
        expect(h.$("#viewCalendarEventStatus").val()).toBe("Confirmed");
    });

    // Regression: a calendar name is free text the owner controls. It used to be interpolated
    // straight into a raw HTML template literal with no escaping, so a name containing markup would
    // be parsed as HTML by every viewer who saw it (the sidebar list here, and the "browse calendars
    // of interest" dialog below) instead of being displayed as plain text.
    it("escapes a malicious calendar name before appending it to the sidebar list", () => {
        const h = loadSite("user", "Calendar", "UserIndex", fixture());
        h.$("#createCalendarName").val(`<img src=x alt="">`);

        h.$("#formCreateCalendar").trigger("submit");
        const call = h.lastAjax();
        expect(call.url).toBe("/Calendar/CreateCalendar");
        call.success?.({
            result: true,
            calendar: { id: 1, name: "<img src=x alt=\"\">", htmlColorCode: "#3788d8" },
        });

        const myCalendarsHtml = h.win.document.getElementById("myCalendars")!.innerHTML;
        expect(myCalendarsHtml).not.toContain("<img");
        expect(myCalendarsHtml).toContain("&lt;img src=x alt=\"\"&gt;");
    });

    it("escapes a malicious calendar name in the 'browse calendars of interest' dialog", () => {
        const h = loadSite("user", "Calendar", "UserIndex", fixture());

        h.$("#aBrowseCalendarsOfInterest").trigger("click");
        const call = h.lastAjax();
        expect(call.url).toBe("/Calendar/GetBrowseCalendarsOfInterest");
        call.success?.({
            result: true,
            browseCalendarsOfInterests: [{ id: 1, name: `<img src=x alt="">`, checked: false }],
        });

        const html = h.win.document.getElementById("divBrowseCalendarsOfInterest")!.innerHTML;
        expect(html).not.toContain("<img");
        expect(html).toContain("&lt;img src=x alt=\"\"&gt;");
    });

    it("CreateCalendarEventUploadedFile/EditCalendarEventUploadedFile do not throw when change fires with no file selected", () => {
        const h = loadSite("user", "Calendar", "UserIndex", fixture());
        expect(() => h.$("#createCalendarEventAttachment").trigger("change")).not.toThrow();
        expect(() => h.$("#editCalendarEventAttachment").trigger("change")).not.toThrow();
    });
});

// ==== extended coverage: events, calendars of interest and the shared calendar behaviour ===============================

/** "Other calendars" checkboxes 9 (checked) and 8. */
const others = `<div id="otherCalendars">
  <label id="lblOtherCalendar9"><input type="checkbox" class="chkOtherCalendar" checked /></label>
  <label id="lblOtherCalendar8"><input type="checkbox" class="chkOtherCalendar" /></label></div>`;

/** Loads the user calendar page with both calendar lists, `opts.my` / `opts.other` as the event data and the shared extras. */
function build(opts: { my?: unknown[]; other?: unknown[]; body?: (html: string) => string } = {}) {
    let html = fixture()
            .replace("<div id=\"myCalendars\"></div>", mine)
            .replace("<div id=\"otherCalendars\"></div>", others)
            .replace(hidden("calendarEventOutputViewModels", "[]"), hidden("calendarEventOutputViewModels", json(opts.my ?? [])))
            .replace(hidden("otherCalendarEventOutputViewModels", "[]"), hidden("otherCalendarEventOutputViewModels", json(opts.other ?? [])))
        + extras;
    if (opts.body) html = opts.body(html);
    return loadSite("user", "Calendar", "UserIndex", html);
}

describeCalendarCommon({ label: "user/Calendar/UserIndex", build, checkedIds: [{ Id: 1 }, { Id: 3 }, { Id: 9 }] });
describeEditFormDetail({ label: "user/Calendar/UserIndex", build });
describeCalendarExtras({ label: "user/Calendar/UserIndex", build });

describe("user/Calendar/UserIndex — events on the grid", () => {
    it("seeds the grid with my events (typed 'My') and other calendars' events (typed 'Other')", () => {
        const h = build({ my: [evt(1, 1)], other: [evt(2, 9)] });
        const cal = h.calendarInstances[0];
        expect(cal.addEvent.mock.calls.map((c: any[]) => [c[0].id, c[0].extendedProps.calendarType])).toEqual([[1, "My"], [2, "Other"]]);
        expect(cal.render).toHaveBeenCalled();
    });

    it("toggling my-calendar or other-calendar checkboxes clears the grid and requests exactly the checked ones, mine first", () => {
        const h = build();
        h.$(".chkCalendar").first().trigger("change");
        expect(h.calendarInstances[0].removeAllEvents).toHaveBeenCalledTimes(1);
        expect(JSON.parse(String(eventCalls(h)[0].data))).toEqual({ Calendars: [{ Id: 1 }, { Id: 3 }, { Id: 9 }] });

        h.$(".chkOtherCalendar").first().trigger("change");
        expect(eventCalls(h)).toHaveLength(2);
    });

    it("the reply's events are added with the type the server assigned; a stale reply is ignored", () => {
        const h = build();
        const cal = h.calendarInstances[0];
        h.$(".chkCalendar").first().trigger("change");
        h.$(".chkCalendar").first().trigger("change");
        const [first, second] = eventCalls(h);
        second.success!({ result: true, calendarEvents: JSON.stringify([evt(20, 1, "Other")]) });
        first.success!({ result: true, calendarEvents: JSON.stringify([evt(10, 1)]) });
        expect(cal.addEvent.mock.calls.map((c: any[]) => [c[0].id, c[0].extendedProps.calendarType])).toEqual([[20, "Other"]]);
    });
});

describe("user/Calendar/UserIndex — a refused events reply", () => {
    it("a reply the server refuses adds no events", () => {
        const h = build();
        h.$(".chkCalendar").first().trigger("click");
        const call = eventCalls(h).at(-1)!;
        const added = h.calendarInstances[0].addEvent.mock.calls.length;
        call.success!({ result: false });
        expect(h.calendarInstances[0].addEvent.mock.calls.length).toBe(added);
    });
});

describe("user/Calendar/UserIndex — calendars of interest", () => {
    /** Opens the "calendars of interest" dialog and answers it with calendars 11 (checked) and 12. */
    const openDialog = (h: Handle) => {
        h.$("#aBrowseCalendarsOfInterest").trigger("click");
        h.lastAjax().success!({
            result: true,
            browseCalendarsOfInterests: [{ id: 11, name: "One", checked: true }, { id: 12, name: "Two", checked: false }],
        });
    };

    it("the dialog's 'all' box reflects whether every entry is checked and toggles them all", () => {
        const h = build();
        openDialog(h);
        expect(h.$("#allCheckBrowseCalendarsOfInterest").prop("checked")).toBe(false);
        h.$(".checkBoxBrowseCalendarsOfInterest").last().prop("checked", true).trigger("change");
        expect(h.$("#allCheckBrowseCalendarsOfInterest").prop("checked")).toBe(true);
        h.$("#allCheckBrowseCalendarsOfInterest").prop("checked", false).trigger("change");
        expect(h.$(".checkBoxBrowseCalendarsOfInterest:checked")).toHaveLength(0);
    });

    it("saving posts the ids of the checked entries, then refreshes the sidebar and events, closes the dialog and toasts", () => {
        const h = build();
        const modal = spyModal(h);
        openDialog(h);
        const ev = h.$.Event("submit");
        h.$("#formUpdateBrowseCalendarsOfInterest").trigger(ev);
        const save = h.lastAjax();
        expect(ev.isDefaultPrevented()).toBe(true);
        expect(save.url).toBe("/Calendar/UpdateOtherCalendar");
        expect(JSON.parse(String(save.data))).toEqual([{ CalendarId: "11" }]);

        save.success!({ result: true, message: "saved" });
        const list = h.lastAjax();
        expect(list.url).toBe("/Calendar/GetOtherCalendars");
        list.success!({ result: true, tempOtherCalendars: [{ id: 11, name: "<i>One</i>", htmlColorCode: "#010203" }] });

        expect(h.$("#otherCalendars label[id^='lblOtherCalendar']").map((_, l) => l.id).get()).toEqual(["lblOtherCalendar11"]);
        expect(h.$("#otherCalendars").html()).toContain("&lt;i&gt;One&lt;/i&gt;");
        expect(JSON.parse(String(h.lastAjax().data))).toEqual({ Calendars: [{ Id: 1 }, { Id: 3 }, { Id: 11 }] });

        h.lastAjax().success!({ result: true, calendarEvents: JSON.stringify([evt(60, 11, "Other")]) });
        h.lastAjax().complete!();
        expect(h.calendarInstances[0].addEvent.mock.calls.at(-1)[0].id).toBe(60);
        expect(modal).toHaveBeenCalledWith("hide");
        expect(h.toastr.success).toHaveBeenCalledWith("saved");
    });

    it("the dialog's own list failing to load is toasted, and the dialog still opens", () => {
        const h = build();
        const modal = spyModal(h);
        h.$("#aBrowseCalendarsOfInterest").trigger("click");
        const call = h.lastAjax();
        call.error!();
        call.complete!();
        expect(h.toastr.error).toHaveBeenCalledWith("L_FailedToLoadCalendars");
        expect(modal).toHaveBeenCalledWith("show");
    });

    it("a refused calendars-of-interest list leaves the dialog empty but still opens it", () => {
        const h = build();
        const modal = spyModal(h);
        h.$("#aBrowseCalendarsOfInterest").trigger("click");
        const call = h.lastAjax();
        call.success!({ result: false });
        call.complete!();
        expect(h.$("#divBrowseCalendarsOfInterest").children()).toHaveLength(0);
        expect(modal).toHaveBeenCalledWith("show");
    });

    it("after a saved selection, a refused events reload adds nothing and the dialog still closes", () => {
        const h = build();
        const modal = spyModal(h);
        openDialog(h);
        h.$("#formUpdateBrowseCalendarsOfInterest").trigger("submit");
        h.lastAjax().success!({ result: true, message: "saved" });
        h.lastAjax().success!({ result: true, tempOtherCalendars: [{ id: 11, name: "One", htmlColorCode: "#010203" }] });
        const events = h.lastAjax();
        const added = h.calendarInstances[0].addEvent.mock.calls.length;
        events.success!({ result: false });
        events.complete!();
        expect(h.calendarInstances[0].addEvent.mock.calls.length).toBe(added);
        expect(modal).toHaveBeenCalledWith("hide");
    });

    it("after a saved selection, a refused list reload just closes the dialog", () => {
        const h = build();
        const modal = spyModal(h);
        openDialog(h);
        h.$("#formUpdateBrowseCalendarsOfInterest").trigger("submit");
        h.lastAjax().success!({ result: true, message: "saved" });
        h.lastAjax().success!({ result: false });
        expect(modal).toHaveBeenCalledWith("hide");
        expect(h.toastr.error).not.toHaveBeenCalled();
    });

    it("after a saved selection, a failing events reload is toasted and the dialog still closes", () => {
        const h = build();
        const modal = spyModal(h);
        openDialog(h);
        h.$("#formUpdateBrowseCalendarsOfInterest").trigger("submit");
        h.lastAjax().success!({ result: true, message: "saved" });
        h.lastAjax().success!({ result: true, tempOtherCalendars: [{ id: 11, name: "One", htmlColorCode: "#010203" }] });
        const events = h.lastAjax();
        expect(events.url).toBe("/Calendar/GetCalendarEvents");
        events.error!();
        events.complete!();
        expect(h.toastr.error).toHaveBeenCalledWith("L_FailedToLoadCalendars");
        expect(modal).toHaveBeenCalledWith("hide");
    });

    it("a failing list reload still closes the dialog (after an error toast); a refused save keeps it open", () => {
        const h = build();
        const modal = spyModal(h);
        openDialog(h);
        h.$("#formUpdateBrowseCalendarsOfInterest").trigger("submit");
        h.lastAjax().success!({ result: true, message: "saved" });
        h.lastAjax().error!();
        expect(h.toastr.error).toHaveBeenCalledWith("L_FailedToLoadCalendars");
        expect(modal).toHaveBeenCalledWith("hide");

        const h2 = build();
        const modal2 = spyModal(h2);
        openDialog(h2);
        h2.$("#formUpdateBrowseCalendarsOfInterest").trigger("submit");
        h2.lastAjax().success!({ result: false, error: "not shared" });
        expect(h2.toastr.error).toHaveBeenCalledWith("not shared");
        expect(modal2).not.toHaveBeenCalledWith("hide");
    });
});

// ==== the edit form, the shared-event view and the "other" popup in detail ========================================

describe("user/Calendar/UserIndex — edit form and shared-event view in detail", () => {
    const png = "iVBORw0KGgo="; // any base64 — only decoded into a Blob
    /** The "other event" popup and the read-only view modal. */
    const detailDom = `
    <div id="otherCalendarEventPopup"></div><div id="otherCalendarEventPopupTitle"></div>
    <div id="divOtherCalendarEventPopupAllDayChecked"></div><div id="divOtherCalendarEventPopupAllDayUnchecked"></div>
    <span id="otherCalendarEventPopupStartAllDayChecked"></span><span id="otherCalendarEventPopupEndAllDayChecked"></span>
    <span id="otherCalendarEventPopupStartAllDayUnchecked"></span><span id="otherCalendarEventPopupStartTimeZoneAllDayUnchecked"></span>
    <span id="otherCalendarEventPopupEndAllDayUnchecked"></span><span id="otherCalendarEventPopupEndTimeZoneAllDayUnchecked"></span>
    <a id="closeOtherCalendarEventPopup" href="#"></a>
    <input id="viewCalendarEventId" /><input id="viewCalendarEventName" /><input type="checkbox" id="viewCalendarEventAllDay" />
    <div id="divViewEventAllDayChecked"></div><div id="divViewEventAllDayUnchecked"></div>
    <input id="viewCalendarEventAllDayCheckedStartDate" /><input id="viewCalendarEventAllDayCheckedEndDate" />
    <input id="viewCalendarEventAllDayUncheckedStartDate" /><input id="viewCalendarEventAllDayUncheckedStartTime" /><input id="viewCalendarEventAllDayUncheckedStartTimeZone" />
    <input id="viewCalendarEventAllDayUncheckedEndDate" /><input id="viewCalendarEventAllDayUncheckedEndTime" /><input id="viewCalendarEventAllDayUncheckedEndTimeZone" />
    <input id="viewCalendarEventLocation" />
    <div id="divViewCalendarEventAttachedFile" style="display:none"></div><span id="spanViewCalendarEventAttachedFile"></span>
    <div id="divViewEventNotificationAllDayChecked"><div id="viewCalendarEventNotificationAllDayChecked"></div></div>
    <div id="divViewEventNotificationAllDayUnchecked"><div id="viewCalendarEventNotificationAllDayUnchecked"></div></div>
`;
    /** Loads the page with {@link detailDom}, stubbed object URLs and a modal spy. */
    const setup = () => {
        const h = build({ body: (html) => html + detailDom });
        (h.win as any).URL.createObjectURL = vi.fn(() => "blob:fake");
        (h.win as any).URL.revokeObjectURL = vi.fn();
        spyModal(h);
        return h;
    };
    /** Clicks event 55 (type "My" unless `over` says otherwise) through the calendar's `eventClick` callback. */
    const click = (h: Handle, over: Record<string, unknown>) => {
        const el = h.win.document.createElement("div");
        h.win.document.body.appendChild(el);
        const event = { id: "55", title: "T", allDay: false, extendedProps: { calendarType: "My" }, ...over };
        return h.calendarOptions[0].eventClick({ el, event });
    };
    /** A stored attachment as the event reply describes it. */
    const attachment = { name: "spec", extension: ".pdf", size: 2_500_000 };
    /** An accepted event reply for event 55, with `over` merged into the event. */
    const payload = (over: Record<string, unknown> = {}) => ({
        result: true, calendarEvent: {
            id: 55, title: "Mine", allDay: false, displayStartDate: "2024-05-01 09:05:00", displayEndDate: "2024-05-02 10:10:00",
            startDateTimeZoneIanaId: "UTC", endDateTimeZoneIanaId: "Asia/Seoul", location: "Room 2", description: "<p>d</p>",
            calendarEventAttachedFile: null, calendarId: 1, status: "Busy", serializedCalendarReminders: "[]", ...over
        }
    });
    /** A stored e-mail reminder (all offsets empty, 09:00) with `over` merged in. */
    const reminder = (over: Record<string, unknown>) => ({
        Method: "Email", MinutesBeforeEvent: null, HoursBeforeEvent: null,
        DaysBeforeEvent: null, WeeksBeforeEvent: null, TimesBeforeEvent: "09:00:00", ...over
    });

    /** Runs "other event → view": IsOtherCalendarEventExists reply, then GetOtherCalendars settles. */
    const openView = (h: Handle, calendarEvent: Record<string, unknown>) => {
        click(h, { extendedProps: { calendarType: "Other" } });
        h.$("#viewOtherCalendarEventPopup").trigger("click");
        h.lastAjax().success!(payload(calendarEvent));
        const inner = h.lastAjax();
        inner.success!({ result: true, tempOtherCalendars: [{ id: 1, name: "Shared" }] });
        inner.complete!();
    };

    /** The inline `display` style of the element matching `id` (jsdom does no layout). */
    const display = (h: Handle, id: string) => (h.$(id)[0] as HTMLElement).style.display;

    describe("shared-event view", () => {
        it("a timed shared event fills the read-only date/time/zone fields and the location", () => {
            const h = setup();
            openView(h, {});
            expect(h.$("#viewCalendarEventId").val()).toBe("55");
            expect(h.$("#viewCalendarEventName").val()).toBe("Mine");
            expect(h.$("#viewCalendarEventAllDay").prop("checked")).toBe(false);
            expect(h.$("#viewCalendarEventAllDayUncheckedStartDate").val()).toBe("2024-05-01");
            expect(h.$("#viewCalendarEventAllDayUncheckedStartTime").val()).toBe("09:05");
            expect(h.$("#viewCalendarEventAllDayUncheckedEndDate").val()).toBe("2024-05-02");
            expect(h.$("#viewCalendarEventAllDayUncheckedEndTime").val()).toBe("10:10");
            expect(h.$("#viewCalendarEventLocation").val()).toBe("Room 2");
            expect(display(h, "#divViewEventAllDayUnchecked")).not.toBe("none");
            expect(display(h, "#divViewEventAllDayChecked")).toBe("none");
        });

        it("an all-day shared event shows the all-day layout with its plain dates", () => {
            const h = setup();
            openView(h, { allDay: true, displayStartDate: "2024-05-01", displayEndDate: "2024-05-03" });
            expect(h.$("#viewCalendarEventAllDay").prop("checked")).toBe(true);
            expect(h.$("#viewCalendarEventAllDayCheckedStartDate").val()).toBe("2024-05-01");
            expect(display(h, "#divViewEventAllDayChecked")).not.toBe("none");
            expect(display(h, "#divViewEventAllDayUnchecked")).toBe("none");
        });

        it("renders the shared event's reminders — day/week rows for all-day, minute..week rows for timed", () => {
            const h = setup();
            openView(h, {
                allDay: true, displayStartDate: "2024-05-01", displayEndDate: "2024-05-03",
                serializedCalendarReminders: JSON.stringify([reminder({ DaysBeforeEvent: 5 }), reminder({ Method: "Notification", WeeksBeforeEvent: 1 })])
            });
            const checked = h.$("#divViewEventNotificationAllDayChecked > div").not("#viewCalendarEventNotificationAllDayChecked");
            expect(checked).toHaveLength(2);
            expect(checked.eq(0).find("input").val()).toBe("5");
            expect(checked.eq(1).find("input").val()).toBe("1");

            const h2 = setup();
            openView(h2, {
                serializedCalendarReminders: JSON.stringify([
                    reminder({ MinutesBeforeEvent: 10 }), reminder({
                        Method: "Notification",
                        HoursBeforeEvent: 3
                    }), reminder({ DaysBeforeEvent: 2 }), reminder({ WeeksBeforeEvent: 4 })])
            });
            const timed = h2.$("#divViewEventNotificationAllDayUnchecked > div").not("#viewCalendarEventNotificationAllDayUnchecked");
            expect(timed.map((_, r) => h2.$(r).find("input").val()).get()).toEqual(["10", "3", "2", "4"]);
        });

        it("shows the attached file and rebuilds inline images", () => {
            const h = setup();
            openView(h, {
                calendarEventAttachedFile: attachment,
                description: `<img data-file="${png}" data-contenttype="image/png" alt="">`
            });
            expect(display(h, "#divViewCalendarEventAttachedFile")).not.toBe("none");
            expect(h.$("#aViewCalendarEventAttachedFile").text()).toBe("spec.pdf");
            expect(h.$("#aViewCalendarEventAttachedFile").attr("data-name")).toBe("spec.pdf");
            expect(h.$("#aViewCalendarEventAttachedFile").attr("data-calendareventid")).toBe("55");
            expect(h.$("#aViewCalendarEventAttachedFile").attr("data-file")).toBeUndefined();
            expect(h.$("#spanViewCalendarEventAttachedFile").text()).toBe("2,441KB");
            const code = h.summernoteCalls.filter((c) => (c.el as HTMLElement | undefined)?.id === "viewCalendarEventDescription" && c.args[0] === "code").pop();
            expect(String(code!.args[1])).toContain("src=\"blob:fake\"");
        });

        it.each(["onload", "onerror"] as const)("the view's rebuilt image %s releases the object URL it was given", (event) => {
            const h = build({ body: (html) => html.replace("<div id=\"viewCalendarEventDescription\"></div>", `<div id="viewCalendarEventDescription"></div><div class="note-editor"><img src="blob:live" alt=""></div>`) + detailDom });
            (h.win as any).URL.createObjectURL = vi.fn(() => "blob:fake");
            spyModal(h);
            openView(h, { description: `<img data-file="${png}" data-contenttype="image/png" alt="">` });
            const revoke = ((h.win as any).URL.revokeObjectURL = vi.fn());
            const img = h.$(".note-editor img")[0] as HTMLImageElement;

            (img[event] as () => void)();

            expect(revoke).toHaveBeenCalledWith("blob:live");
        });

        it("a view of a shared event whose calendar list is refused still opens with the rest filled in", () => {
            const h = setup();
            click(h, { extendedProps: { calendarType: "Other" } });
            h.$("#viewOtherCalendarEventPopup").trigger("click");
            h.lastAjax().success!(payload());
            const inner = h.lastAjax();
            inner.success!({ result: false });
            inner.complete!();
            expect(h.$("#viewCalendarEventMyCalendar option")).toHaveLength(0);
            expect(h.$("#viewCalendarEventStatus").val()).toBe("Busy");
        });

        it("the view leaves an inline image with no content type as stored", () => {
            const h = setup();
            openView(h, { description: `<img data-file="${png}" alt="">` });
            const code = h.summernoteCalls.filter((c) => (c.el as HTMLElement | undefined)?.id === "viewCalendarEventDescription" && c.args[0] === "code").pop();
            expect(String(code!.args[1])).toContain("data-file");
        });

        it("shows a shared event's reminders with their own method selected (email and notification alike)", () => {
            const h = setup();
            openView(h, {
                allDay: true, displayStartDate: "2024-05-01", displayEndDate: "2024-05-03",
                serializedCalendarReminders: JSON.stringify([reminder({ Method: "Email", DaysBeforeEvent: 1 }), reminder({
                    Method: "Notification",
                    DaysBeforeEvent: 2
                })])
            });
            const rows = h.$("#divViewEventNotificationAllDayChecked > div").not("#viewCalendarEventNotificationAllDayChecked");
            expect(rows.map((_, r) => h.$(r).find("select").first().val()).get()).toEqual(["Email", "Notification"]);
        });

        it("a refused lookup toasts the server's message and opens nothing", () => {
            const h = setup();
            click(h, { extendedProps: { calendarType: "Other" } });
            h.$("#viewOtherCalendarEventPopup").trigger("click");
            h.lastAjax().success!({ result: false, error: "no longer shared" });
            expect(h.toastr.error).toHaveBeenCalledWith("no longer shared");
        });
    });

    describe("'other' calendar event popup", () => {
        it("a timed event shows its local start/end with both zones", () => {
            const h = setup();
            click(h, {
                extendedProps: {
                    calendarType: "Other",
                    displayStartDate: "2024-05-01 09:05:00",
                    displayEndDate: "2024-05-01 10:10:00",
                    displayStartDateTimeZone: "UTC",
                    displayEndDateTimeZone: "KST"
                }
            });
            expect(h.$("#otherCalendarEventPopupStartAllDayUnchecked").text()).toBe("2024-05-01 09:05:00");
            expect(h.$("#otherCalendarEventPopupStartTimeZoneAllDayUnchecked").text()).toBe("(UTC)");
            expect(h.$("#otherCalendarEventPopupEndTimeZoneAllDayUnchecked").text()).toBe("(KST)");
            expect(display(h, "#divOtherCalendarEventPopupAllDayUnchecked")).not.toBe("none");
            expect(display(h, "#divOtherCalendarEventPopupAllDayChecked")).toBe("none");
            expect(display(h, "#otherCalendarEventPopup")).toBe("block");
        });

        it("a click on the open popup itself, or on a calendar event, does not close it", () => {
            const h = setup();
            click(h, { extendedProps: { calendarType: "Other" } });
            h.$("#otherCalendarEventPopup").trigger("click");
            const other = h.win.document.createElement("div");
            other.className = "fc-event";
            h.win.document.body.appendChild(other);
            h.$(other).trigger("click");
            expect(display(h, "#otherCalendarEventPopup")).toBe("block");
        });

        it("an event of neither calendar type opens no popup", () => {
            const h = setup();
            const before = h.ajaxCalls.length;
            const result = click(h, { extendedProps: { calendarType: "Something" } });
            expect(result).toBeUndefined();
            expect(display(h, "#otherCalendarEventPopup")).not.toBe("block");
            expect(display(h, "#calendarEventPopup")).not.toBe("block");
            expect(h.ajaxCalls).toHaveLength(before);
        });

        it("the close button, a click elsewhere and a second click on the same event all hide it", () => {
            const h = setup();
            click(h, { extendedProps: { calendarType: "Other" } });
            h.$("#closeOtherCalendarEventPopup").trigger("click");
            expect(display(h, "#otherCalendarEventPopup")).toBe("none");

            click(h, { extendedProps: { calendarType: "Other" } });
            h.$(h.win.document.body).trigger("click");
            expect(display(h, "#otherCalendarEventPopup")).toBe("none");

            (h.$("#otherCalendarEventPopup")[0] as any).getClientRects = () => [1]; // jsdom has no layout
            const el = h.win.document.createElement("div");
            h.win.document.body.appendChild(el);
            const event = { id: "o1", title: "T", allDay: false, extendedProps: { calendarType: "Other" } };
            h.calendarOptions[0].eventClick({ el, event });
            expect(h.calendarOptions[0].eventClick({ el, event })).toBeUndefined();
            expect(display(h, "#otherCalendarEventPopup")).toBe("none");
        });
    });

});

describeMissingServerConstants("user", "Calendar", "UserIndex", () => fixture());

import { describe, it, expect, vi } from "vitest";
import { describeMissingServerConstants } from "@tests/_common/missingConfigSuite";
import { loadSite, antiForgery, hidden, stubPlugin, eventProps, lastOf, successOf, completeOf, instanceOfType, calendarEventClick, firstCalendar, present } from "@tests/_common/harness";

// wwwroot/anonymous/custom/Calendar/AnonymousIndex/js/site.js  (read-only calendar view)
function fixture(): string {
    return (
        antiForgery +
        hidden("maxMinutesBeforeEvent", "40320") + hidden("maxHoursBeforeEvent", "672") +
        hidden("maxDaysBeforeEvent", "28") + hidden("maxWeeksBeforeEvent", "4") +
        hidden("reminderTimeIntervals", "[&quot;09:00&quot;,&quot;18:30&quot;]") + hidden("defaultReminderTimeOfDay", "09:00") +
        hidden("otherCalendarEventOutputViewModels", "[]") +
        ["localizerIETFLanguageTag", "localizerToday", "localizerMonth", "localizerEmail", "localizerNotification",
            "localizerFailedToLoadCalendars"]
            .map((id) => hidden(id, "x")).join("") +
        `<div id="calendar"></div><div id="viewCalendarEventTaskTabs"></div>
     <div id="otherCalendarEventPopup"></div>
     <div id="otherCalendarEventPopupTitle"></div>
     <div id="divOtherCalendarEventPopupAllDayChecked"></div><div id="divOtherCalendarEventPopupAllDayUnchecked"></div>
     <span id="otherCalendarEventPopupStartAllDayChecked"></span><span id="otherCalendarEventPopupEndAllDayChecked"></span>
     <span id="otherCalendarEventPopupStartAllDayUnchecked"></span><span id="otherCalendarEventPopupStartTimeZoneAllDayUnchecked"></span>
     <span id="otherCalendarEventPopupEndAllDayUnchecked"></span><span id="otherCalendarEventPopupEndTimeZoneAllDayUnchecked"></span>
     <a id="viewOtherCalendarEventPopup" href="#"></a><a id="closeOtherCalendarEventPopup" href="#"></a>
     <input id="viewCalendarEventId" /><input id="viewCalendarEventName" /><input type="checkbox" id="viewCalendarEventAllDay" />
     <div id="divViewEventAllDayChecked"></div><div id="divViewEventAllDayUnchecked"></div>
     <input id="viewCalendarEventAllDayCheckedStartDate" /><input id="viewCalendarEventAllDayCheckedEndDate" />
     <input id="viewCalendarEventAllDayUncheckedStartDate" /><input id="viewCalendarEventAllDayUncheckedStartTime" /><input id="viewCalendarEventAllDayUncheckedStartTimeZone" />
     <input id="viewCalendarEventAllDayUncheckedEndDate" /><input id="viewCalendarEventAllDayUncheckedEndTime" /><input id="viewCalendarEventAllDayUncheckedEndTimeZone" />
     <input id="viewCalendarEventLocation" />
     <div id="viewCalendarEventDescription"></div><div class="note-editor"><img src="blob:live" alt=""></div>
     <div id="divViewCalendarEventAttachedFile"></div>
     <span id="spanViewCalendarEventAttachedFile"></span>
     <select id="viewCalendarEventMyCalendar"></select>
     <input id="viewCalendarEventStatus" />
     <div id="divViewEventNotificationAllDayChecked"></div><div id="divViewEventNotificationAllDayUnchecked"></div>
     <div id="viewCalendarEventNotificationAllDayChecked"></div><div id="viewCalendarEventNotificationAllDayUnchecked"></div>
     <div id="viewCalendarEventTaskDialogModal"></div>
     <div id="otherCalendars"></div>
     <a id="aViewCalendarEventAttachedFile"></a>`
    );
}

/** Minimal payload accepted by IsOtherCalendarEventExists's success handler. */
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

describe("Calendar/AnonymousIndex", () => {
    it("loads and renders the (stubbed) FullCalendar without throwing", () => {
        expect(() => loadSite("anonymous", "Calendar", "AnonymousIndex", fixture())).not.toThrow();
    });

    it("re-fetches events when an other-calendar checkbox changes", () => {
        const h = loadSite("anonymous", "Calendar", "AnonymousIndex", fixture());
        h.win.document.body.insertAdjacentHTML("beforeend", "<input type=\"checkbox\" class=\"chkOtherCalendar\" />");
        h.$(".chkOtherCalendar").trigger("change");
        expect(h.ajaxCalls.some((c) => c.url === "/Calendar/GetCalendarEvents")).toBe(true);
    });

    /**
     * Drives the "click an Other event -> click 'view' in its popup" flow via the captured
     * FullCalendar `eventClick` callback (never actually invoked by the stubbed FullCalendar
     * itself), since that's the only way to reach the GetOtherCalendars fetch this fix touched.
     */
    function openViewPopup(h: ReturnType<typeof loadSite>): void {
        const eventEl = h.win.document.createElement("div");
        h.win.document.body.appendChild(eventEl);
        calendarEventClick(h, {
            el: eventEl,
            event: {
                id: "other-1",
                title: "Team sync",
                allDay: true,
                extendedProps: eventProps({ calendarType: "Other", displayStartDate: "2024-01-01", displayEndDate: "2024-01-02" }),
            },
        });
        h.$("#viewOtherCalendarEventPopup").trigger("click");
    }

    // Regression: GetOtherCalendars used to set `async: false`, freezing the page for the round
    // trip. It must run asynchronous, with the read-only modal built from `complete` once settled.
    it("'view' fetches the calendar list asynchronously and still builds the modal once it settles", () => {
        const h = loadSite("anonymous", "Calendar", "AnonymousIndex", fixture());
        openViewPopup(h);

        const outerCall = h.lastAjax();
        expect(outerCall.url).toContain("/Calendar/IsOtherCalendarEventExists");
        outerCall.success?.(makeCalendarEventPayload());

        const innerCall = h.lastAjax();
        expect(innerCall.url).toBe("/Calendar/GetOtherCalendars");
        expect(innerCall.async).not.toBe(false);

        innerCall.success?.({ result: true, tempOtherCalendars: [{ id: 5, name: "Shared", htmlColorCode: "#123456" }] });
        innerCall.complete?.();

        // Only set inside showViewCalendarEventModal, which now runs from `complete`.
        expect(h.$("#viewCalendarEventStatus").val()).toBe("Confirmed");
        expect(h.$("#viewCalendarEventMyCalendar option").length).toBe(1);
    });

    // Regression: a fetch failure used to have no `error` handler; the read-only modal must still
    // be built (degraded — empty calendar select) instead of silently never opening.
    it("'view' shows an error toast and still builds the modal when GetOtherCalendars fails", () => {
        const h = loadSite("anonymous", "Calendar", "AnonymousIndex", fixture());
        openViewPopup(h);

        h.lastAjax().success?.(makeCalendarEventPayload());

        const innerCall = h.lastAjax();
        innerCall.error?.();
        innerCall.complete?.();

        expect(h.toastr.error).toHaveBeenCalledWith("x");
        expect(h.$("#viewCalendarEventStatus").val()).toBe("Confirmed");
    });
});

// ---- events, popup and the read-only modal ---------------------------------------------------------

/** A one-hour server-rendered "Other" event in `calendarId`. */
const seededEvent = (id: number, calendarId = 5) => ({
    Id: id, Title: `Event ${id}`, AllDay: false, StartDate: "2024-05-01T10:00:00", EndDate: "2024-05-01T11:00:00", HtmlColorCode: "#ff0000",
    CalendarId: calendarId, DisplayStartDate: "2024-05-01 10:00:00", DisplayEndDate: "2024-05-01 11:00:00",
    DisplayStartDateTimeZone: "UTC", DisplayEndDateTimeZone: "UTC", CalendarType: "Other",
});

/** The page fixture with `events` as the server-rendered event data, plus `extra`. */
const fixtureWith = (events: unknown[] = [], extra = "") =>
    fixture().replace(hidden("otherCalendarEventOutputViewModels", "[]"),
        hidden("otherCalendarEventOutputViewModels", JSON.stringify(events).replace(/"/g, "&quot;"))) + extra;

/** Loads the page over {@link fixtureWith}. */
const load = (events: unknown[] = [], extra = "") => loadSite("anonymous", "Calendar", "AnonymousIndex", fixtureWith(events, extra));

/** A FullCalendar event object of type "Other", as `eventClick` receives it, with `o` merged in. */
const otherEvent = (o: Record<string, unknown> = {}) => ({
    id: "e1", title: "Sync", allDay: false,
    extendedProps: eventProps({
        calendarType: "Other",
        displayStartDate: "2024-05-01 10:00:00",
        displayEndDate: "2024-05-01 11:00:00",
        displayStartDateTimeZone: "UTC",
        displayEndDateTimeZone: "KST"
    }),
    ...o,
});

describe("Calendar/AnonymousIndex — seeded and refreshed events", () => {
    it("seeds the calendar with every server-rendered event, typed as 'Other' and coloured", () => {
        const h = load([seededEvent(1), seededEvent(2)]);
        const cal = firstCalendar(h);
        expect(cal.addEvent).toHaveBeenCalledTimes(2);
        expect(present(cal.addEvent.mock.calls[0])[0]).toMatchObject({
            id: "1", title: "Event 1", allDay: false, backgroundColor: "#ff0000", borderColor: "#ff0000",
            extendedProps: { calendarId: 5, calendarType: "Other", displayStartDate: "2024-05-01 10:00:00" },
        });
        expect(cal.render).toHaveBeenCalled();
    });

    /** Shared-calendar checkboxes 7 (checked), 8 and 9 (checked). */
    const checkboxes = `<div id="otherCalendars">
    <label id="lblOtherCalendar7"><input type="checkbox" class="chkOtherCalendar" checked /></label>
    <label id="lblOtherCalendar8"><input type="checkbox" class="chkOtherCalendar" /></label>
    <label id="lblOtherCalendar9"><input type="checkbox" class="chkOtherCalendar" checked /></label></div>`;

    /** Loads the page with {@link checkboxes} in place of the empty list. */
    const withCheckboxes = () => loadSite("anonymous", "Calendar", "AnonymousIndex",
        fixtureWith([], "").replace("<div id=\"otherCalendars\"></div>", checkboxes));

    it("toggling a calendar clears the grid and asks for the events of exactly the checked calendars", () => {
        const h = withCheckboxes();
        h.$(".chkOtherCalendar").first().trigger("change");
        expect(firstCalendar(h).removeAllEvents).toHaveBeenCalledTimes(1);
        const call = h.lastAjax();
        expect(call.url).toBe("/Calendar/GetCalendarEvents");
        expect(JSON.parse(String(call.data))).toEqual({ Calendars: [{ Id: 7 }, { Id: 9 }] });
        expect(call.headers).toEqual({ RequestVerificationToken: "tok" });
    });

    it("the returned events are added with their own calendar type; a refused reply adds nothing", () => {
        const h = withCheckboxes();
        const cal = firstCalendar(h);
        h.$(".chkOtherCalendar").first().trigger("change");
        h.respond(0, { result: true, calendarEvents: JSON.stringify([seededEvent(3), seededEvent(4)]) });
        expect(cal.addEvent).toHaveBeenCalledTimes(2);
        expect(lastOf(cal.addEvent.mock.calls)[0].extendedProps["calendarType"]).toBe("Other");

        cal.addEvent.mockClear();
        h.$(".chkOtherCalendar").first().trigger("change");
        h.respond(0, { result: false });
        expect(cal.addEvent).not.toHaveBeenCalled();
    });

    it("a stale reply that lands after a newer toggle is ignored (it must not resurrect unchecked calendars)", () => {
        const h = withCheckboxes();
        const cal = firstCalendar(h);
        h.$(".chkOtherCalendar").first().trigger("change"); // request 1
        h.$(".chkOtherCalendar").last().trigger("change");  // request 2
        const [first, second] = h.ajaxCalls.filter((c) => c.url === "/Calendar/GetCalendarEvents");

        successOf(second)({ result: true, calendarEvents: JSON.stringify([seededEvent(20)]) });
        successOf(first)({ result: true, calendarEvents: JSON.stringify([seededEvent(10)]) });

        const ids = cal.addEvent.mock.calls.map((c) => c[0]["id"]);
        expect(ids).toEqual(["20"]);
    });
});

describe("Calendar/AnonymousIndex — the 'other event' popup", () => {
    /** Clicks `event` through the calendar's `eventClick` callback, anchored on `el` (a new element when omitted). */
    const click = (h: ReturnType<typeof load>, event = otherEvent(), el?: HTMLElement) => {
        const anchor = el ?? h.win.document.createElement("div");
        if (!el) h.win.document.body.appendChild(anchor);
        return calendarEventClick(h, { el: anchor, event });
    };
    /** The inline `display` style of the element matching `id` (jsdom does no layout). */
    const display = (h: ReturnType<typeof load>, id: string) => (instanceOfType(h.$(id)[0], HTMLElement)).style.display;

    it("only 'Other' events open the popup", () => {
        const h = load();
        expect(click(h, otherEvent({ extendedProps: eventProps({ calendarType: "My" }) }))).toBeUndefined();
        expect(h.$("#otherCalendarEventPopupTitle").text()).toBe("");
    });

    it("a timed event fills the timed layout (with both time zones) and hides the all-day layout", () => {
        const h = load();
        expect(click(h)).toBe(false);
        expect(h.$("#otherCalendarEventPopupTitle").text()).toBe("Sync");
        expect(h.$("#otherCalendarEventPopupStartAllDayUnchecked").text()).toBe("2024-05-01 10:00:00");
        expect(h.$("#otherCalendarEventPopupStartTimeZoneAllDayUnchecked").text()).toBe("(UTC)");
        expect(h.$("#otherCalendarEventPopupEndTimeZoneAllDayUnchecked").text()).toBe("(KST)");
        expect(display(h, "#divOtherCalendarEventPopupAllDayUnchecked")).not.toBe("none");
        expect(display(h, "#divOtherCalendarEventPopupAllDayChecked")).toBe("none");
        expect(display(h, "#otherCalendarEventPopup")).toBe("block");
        expect(h.$("#otherCalendarEventPopup").data("event-id")).toBe("e1");
    });

    it("an all-day event fills the all-day layout instead", () => {
        const h = load();
        click(h, otherEvent({ allDay: true, extendedProps: eventProps({ calendarType: "Other", displayStartDate: "2024-05-01", displayEndDate: "2024-05-02" }) }));
        expect(h.$("#otherCalendarEventPopupStartAllDayChecked").text()).toBe("2024-05-01");
        expect(h.$("#otherCalendarEventPopupEndAllDayChecked").text()).toBe("2024-05-02");
        expect(display(h, "#divOtherCalendarEventPopupAllDayChecked")).not.toBe("none");
        expect(display(h, "#divOtherCalendarEventPopupAllDayUnchecked")).toBe("none");
    });

    it("clicking the same event again closes the popup; the close button and a click elsewhere close it too", () => {
        const h = load();
        const popup = instanceOfType(h.$("#otherCalendarEventPopup")[0], HTMLElement);
        Object.assign(popup, { getClientRects: () => [1] }); // jsdom has no layout: make it count as visible
        const anchor = h.win.document.createElement("div");
        h.win.document.body.appendChild(anchor);

        click(h, otherEvent(), anchor);
        expect(display(h, "#otherCalendarEventPopup")).toBe("block");
        click(h, otherEvent(), anchor); // same id while visible → toggles closed
        expect(display(h, "#otherCalendarEventPopup")).toBe("none");

        click(h, otherEvent({ id: "e2" }), anchor);
        h.$("#closeOtherCalendarEventPopup").trigger("click");
        expect(display(h, "#otherCalendarEventPopup")).toBe("none");

        click(h, otherEvent({ id: "e3" }), anchor);
        h.$(h.win.document.body).trigger("click"); // outside the popup and any .fc-event
        expect(display(h, "#otherCalendarEventPopup")).toBe("none");
    });

    it("a click on the popup itself or on a calendar event does not close it", () => {
        const h = load();
        const anchor = h.win.document.createElement("div");
        h.win.document.body.appendChild(anchor);
        h.$("#otherCalendarEventPopup").append(h.$("#otherCalendarEventPopupTitle")); // real markup nests the title in the popup
        click(h, otherEvent(), anchor);
        h.$("#otherCalendarEventPopupTitle").trigger("click");
        expect(display(h, "#otherCalendarEventPopup")).toBe("block");
        const fcEvent = h.win.document.createElement("div");
        fcEvent.className = "fc-event";
        h.win.document.body.appendChild(fcEvent);
        h.$(fcEvent).trigger("click");
        expect(display(h, "#otherCalendarEventPopup")).toBe("block");
    });
});

describe("Calendar/AnonymousIndex — the read-only view modal", () => {
    /** Clicks "Other" event 77, then the popup's View button. */
    const open = (h: ReturnType<typeof load>) => {
        const el = h.win.document.createElement("div");
        h.win.document.body.appendChild(el);
        calendarEventClick(h, { el, event: otherEvent({ id: "77" }) });
        h.$("#viewOtherCalendarEventPopup").trigger("click");
    };
    /** An accepted event reply for a timed UTC → Asia/Seoul event, with `overrides` merged in. */
    const reply = (overrides: Record<string, unknown> = {}) => makeCalendarEventPayload({
        allDay: false, displayStartDate: "2024-05-01 10:30:00", displayEndDate: "2024-05-01 11:45:00",
        startDateTimeZoneIanaId: "UTC", endDateTimeZoneIanaId: "Asia/Seoul", location: "Room 1", ...overrides,
    });
    /** Settles the follow-up calendar-list request (success, then complete). */
    const finish = (h: ReturnType<typeof load>) => {
        const inner = h.lastAjax();
        inner.success?.({ result: true, tempOtherCalendars: [{ id: 5, name: "Shared", htmlColorCode: "#123456" }] });
        inner.complete?.();
    };

    it("the popup's View button asks for that event by id and closes the popup", () => {
        const h = load();
        open(h);
        expect(h.lastAjax().url).toBe("/Calendar/IsOtherCalendarEventExists?id=77");
        expect((instanceOfType(h.$("#otherCalendarEventPopup")[0], HTMLElement)).style.display).toBe("none");
    });

    it("a refused calendar list leaves the view's calendar select empty but the modal is still built", () => {
        const h = load();
        open(h);
        successOf(h.lastAjax())(reply());
        const inner = h.lastAjax();
        successOf(inner)({ result: false });
        completeOf(inner)();
        expect(h.$("#viewCalendarEventMyCalendar option")).toHaveLength(0);
        expect(h.$("#viewCalendarEventStatus").val()).toBe("Confirmed");
    });

    it("an inline image with no content type is left as stored", () => {
        const h = load();
        open(h);
        successOf(h.lastAjax())(reply({ description: `<img data-file="${btoa("abc")}" alt="">` }));
        const setCode = h.summernoteCalls.find((c) => c.args[0] === "code");
        expect(String(setCode?.args[1])).toContain("data-file");
    });

    it("an event that can no longer be found is toasted and no modal is built", () => {
        const h = load();
        open(h);
        successOf(h.lastAjax())({ result: false, error: "gone" });
        expect(h.toastr.error).toHaveBeenCalledWith("gone");
        expect(h.ajaxCalls).toHaveLength(1);
    });

    it("a timed event fills the date, HH:mm time and zone fields for both ends", () => {
        const h = load();
        open(h);
        successOf(h.lastAjax())(reply());
        expect(h.$("#viewCalendarEventAllDayUncheckedStartDate").val()).toBe("2024-05-01");
        expect(h.$("#viewCalendarEventAllDayUncheckedStartTime").val()).toBe("10:30");
        expect(h.$("#viewCalendarEventAllDayUncheckedEndTime").val()).toBe("11:45");
        expect(h.$("#viewCalendarEventAllDayUncheckedEndTimeZone").val()).toBe("Asia/Seoul");
        expect(h.$("#viewCalendarEventLocation").val()).toBe("Room 1");
        expect((instanceOfType(h.$("#divViewEventAllDayUnchecked")[0], HTMLElement)).style.display).not.toBe("none");
    });

    it("an all-day event fills the all-day fields", () => {
        const h = load();
        open(h);
        successOf(h.lastAjax())(reply({ allDay: true, displayStartDate: "2024-05-01", displayEndDate: "2024-05-03" }));
        expect(h.$("#viewCalendarEventAllDayCheckedStartDate").val()).toBe("2024-05-01");
        expect(h.$("#viewCalendarEventAllDayCheckedEndDate").val()).toBe("2024-05-03");
        expect((instanceOfType(h.$("#divViewEventAllDayChecked")[0], HTMLElement)).style.display).not.toBe("none");
    });

    it("the description's inline images are rehydrated into the disabled rich-text viewer", () => {
        const h = load();
        open(h);
        successOf(h.lastAjax())(reply({ description: `<p>hi</p><img data-file="${btoa("abc")}" data-contenttype="image/png" alt="">` }));
        const setCode = h.summernoteCalls.find((c) => c.args[0] === "code");
        expect(String(setCode?.args[1])).toContain("blob:");
        expect(String(setCode?.args[1])).not.toContain("data-file");
        expect(h.summernoteCalls.some((c) => c.args[0] === "disable")).toBe(true);
    });

    it.each(["onload", "onerror"] as const)("a rebuilt description image's %s releases the object URL it was given", (event) => {
        const h = load(); // (the fixture's editor container already holds an image, as summernote's own DOM would after `code` is set)
        open(h);
        successOf(h.lastAjax())(reply({ description: `<img data-file="${btoa("abc")}" data-contenttype="image/png" alt="">` }));
        const revoke = (h.win.URL.revokeObjectURL = vi.fn());
        const img = instanceOfType(h.$(".note-editor img")[0], HTMLImageElement);

        img.dispatchEvent(new Event(event.slice(2))); // fires the onload / onerror handler

        expect(revoke).toHaveBeenCalledWith("blob:live");
    });

    it("an attachment stored without an extension shows its bare name", () => {
        const h = load();
        open(h);
        successOf(h.lastAjax())(reply({ id: 77, calendarEventAttachedFile: { name: "report", extension: null, size: 10 } }));
        expect(h.$("#aViewCalendarEventAttachedFile").text()).toBe("report");
        expect(h.$("#aViewCalendarEventAttachedFile").attr("data-name")).toBe("report");
    });

    it("an attachment shows its name and a rounded KB size with thousands separators, and its link names the event — not the file's bytes", () => {
        const h = load();
        open(h);
        successOf(h.lastAjax())(reply({ id: 77, calendarEventAttachedFile: { name: "report", extension: ".zip", size: 2_048_000 } }));
        expect(h.$("#aViewCalendarEventAttachedFile").text()).toBe("report.zip");
        expect(h.$("#aViewCalendarEventAttachedFile").attr("data-name")).toBe("report.zip");
        expect(h.$("#aViewCalendarEventAttachedFile").attr("data-calendareventid")).toBe("77");
        expect(h.$("#aViewCalendarEventAttachedFile").attr("data-file")).toBeUndefined();
        expect(h.$("#spanViewCalendarEventAttachedFile").text()).toBe("2,000KB");
        expect((instanceOfType(h.$("#divViewCalendarEventAttachedFile")[0], HTMLElement)).style.display).not.toBe("none");
    });

    /** The page with an attachment link for event 77 named "report.zip". */
    const withAttachment = () => {
        const h = load();
        h.$("#aViewCalendarEventAttachedFile").attr({ "data-calendareventid": "77", "data-name": "report.zip" });
        return h;
    };

    it("clicking the attachment link asks the download endpoint for the event's attachment as a blob", () => {
        const h = withAttachment();
        const ev = h.$.Event("click");
        h.$("#aViewCalendarEventAttachedFile").trigger(ev);

        expect(ev.isDefaultPrevented()).toBe(true);
        const call = h.lastAjax();
        expect(call.url).toBe("/Calendar/DownloadCalendarEventAttachedFile");
        expect(call.type).toBe("POST");
        expect(call.data).toEqual({ calendarEventId: "77" });
        expect(call.headers).toEqual({ RequestVerificationToken: "tok" });
        expect(call.xhrFields).toEqual({ responseType: "blob" });
    });

    it("the returned file is saved under the attachment's name, and its object URL released shortly after", () => {
        const click = vi.spyOn(window.HTMLAnchorElement.prototype, "click").mockImplementation(function() { /* jsdom cannot navigate */
        });
        try {
            const h = withAttachment();
            h.win.URL.createObjectURL = () => "blob:attachment";
            const revoke = (h.win.URL.revokeObjectURL = vi.fn());
            h.$("#aViewCalendarEventAttachedFile").trigger("click");
            vi.useFakeTimers();
            try {
                h.respond(0, new h.win.Blob(["zip-bytes"], { type: "application/zip" }));
                expect(click).toHaveBeenCalledTimes(1);
                expect((instanceOfType(click.mock.contexts[0], HTMLAnchorElement)).download).toBe("report.zip");
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

    it("a refusal is shown as the server's error and nothing is saved; a link without an event id does nothing", async () => {
        const click = vi.spyOn(window.HTMLAnchorElement.prototype, "click").mockImplementation(function() { /* noop */
        });
        try {
            const h = withAttachment();
            h.$("#aViewCalendarEventAttachedFile").trigger("click");
            h.respondOverHttp(0, JSON.stringify({ result: false, error: "The calendar event could not be found." }), "application/json; charset=utf-8");
            await vi.waitFor(() => expect(h.toastr.error).toHaveBeenCalledWith("The calendar event could not be found."));
            expect(click).not.toHaveBeenCalled();

            const without = load();
            const before = without.ajaxCalls.length;
            without.$("#aViewCalendarEventAttachedFile").trigger("click");
            expect(without.ajaxCalls).toHaveLength(before);
        } finally {
            click.mockRestore();
        }
    });

    it("the calendar select is filled from the visible calendars and set to the event's own calendar", () => {
        const h = load();
        open(h);
        successOf(h.lastAjax())(reply({ calendarId: 6 }));
        const inner = h.lastAjax();
        successOf(inner)({ result: true, tempOtherCalendars: [{ id: 5, name: "A", htmlColorCode: "#123456" }, { id: 6, name: "B", htmlColorCode: "#123456" }] });
        expect(h.$("#viewCalendarEventMyCalendar option").map((_, o) => o.textContent).get()).toEqual(["A", "B"]);
        expect(h.$("#viewCalendarEventMyCalendar").val()).toBe("6");
    });

    it("the modal is opened static (no Esc) once the calendar list settles", () => {
        const h = load();
        const modal = vi.fn(function(this: JQuery, _command?: unknown) {
            return this;
        });
        open(h);
        stubPlugin(h, "modal", modal);
        successOf(h.lastAjax())(reply());
        finish(h);
        expect(modal).toHaveBeenCalledWith({ keyboard: false, backdrop: "static" });
        expect(modal).toHaveBeenCalledWith("show");
    });

    it("timed reminders are shown read-only with the right unit selected; Email reminders are never shown to a visitor", () => {
        const h = load();
        open(h);
        const reminders = [
            { Method: "Email", MinutesBeforeEvent: 5, HoursBeforeEvent: null, DaysBeforeEvent: null, WeeksBeforeEvent: null, TimesBeforeEvent: null },
            { Method: "Notification", MinutesBeforeEvent: null, HoursBeforeEvent: 2, DaysBeforeEvent: null, WeeksBeforeEvent: null, TimesBeforeEvent: null },
            { Method: "Notification", MinutesBeforeEvent: null, HoursBeforeEvent: null, DaysBeforeEvent: 3, WeeksBeforeEvent: null, TimesBeforeEvent: null },
            { Method: "Notification", MinutesBeforeEvent: null, HoursBeforeEvent: null, DaysBeforeEvent: null, WeeksBeforeEvent: 1, TimesBeforeEvent: null },
            { Method: "Notification", MinutesBeforeEvent: 15, HoursBeforeEvent: null, DaysBeforeEvent: null, WeeksBeforeEvent: null, TimesBeforeEvent: null },
        ];
        successOf(h.lastAjax())(reply({ serializedCalendarReminders: JSON.stringify(reminders) }));
        finish(h);

        const rows = h.$(".divViewEventNotificationAllDayUncheckedRow");
        expect(rows).toHaveLength(4); // the Email one is skipped
        const units = rows.map((_, r) => h.$(r).find(".viewCalendarEventSelNotificationTimeTypeAllDayUnchecked option[selected]").val()).get();
        expect(units).toEqual(["Hours", "Days", "Weeks", "Minutes"]);
        expect(rows.find("input").map((_, i) => (instanceOfType(i, HTMLInputElement)).value).get()).toEqual(["2", "3", "1", "15"]);
        expect(rows.find("input").map((_, i) => (instanceOfType(i, HTMLInputElement)).max).get()).toEqual(["672", "28", "4", "40320"]);
        expect(rows.find("select, input").toArray().every((e) => e.matches(":disabled"))).toBe(true);
    });

    it("all-day reminders show days/weeks lead time and the chosen HH:mm; Email ones are skipped", () => {
        const h = loadSite("anonymous", "Calendar", "AnonymousIndex",
            fixtureWith().replace(hidden("reminderTimeIntervals", "[&quot;09:00&quot;,&quot;18:30&quot;]"), hidden("reminderTimeIntervals", "[&quot;08:00&quot;,&quot;09:30&quot;]")));
        open(h);
        const reminders = [
            { Method: "Email", MinutesBeforeEvent: null, HoursBeforeEvent: null, DaysBeforeEvent: 1, WeeksBeforeEvent: null, TimesBeforeEvent: "08:00:00" },
            { Method: "Notification", MinutesBeforeEvent: null, HoursBeforeEvent: null, DaysBeforeEvent: 2, WeeksBeforeEvent: null, TimesBeforeEvent: "09:30:00" },
            { Method: "Notification", MinutesBeforeEvent: null, HoursBeforeEvent: null, DaysBeforeEvent: null, WeeksBeforeEvent: 3, TimesBeforeEvent: "08:00:00" },
        ];
        successOf(h.lastAjax())(reply({
            allDay: true,
            displayStartDate: "2024-05-01",
            displayEndDate: "2024-05-02",
            serializedCalendarReminders: JSON.stringify(reminders)
        }));
        finish(h);

        const rows = h.$(".divViewEventNotificationAllDayCheckedRow");
        expect(rows).toHaveLength(2);
        expect(rows.map((_, r) => h.$(r).find(".viewCalendarEventSelNotificationTimeAllDayChecked option[selected]").val()).get()).toEqual(["09:30", "08:00"]);
        expect(rows.map((_, r) => h.$(r).find(".viewCalendarEventSelNotificationTimeTypeAllDayChecked option[selected]").val()).get()).toEqual(["Days", "Weeks"]);
        expect(rows.find("input").map((_, i) => (instanceOfType(i, HTMLInputElement)).max).get()).toEqual(["28", "4"]);
    });
});

describeMissingServerConstants("anonymous", "Calendar", "AnonymousIndex", () => fixture());

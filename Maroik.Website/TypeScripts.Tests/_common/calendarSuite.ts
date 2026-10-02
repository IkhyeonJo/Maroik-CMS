/*
 * Shared behavior suite for the calendar page scripts (user/Calendar/UserIndex and admin/Calendar/AdminIndex):
 * reminder-row validation, the create-event form and the "my calendars" side list (create / edit / delete).
 * The two scripts differ mainly in the "other calendars" feature (user only) and the event-listener namespace,
 * so each site.test.ts supplies a `build()` for its own fixture and the checked-calendar ids it expects.
 */
import { describe, it, expect, vi } from "vitest";
import { hidden, type SiteHandle } from "@tests/_common/harness";

/** Short alias used by the calendar suites. */
export type Handle = SiteHandle;
/** Loads a calendar page with the given "my" / "other" event data, optionally rewriting its fixture HTML first. */
export type Build = (opts?: { my?: unknown[]; other?: unknown[]; body?: (html: string) => string }) => Handle;

/** A one-hour server-rendered event (the shape of the hidden event-data inputs) in `calendarId`. */
export const evt = (id: number, calendarId: number, type = "My") => ({
    Id: id, Title: `Event ${id}`, AllDay: false, StartDate: "2024-05-01T10:00:00", EndDate: "2024-05-01T11:00:00", HtmlColorCode: "#123456",
    CalendarId: calendarId, DisplayStartDate: "2024-05-01 10:00:00", DisplayEndDate: "2024-05-01 11:00:00",
    DisplayStartDateTimeZone: "UTC", DisplayEndDateTimeZone: "UTC", CalendarType: type,
});

/** The "my calendars" side list: ids 1 (Zeta, checked), 2 (Alpha), 3 (Mid, checked). */
export const mine = `<div id="myCalendars">
  <label id="lblCalendar1"><input type="checkbox" class="chkCalendar" checked /><label>Zeta</label><i id="1" class="edit-calendar"></i><i id="1" class="delete-calendar"></i></label>
  <label id="lblCalendar2"><input type="checkbox" class="chkCalendar" /><label>Alpha</label></label>
  <label id="lblCalendar3"><input type="checkbox" class="chkCalendar" checked /><label>Mid</label></label></div>`;

/** Modal shells and form inputs the calendar scripts cache, shared by both page fixtures. */
export const extras = `
  <div id="createCalendarEventTaskDialogModal"></div><div id="editCalendarEventTaskDialogModal"></div>
  <div id="createCalendarDialogModal"></div><div id="editCalendarDialogModal"></div><div id="confirmDeleteCalendarDialogModal"></div>
  <input id="createCalendarEventName" value="Team lunch" /><input id="createCalendarEventLocation" value="Cafe" />
  <input id="createCalendarEventStatus" value="Busy" />
  <input id="createCalendarEventAllDayUncheckedStartTime" value="10:15" /><input id="createCalendarEventAllDayUncheckedEndTime" value="11:30" />
  <input id="createCalendarEventAllDayUncheckedStartTimeZone" value="UTC" /><input id="createCalendarEventAllDayUncheckedEndTimeZone" value="Asia/Seoul" />
  <div id="calendarEventPopup"></div><div id="calendarEventPopupTitle"></div>
  <div id="divCalendarEventPopupAllDayChecked"></div><div id="divCalendarEventPopupAllDayUnchecked"></div>
  <span id="calendarEventPopupStartAllDayChecked"></span><span id="calendarEventPopupEndAllDayChecked"></span>
  <span id="calendarEventPopupStartAllDayUnchecked"></span><span id="calendarEventPopupStartTimeZoneAllDayUnchecked"></span>
  <span id="calendarEventPopupEndAllDayUnchecked"></span><span id="calendarEventPopupEndTimeZoneAllDayUnchecked"></span>
  <a id="closeCalendarEventPopup" href="#"></a><a id="deleteCalendarEventPopup" href="#"></a>
  <input id="editCalendarEventId" /><input id="editCalendarEventName" value="Edited" /><input id="editCalendarEventLocation" value="Room 2" />
  <input id="editCalendarEventStatus2" />
  <input id="editCalendarEventAllDayUncheckedStartTime" value="09:05" /><input id="editCalendarEventAllDayUncheckedEndTime" value="10:10" />
  <input id="editCalendarEventAllDayUncheckedStartTimeZone" value="UTC" /><input id="editCalendarEventAllDayUncheckedEndTimeZone" value="UTC" />
  <button id="btnDeleteCalendar"></button><input id="editCalendarId" /><input id="editCalendarName" /><input id="editCalendarDescription" /><input id="editCalendarHtmlColorCode" /><input id="editCalendarTimeZone" />`;

/** JSON for a hidden `<input>` value attribute. */
export const json = (value: unknown) => JSON.stringify(value).replace(/"/g, "&quot;");
export { hidden };

/** Replaces `$.fn.modal` with a chainable spy and returns it. */
export const spyModal = (h: Handle) => {
    const modal = vi.fn(function(this: any) {
        return this;
    });
    (h.$.fn as any).modal = modal;
    return modal;
};
/** The captured `GetCalendarEvents` requests. */
export const eventCalls = (h: Handle) => h.ajaxCalls.filter((c) => c.url === "/Calendar/GetCalendarEvents");

/** What one calendar page supplies to {@link describeCalendarCommon}. */
export interface CalendarCommon {
    /** suite-name prefix */
    label: string;
    /** loads the page */
    build: Build;
    /** calendar ids the page requests when its checked boxes are collected (mine 1 and 3, plus any "other" ones) */
    checkedIds: { Id: number }[];
}

/** Registers the shared calendar tests: reminder validation, the create-event form and the "my calendars" list. */
export function describeCalendarCommon(c: CalendarCommon): void {
    const { build, label } = c;
    describe(`${label} — reminder validation`, () => {
        const row = (kind: "create" | "edit", checked: boolean, type: string, value: string) => {
            const suffix = checked ? "AllDayChecked" : "AllDayUnchecked";
            const units = checked ? ["Days", "Weeks"] : ["Minutes", "Hours", "Days", "Weeks"];
            return `<div class="div${kind === "create" ? "Create" : "Edit"}EventNotification${suffix}Row">
      <input class="${kind}CalendarEventSelNotificationNumber${suffix}" value="${value}" />
      <select class="${kind}CalendarEventSelNotificationTimeType${suffix}">${units.map((u) => `<option value="${u}" ${u === type ? "selected" : ""}>${u}</option>`).join("")}</select>
      <div class="error-message" style="display:none"></div></div>`;
        };
        const run = (kind: "create" | "edit", checked: boolean, type: string, value: string) => {
            const h = build({ body: (html) => html + row(kind, checked, type, value) });
            const r = h.$(`.div${kind === "create" ? "Create" : "Edit"}EventNotification${checked ? "AllDayChecked" : "AllDayUnchecked"}Row`).last();
            const suffix = checked ? "AllDayChecked" : "AllDayUnchecked";
            r.find(`.${kind}CalendarEventSelNotificationNumber${suffix}`).trigger("input");
            return { input: r.find(`.${kind}CalendarEventSelNotificationNumber${suffix}`)[0] as HTMLElement, message: r.find(".error-message") };
        };

        // (max: minutes 40320, hours 672, days 28, weeks 4 — the server-published fixture values)
        it.each([
            ["create", true, "Days", "29", "L_ErrorRangeDay"], ["create", true, "Weeks", "5", "L_ErrorRangeWeek"],
            ["create", false, "Minutes", "40321", "L_ErrorRangeMinute"], ["create", false, "Hours", "673", "L_ErrorRangeHour"],
            ["create", false, "Days", "29", "L_ErrorRangeDay"], ["create", false, "Weeks", "5", "L_ErrorRangeWeek"],
            ["edit", true, "Days", "29", "L_ErrorRangeDay"], ["edit", true, "Weeks", "5", "L_ErrorRangeWeek"],
            ["edit", false, "Minutes", "40321", "L_ErrorRangeMinute"], ["edit", false, "Hours", "673", "L_ErrorRangeHour"],
            ["edit", false, "Days", "29", "L_ErrorRangeDay"], ["edit", false, "Weeks", "5", "L_ErrorRangeWeek"],
            ["create", true, "Days", "", "L_ThisFieldRequired"], ["edit", false, "Hours", "  ", "L_ThisFieldRequired"],
            ["create", false, "Days", "abc", "L_ErrorInvalidNumber"], ["edit", true, "Weeks", "1e", "L_ErrorInvalidNumber"],
            ["create", false, "Days", "", "L_ThisFieldRequired"], ["edit", false, "Days", "", "L_ThisFieldRequired"],
            ["edit", true, "Days", "", "L_ThisFieldRequired"], ["create", true, "Weeks", "  ", "L_ThisFieldRequired"],
            ["create", false, "Hours", "1e", "L_ErrorInvalidNumber"], ["edit", false, "Minutes", "abc", "L_ErrorInvalidNumber"],
            ["create", true, "Days", "x", "L_ErrorInvalidNumber"], ["edit", true, "Days", "-", "L_ErrorInvalidNumber"],
        ] as const)("%s (all-day=%s) %s reminder %j is flagged as: %s", (kind, checked, type, value, message) => {
            const { input, message: el } = run(kind, checked, type, value);
            expect(el.text()).toBe(message);
            expect(input.style.textDecoration).toBe("underline"); // the input is underlined; the text is hidden again by the live handler
        });

        it.each([
            ["create", true, "Days", "28"], ["create", false, "Minutes", "40320"], ["edit", true, "Weeks", "4"], ["edit", false, "Hours", "0"],
        ] as const)("%s (all-day=%s) %s reminder %s (the exact limit) is accepted and not underlined", (kind, checked, type, value) => {
            const { input, message } = run(kind, checked, type, value);
            expect(message.text()).toBe("");
            expect(input.style.textDecoration).toBe("none");
        });

        it("changing the unit re-validates the same number against the new unit's limit", () => {
            const h = build({ body: (html) => html + row("create", true, "Days", "10") });
            const r = h.$(".divCreateEventNotificationAllDayCheckedRow").last();
            r.find(".createCalendarEventSelNotificationTimeTypeAllDayChecked").val("Weeks").trigger("change");
            expect(r.find(".error-message").text()).toBe("L_ErrorRangeWeek"); // 10 weeks > 4
        });

        it.each([
            ["create", true, "Weeks", "L_ErrorRangeWeek"], ["create", false, "Minutes", "L_ErrorRangeMinute"],
            ["edit", true, "Weeks", "L_ErrorRangeWeek"], ["edit", false, "Minutes", "L_ErrorRangeMinute"],
        ] as const)("changing the %s (all-day=%s) unit to %s re-validates the number", (kind, checked, unit, message) => {
            const suffix = checked ? "AllDayChecked" : "AllDayUnchecked";
            const rowClass = `.div${kind === "create" ? "Create" : "Edit"}EventNotification${suffix}Row`;
            // 50000 is over every unit's limit; the starting unit (Days / Hours) is picked so the change is what flips it
            const h = build({ body: (html) => html + row(kind, checked, checked ? "Days" : "Hours", "50000") });
            const r = h.$(rowClass).last();
            r.find(`.${kind}CalendarEventSelNotificationTimeType${suffix}`).val(unit).trigger("change");
            expect(r.find(".error-message").text()).toBe(message);
        });
    });

    describe(`${label} — create-event form`, () => {
        const withRows = (rows: string) => (html: string) => html + rows;
        const timedRows = `<div class="divCreateEventNotificationAllDayUncheckedRow">
    <select class="createCalendarEventSelNotificationMethodAllDayUnchecked"><option value="Notification" selected>N</option></select>
    <input class="createCalendarEventSelNotificationNumberAllDayUnchecked" value="15" />
    <select class="createCalendarEventSelNotificationTimeTypeAllDayUnchecked"><option value="Minutes" selected>m</option></select>
    <div class="error-message" style="display:none"></div></div>
   <div class="divCreateEventNotificationAllDayUncheckedRow">
    <select class="createCalendarEventSelNotificationMethodAllDayUnchecked"><option value="Email" selected>E</option></select>
    <input class="createCalendarEventSelNotificationNumberAllDayUnchecked" value="2" />
    <select class="createCalendarEventSelNotificationTimeTypeAllDayUnchecked"><option value="Weeks" selected>w</option></select>
    <div class="error-message" style="display:none"></div></div>`;

        const setDates = (h: Handle) => {
            h.$("#createCalendarEventAllDayUncheckedStartDate").val("2024-05-01");
            h.$("#createCalendarEventAllDayUncheckedEndDate").val("2024-05-02");
            h.$("#createCalendarEventAllDayCheckedStartDate").val("2024-06-01");
            h.$("#createCalendarEventAllDayCheckedEndDate").val("2024-06-03");
            h.$("#createCalendarEventMyCalendar").html("<option value=\"4\" selected>c</option>");
        };
        const submit = (h: Handle) => {
            const ev = h.$.Event("submit");
            h.$("#formCreateCalendarEvent").trigger(ev);
            return ev;
        };
        const created = (h: Handle) => h.ajaxCalls.find((c) => c.url === "/Calendar/CreateCalendarEvent")!;

        it("a timed event posts multipart form data with joined date+time, both zones and its mapped reminders; the form never navigates", () => {
            const h = build({ body: withRows(timedRows) });
            setDates(h);
            const ev = submit(h);
            const data = created(h).data as FormData;
            expect(ev.isDefaultPrevented()).toBe(true);
            expect(created(h).headers).toEqual({ RequestVerificationToken: "tok" });
            expect(data.get("Title")).toBe("Team lunch");
            expect(data.get("AllDay")).toBe("false");
            expect(data.get("StartDate")).toBe("2024-05-01 10:15");
            expect(data.get("EndDate")).toBe("2024-05-02 11:30");
            expect(data.get("StartDateTimeZoneIanaId")).toBe("UTC");
            expect(data.get("EndDateTimeZoneIanaId")).toBe("Asia/Seoul");
            expect(data.get("Location")).toBe("Cafe");
            expect(data.get("CalendarId")).toBe("4");
            expect(data.get("Status")).toBe("Busy");
            expect(JSON.parse(String(data.get("SerializedCalendarReminders")))).toEqual([
                {
                    Method: "Notification",
                    MinutesBeforeEvent: "15",
                    HoursBeforeEvent: null,
                    DaysBeforeEvent: null,
                    WeeksBeforeEvent: null,
                    TimesBeforeEvent: null
                },
                { Method: "Email", MinutesBeforeEvent: null, HoursBeforeEvent: null, DaysBeforeEvent: null, WeeksBeforeEvent: "2", TimesBeforeEvent: null },
            ]);
        });

        it("an all-day event posts plain dates, blank zones and days/weeks reminders with their time of day", () => {
            const rows = `<div class="divCreateEventNotificationAllDayCheckedRow">
      <select class="createCalendarEventSelNotificationMethodAllDayChecked"><option value="Notification" selected>N</option></select>
      <input class="createCalendarEventSelNotificationNumberAllDayChecked" value="3" />
      <select class="createCalendarEventSelNotificationTimeTypeAllDayChecked"><option value="Days" selected>d</option></select>
      <select class="createCalendarEventSelNotificationTimeAllDayChecked"><option value="08:30" selected>t</option></select>
      <div class="error-message" style="display:none"></div></div>`;
            const h = build({ body: (html) => html.replace(/<div class="divCreateEventNotificationAllDayCheckedRow">[\s\S]*?<\/div>\s*<\/div>/, "") + rows });
            setDates(h);
            h.$("#createCalendarEventAllDay").prop("checked", true);
            submit(h);
            const data = created(h).data as FormData;
            expect(data.get("AllDay")).toBe("true");
            expect(data.get("StartDate")).toBe("2024-06-01");
            expect(data.get("EndDate")).toBe("2024-06-03");
            expect(data.get("StartDateTimeZoneIanaId")).toBe("");
            expect(JSON.parse(String(data.get("SerializedCalendarReminders")))).toEqual([
                {
                    Method: "Notification",
                    MinutesBeforeEvent: null,
                    HoursBeforeEvent: null,
                    DaysBeforeEvent: "3",
                    WeeksBeforeEvent: null,
                    TimesBeforeEvent: "08:30"
                },
            ]);
        });

        it("every reminder unit in the create form maps to its own field (minutes, hours, days, weeks — timed and all-day)", () => {
            const timedRow = (method: string, unit: string, n: string) => `<div class="divCreateEventNotificationAllDayUncheckedRow">
      <select class="createCalendarEventSelNotificationMethodAllDayUnchecked"><option value="${method}" selected>m</option></select>
      <input class="createCalendarEventSelNotificationNumberAllDayUnchecked" value="${n}" />
      <select class="createCalendarEventSelNotificationTimeTypeAllDayUnchecked"><option value="${unit}" selected>u</option></select>
      <div class="error-message" style="display:none"></div></div>`;
            const dayRow = (unit: string, n: string, time: string) => `<div class="divCreateEventNotificationAllDayCheckedRow">
      <select class="createCalendarEventSelNotificationMethodAllDayChecked"><option value="Email" selected>E</option></select>
      <input class="createCalendarEventSelNotificationNumberAllDayChecked" value="${n}" />
      <select class="createCalendarEventSelNotificationTimeTypeAllDayChecked"><option value="${unit}" selected>u</option></select>
      <select class="createCalendarEventSelNotificationTimeAllDayChecked"><option value="${time}" selected>t</option></select>
      <div class="error-message" style="display:none"></div></div>`;
            const strip = (html: string) => html.replace(/<div class="divCreateEventNotificationAllDayCheckedRow">[\s\S]*?<\/div>\s*<\/div>/, "");
            const shape = (o: Record<string, unknown>) => ({
                Method: "Email",
                MinutesBeforeEvent: null,
                HoursBeforeEvent: null,
                DaysBeforeEvent: null,
                WeeksBeforeEvent: null,
                TimesBeforeEvent: null, ...o
            });

            const timed = build({ body: (html) => html + timedRow("Email", "Hours", "2") + timedRow("Notification", "Days", "3") });
            setDates(timed);
            submit(timed);
            expect(JSON.parse(String((created(timed).data as FormData).get("SerializedCalendarReminders")))).toEqual([
                shape({ HoursBeforeEvent: "2" }), shape({ Method: "Notification", DaysBeforeEvent: "3" })]);

            const allDay = build({ body: (html) => strip(html) + dayRow("Days", "1", "07:00") + dayRow("Weeks", "2", "21:15") });
            setDates(allDay);
            allDay.$("#createCalendarEventAllDay").prop("checked", true);
            submit(allDay);
            expect(JSON.parse(String((created(allDay).data as FormData).get("SerializedCalendarReminders")))).toEqual([
                shape({ DaysBeforeEvent: "1", TimesBeforeEvent: "07:00" }), shape({ WeeksBeforeEvent: "2", TimesBeforeEvent: "21:15" })]);
        });

        it("an out-of-range reminder, or an invalid form, blocks the request", () => {
            const bad = timedRows.replace("value=\"15\"", "value=\"999999\"");
            const h = build({ body: withRows(bad) });
            setDates(h);
            submit(h);
            expect(created(h)).toBeUndefined();
            expect(h.$(".divCreateEventNotificationAllDayUncheckedRow").first().find(".error-message").text()).toBe("L_ErrorRangeMinute");

            const h2 = build({ body: withRows(timedRows) });
            (h2.$.fn as any).valid = () => false;
            submit(h2);
            expect(created(h2)).toBeUndefined();
        });

        it("success closes the modal, toasts and reloads the events of the checked calendars; a refusal only toasts", () => {
            const h = build({ body: withRows(timedRows) });
            const modal = spyModal(h);
            setDates(h);
            submit(h);
            created(h).success!({ result: true, message: "created" });
            expect(h.calendarInstances[0].removeAllEvents).toHaveBeenCalled();
            expect(JSON.parse(String(eventCalls(h)[0].data))).toEqual({ Calendars: c.checkedIds });
            eventCalls(h)[0].success!({ result: true, calendarEvents: JSON.stringify([evt(50, 1)]) });
            expect(h.calendarInstances[0].addEvent.mock.calls.at(-1)[0].id).toBe(50);
            expect(h.toastr.success).toHaveBeenCalledWith("created");
            expect(modal).toHaveBeenCalledWith("hide");
            eventCalls(h)[0].success!({ result: false, error: "no events for you" }); // the follow-up reload being refused is toasted with its own message
            expect(h.toastr.error).toHaveBeenCalledWith("no events for you");

            const h2 = build({ body: withRows(timedRows) });
            setDates(h2);
            submit(h2);
            created(h2).success!({ result: false, error: "nope" });
            expect(h2.toastr.error).toHaveBeenCalledWith("nope");
            expect(eventCalls(h2)).toHaveLength(0);

            // A server fault is answered with the temporary-error message, which is shown exactly as sent.
            const h3 = build({ body: withRows(timedRows) });
            setDates(h3);
            submit(h3);
            created(h3).success!({ result: false, error: "A temporary error occurred. Please try again later." });
            expect(h3.toastr.error).toHaveBeenCalledWith("A temporary error occurred. Please try again later.");
        });
    });

    describe(`${label} — my calendars`, () => {
        const labels = (h: Handle) => h.$("#myCalendars > label").map((_, l) => l.id).get();

        it("creating a calendar appends an escaped, coloured entry in name order and toasts", () => {
            const h = build();
            spyModal(h);
            h.$("#createCalendarName").val("Beta");
            const ev = h.$.Event("submit");
            h.$("#formCreateCalendar").trigger(ev);
            const call = h.lastAjax();
            expect(ev.isDefaultPrevented()).toBe(true);
            expect(call.url).toBe("/Calendar/CreateCalendar");
            expect(JSON.parse(String(call.data))).toEqual({ Calendars: [{ Name: "Beta", Description: "", HtmlColorCode: "#3788d8", TimeZoneIanaId: "UTC" }] });

            call.success!({ result: true, message: "made", calendar: { id: 7, name: "Beta", htmlColorCode: "#abcdef" } });

            expect(labels(h)).toEqual(["lblCalendar2", "lblCalendar7", "lblCalendar3", "lblCalendar1"]); // Alpha, Beta, Mid, Zeta
            expect(h.$("#lblCalendar7 input").css("accent-color")).toBe("rgb(171, 205, 239)"); // #abcdef, as jsdom normalizes it
            expect(h.toastr.success).toHaveBeenCalledWith("made");
        });

        it("a hostile calendar name is HTML-escaped, never interpreted", () => {
            const h = build();
            spyModal(h);
            h.$("#formCreateCalendar").trigger("submit");
            h.lastAjax().success!({
                result: true,
                message: "made",
                calendar: {
                    id: 7,
                    name: `<img src=x alt="">`,
                    htmlColorCode: "#abcdef"
                }
            });
            expect(h.$("#lblCalendar7 img")).toHaveLength(0);
            expect(h.$("#lblCalendar7").html()).toContain("&lt;img src=x alt=\"\"&gt;");
        });

        it("a refused create only toasts; an invalid form sends nothing", () => {
            const h = build();
            h.$("#formCreateCalendar").trigger("submit");
            h.lastAjax().success!({ result: false, error: "duplicate" });
            expect(h.toastr.error).toHaveBeenCalledWith("duplicate");
            expect(labels(h)).toHaveLength(3);

            const h2 = build();
            (h2.$.fn as any).valid = () => false;
            h2.$("#formCreateCalendar").trigger("submit");
            expect(h2.ajaxCalls.filter((c) => c.url === "/Calendar/CreateCalendar")).toHaveLength(0);
        });

        it("the pencil fetches the calendar and opens the static edit modal; the trash only opens the confirm modal with the id", () => {
            const h = build();
            const modal = spyModal(h);
            h.$(".edit-calendar").trigger("click");
            expect(h.lastAjax().url).toBe("/Calendar/IsCalendarExists?id=1");
            h.lastAjax().success!({ result: true, calendar: { id: 1, name: "Zeta", description: "d", htmlColorCode: "#111111", timeZoneIanaId: "UTC" } });
            expect(h.$("#editCalendarName").val()).toBe("Zeta");
            expect(modal).toHaveBeenCalledWith({ keyboard: false, backdrop: "static" });

            h.$(".delete-calendar").trigger("click");
            expect(h.$("#confirmDeleteCalendarDialogModal").data("calendar-id")).toBe("1");
            expect(h.ajaxCalls.filter((c) => c.url.includes("IsCalendarExists"))).toHaveLength(1);
        });

        it("the pencil toasts when the calendar is gone; the icons highlight on hover", () => {
            const h = build();
            h.$(".edit-calendar").trigger("click");
            h.lastAjax().success!({ result: false, error: "gone" });
            expect(h.toastr.error).toHaveBeenCalledWith("gone");
            for (const cls of [".edit-calendar", ".delete-calendar"]) {
                h.$(cls).trigger("mouseenter");
                expect((h.$(cls)[0] as HTMLElement).style.color).toBe("blue");
                h.$(cls).trigger("mouseleave");
                expect((h.$(cls)[0] as HTMLElement).style.color).toBe("");
            }
        });

        it("saving an edit updates the entry's name and colour, recolours its events, keeps the list sorted and toasts", () => {
            const h = build({ my: [evt(1, 1)] });
            vi.useFakeTimers(); // after build: loadSite manages its own fake timers while it evaluates the script
            try {
                spyModal(h);
                const setProp = vi.fn();
                h.calendarInstances[0].getEvents = () => [{ extendedProps: { calendarId: 1 }, setProp }, {
                    extendedProps: { calendarId: 2 },
                    setProp: vi.fn()
                }];
                h.$("#editCalendarId").val("1");
                h.$("#editCalendarName").val("Aaa");
                h.$("#formEditCalendar").trigger("submit");
                const call = h.lastAjax();
                expect(call.url).toBe("/Calendar/UpdateCalendar");
                expect(JSON.parse(String(call.data)).Calendars[0]).toMatchObject({ Id: "1", Name: "Aaa" });

                call.success!({ result: true, message: "saved", calendar: { id: 1, name: "Aaa", htmlColorCode: "#222222" } });
                vi.runAllTimers();

                expect(h.$("#lblCalendar1 > label").text()).toBe("Aaa");
                expect(setProp).toHaveBeenCalledWith("backgroundColor", "#222222");
                expect(setProp).toHaveBeenCalledWith("borderColor", "#222222");
                expect(h.$("#myCalendars > label").map((_, l) => l.id).get()).toEqual(["lblCalendar1", "lblCalendar2", "lblCalendar3"]); // Aaa, Alpha, Mid
                expect(h.toastr.success).toHaveBeenCalledWith("saved");
            } finally {
                vi.useRealTimers();
            }
        });

        it("a refused edit toasts; an invalid edit form sends nothing", () => {
            const h = build();
            h.$("#formEditCalendar").trigger("submit");
            h.lastAjax().success!({ result: false, error: "no" });
            expect(h.toastr.error).toHaveBeenCalledWith("no");
            const h2 = build();
            (h2.$.fn as any).valid = () => false;
            h2.$("#formEditCalendar").trigger("submit");
            expect(h2.ajaxCalls.filter((c) => c.url === "/Calendar/UpdateCalendar")).toHaveLength(0);
        });

        it("confirming a delete re-checks the calendar, deletes it, removes its entry and events, and toasts", () => {
            const h = build();
            const modal = spyModal(h);
            const remove1 = vi.fn();
            const remove2 = vi.fn();
            h.calendarInstances[0].getEvents = () => [{ extendedProps: { calendarId: 1 }, remove: remove1 }, {
                extendedProps: { calendarId: 2 },
                remove: remove2
            }];
            h.$(".delete-calendar").trigger("click");
            h.$("#btnDeleteCalendar").trigger("click");

            expect(h.lastAjax().url).toBe("/Calendar/IsCalendarExists?id=1");
            h.lastAjax().success!({ result: true, calendar: { id: 1 } });
            const del = h.lastAjax();
            expect(del.url).toBe("/Calendar/DeleteCalendar");
            expect(JSON.parse(String(del.data))).toEqual({ Calendars: [{ Id: 1 }] });

            del.success!({ result: true, message: "deleted", calendar: { id: 1 } });
            expect(h.$("#lblCalendar1")).toHaveLength(0);
            expect(remove1).toHaveBeenCalled();
            expect(remove2).not.toHaveBeenCalled();
            expect(modal).toHaveBeenCalledWith("hide");
            expect(h.toastr.success).toHaveBeenCalledWith("deleted");
        });

        it("delete: a calendar that no longer exists, or a refused delete, only toasts and keeps the entry", () => {
            const h = build();
            h.$(".delete-calendar").trigger("click");
            h.$("#btnDeleteCalendar").trigger("click");
            h.lastAjax().success!({ result: false, error: "already gone" });
            expect(h.toastr.error).toHaveBeenCalledWith("already gone");

            h.$("#btnDeleteCalendar").trigger("click");
            h.lastAjax().success!({ result: true, calendar: { id: 1 } });
            h.lastAjax().success!({ result: false, error: "in use" });
            expect(h.toastr.error).toHaveBeenCalledWith("in use");
            expect(h.$("#lblCalendar1")).toHaveLength(1);
        });
    });

    describe(`${label} — event popup, edit form and reminder rows`, () => {
        const clickEvent = (h: Handle, over: Record<string, unknown> = {}) => {
            const el = h.win.document.createElement("div");
            h.win.document.body.appendChild(el);
            const event = {
                id: "55", title: "Mine", allDay: false, remove: vi.fn(),
                extendedProps: {
                    calendarType: "My",
                    displayStartDate: "2024-05-01 09:05:00",
                    displayEndDate: "2024-05-01 10:10:00",
                    displayStartDateTimeZone: "UTC",
                    displayEndDateTimeZone: "KST"
                }, ...over
            };
            const result = h.calendarOptions[0].eventClick({ el, event });
            return { event, result, el };
        };
        /** The inline `display` style of the element matching `id` (jsdom does no layout). */
        const display = (h: Handle, id: string) => (h.$(id)[0] as HTMLElement).style.display;

        it("clicking my own timed event opens its popup with the local start/end and both zones", () => {
            const h = build();
            const { result } = clickEvent(h);
            expect(result).toBe(false);
            expect(h.$("#calendarEventPopupTitle").text()).toBe("Mine");
            expect(h.$("#calendarEventPopupStartAllDayUnchecked").text()).toBe("2024-05-01 09:05:00");
            expect(h.$("#calendarEventPopupEndTimeZoneAllDayUnchecked").text()).toBe("(KST)");
            expect(display(h, "#divCalendarEventPopupAllDayUnchecked")).not.toBe("none");
            expect(display(h, "#divCalendarEventPopupAllDayChecked")).toBe("none");
            expect(display(h, "#calendarEventPopup")).toBe("block");
            expect(h.$("#calendarEventPopup").data("event-id")).toBe("55");
        });

        it("an all-day event uses the all-day layout; the close button and a click elsewhere hide the popup", () => {
            const h = build();
            clickEvent(h, { allDay: true, extendedProps: { calendarType: "My", displayStartDate: "2024-05-01", displayEndDate: "2024-05-02" } });
            expect(h.$("#calendarEventPopupStartAllDayChecked").text()).toBe("2024-05-01");
            expect(display(h, "#divCalendarEventPopupAllDayChecked")).not.toBe("none");
            h.$("#closeCalendarEventPopup").trigger("click");
            expect(display(h, "#calendarEventPopup")).toBe("none");

            clickEvent(h);
            h.$(h.win.document.body).trigger("click");
            expect(display(h, "#calendarEventPopup")).toBe("none");
        });

        it("clicking the same event again while its popup is open closes it", () => {
            const h = build();
            (h.$("#calendarEventPopup")[0] as any).getClientRects = () => [1]; // jsdom has no layout
            const el = h.win.document.createElement("div");
            h.win.document.body.appendChild(el);
            const event = { id: "55", title: "Mine", allDay: false, extendedProps: { calendarType: "My" } };
            h.calendarOptions[0].eventClick({ el, event });
            expect(h.calendarOptions[0].eventClick({ el, event })).toBeUndefined();
            expect(display(h, "#calendarEventPopup")).toBe("none");
        });

        it("the popup's delete asks for confirmation, deletes by id, removes the event and toasts; declining does nothing", () => {
            const h = build();
            const { event } = clickEvent(h);
            (h.win as any).confirm = vi.fn(() => false);
            h.$("#deleteCalendarEventPopup").trigger("click");
            expect(h.ajaxCalls.filter((a) => a.url.startsWith("/Calendar/DeleteCalendarEvent"))).toHaveLength(0);

            (h.win as any).confirm = vi.fn(() => true);
            h.$("#deleteCalendarEventPopup").trigger("click");
            expect((h.win as any).confirm).toHaveBeenCalledWith("L_ConfirmDelete");
            const call = h.lastAjax();
            expect(call.url).toBe("/Calendar/DeleteCalendarEvent?id=55");
            call.success!({ result: true, message: "deleted" });
            expect(event.remove).toHaveBeenCalled();
            expect(h.toastr.success).toHaveBeenCalledWith("deleted");
        });

        it("a refused event delete only toasts and keeps the event", () => {
            const h = build();
            const { event } = clickEvent(h);
            h.$("#deleteCalendarEventPopup").trigger("click");
            h.lastAjax().success!({ result: false, error: "not yours" });
            expect(h.toastr.error).toHaveBeenCalledWith("not yours");
            expect(event.remove).not.toHaveBeenCalled();
        });

        it("the popup's edit fetches the event by id; a missing event is toasted, no modal", () => {
            const h = build();
            clickEvent(h);
            h.$("#editCalendarEventPopup").trigger("click");
            expect(h.lastAjax().url).toBe("/Calendar/IsCalendarEventExists?id=55");
            h.lastAjax().success!({ result: false, error: "gone" });
            expect(h.toastr.error).toHaveBeenCalledWith("gone");
        });

        it("a timed event fills the edit form's date, time and zone fields", () => {
            const h = build();
            spyModal(h);
            clickEvent(h);
            h.$("#editCalendarEventPopup").trigger("click");
            h.lastAjax().success!({
                result: true, calendarEvent: {
                    id: 55, title: "Mine", allDay: false, displayStartDate: "2024-05-01 09:05:00", displayEndDate: "2024-05-02 10:10:00",
                    startDateTimeZoneIanaId: "UTC", endDateTimeZoneIanaId: "Asia/Seoul", location: "Room 2", description: "<p>d</p>",
                    calendarEventAttachedFile: null, calendarId: 1, status: "Busy", serializedCalendarReminders: "[]"
                }
            });
            expect(h.$("#editCalendarEventId").val()).toBe("55");
            expect(h.$("#editCalendarEventName").val()).toBe("Mine");
            expect(h.$("#editCalendarEventAllDayUncheckedStartDate").val() ?? "").toBeDefined();
            expect(h.$("#editCalendarEventAllDayUncheckedEndTimeZone").val()).toBe("Asia/Seoul");
            expect(h.$("#editCalendarEventLocation").val()).toBe("Room 2");
        });

        // ---- edit-event submit ----------------------------------------------------------------------------
        const editRow = `<div class="divEditEventNotificationAllDayUncheckedRow">
      <select class="editCalendarEventSelNotificationMethodAllDayUnchecked"><option value="Notification" selected>N</option></select>
      <input class="editCalendarEventSelNotificationNumberAllDayUnchecked" value="30" />
      <select class="editCalendarEventSelNotificationTimeTypeAllDayUnchecked"><option value="Hours" selected>h</option></select>
      <div class="error-message" style="display:none"></div></div>`;
        const buildEdit = (rows = editRow) => {
            const h = build({ body: (html) => html + rows });
            h.$("#editCalendarEventId").val("55");
            h.$("#editCalendarEventAllDayUncheckedStartDate").val("2024-05-01");
            h.$("#editCalendarEventAllDayUncheckedEndDate").val("2024-05-02");
            h.$("#editCalendarEventMyCalendar").html("<option value=\"1\" selected>c</option>");
            return h;
        };
        const updateCall = (h: Handle) => h.ajaxCalls.find((a) => a.url === "/Calendar/UpdateCalendarEvent")!;

        it("saving an edited timed event posts the joined dates, zones and its mapped reminders", () => {
            const h = buildEdit();
            const ev = h.$.Event("submit");
            h.$("#formEditCalendarEvent").trigger(ev);
            const data = updateCall(h).data as FormData;
            expect(ev.isDefaultPrevented()).toBe(true);
            expect(data.get("Id")).toBe("55");
            expect(data.get("Title")).toBe("Edited");
            expect(data.get("StartDate")).toBe("2024-05-01 09:05");
            expect(data.get("EndDate")).toBe("2024-05-02 10:10");
            expect(data.get("Location")).toBe("Room 2");
            expect(JSON.parse(String(data.get("SerializedCalendarReminders")))).toEqual([
                {
                    Method: "Notification",
                    MinutesBeforeEvent: null,
                    HoursBeforeEvent: "30",
                    DaysBeforeEvent: null,
                    WeeksBeforeEvent: null,
                    TimesBeforeEvent: null
                }]);
        });

        it("saving an edited all-day event posts plain dates and blank zones", () => {
            const h = build({ body: (html) => html.replace(/<div class="divEditEventNotificationAllDayCheckedRow">[\s\S]*?<\/div>\s*<\/div>/, "") });
            h.$("#editCalendarEventId").val("55");
            h.$("#editCalendarEventAllDay").prop("checked", true);
            h.$("#editCalendarEventAllDayCheckedStartDate").val("2024-06-01");
            h.$("#editCalendarEventAllDayCheckedEndDate").val("2024-06-03");
            h.$("#formEditCalendarEvent").trigger("submit");
            const data = updateCall(h).data as FormData;
            expect(data.get("AllDay")).toBe("true");
            expect(data.get("StartDate")).toBe("2024-06-01");
            expect(data.get("StartDateTimeZoneIanaId")).toBe("");
        });

        it("an out-of-range reminder or an invalid form blocks the edit", () => {
            const h = buildEdit(editRow.replace("value=\"30\"", "value=\"999999\""));
            h.$("#formEditCalendarEvent").trigger("submit");
            expect(updateCall(h)).toBeUndefined();
            expect(h.$(".divEditEventNotificationAllDayUncheckedRow").last().find(".error-message").text()).toBe("L_ErrorRangeHour");

            const h2 = buildEdit();
            (h2.$.fn as any).valid = () => false;
            h2.$("#formEditCalendarEvent").trigger("submit");
            expect(updateCall(h2)).toBeUndefined();
        });

        it("an accepted edit closes the modal, toasts and reloads the events; a refusal only toasts", () => {
            const h = buildEdit();
            const modal = spyModal(h);
            h.$("#formEditCalendarEvent").trigger("submit");
            updateCall(h).success!({ result: true, message: "updated" });
            expect(h.calendarInstances[0].removeAllEvents).toHaveBeenCalled();
            expect(eventCalls(h)).toHaveLength(1);
            expect(h.toastr.success).toHaveBeenCalledWith("updated");
            expect(modal).toHaveBeenCalledWith("hide");

            const h2 = buildEdit();
            h2.$("#formEditCalendarEvent").trigger("submit");
            updateCall(h2).success!({ result: false, error: "no" });
            expect(h2.toastr.error).toHaveBeenCalledWith("no");
        });

        // ---- attachments and inline images -------------------------------------------------------------------
        it.each(["create", "edit"])("the %s attachment over the size limit is refused with the localized message", (which) => {
            const h = build();
            const file = new h.win.File([new Uint8Array(1_048_577)], "big.zip");
            Object.defineProperty(h.$(`#${which}CalendarEventAttachment`)[0], "files", { configurable: true, value: [file] });
            h.$(`#${which}CalendarEventAttachment`).trigger("change");
            expect(h.win.alert).toHaveBeenCalledWith("big");
        });

        it("an attachment within the limit is sent with the create form", () => {
            const h = build();
            const file = new h.win.File([new Uint8Array(10)], "ok.zip");
            Object.defineProperty(h.$("#createCalendarEventAttachment")[0], "files", { configurable: true, value: [file] });
            h.$("#createCalendarEventAttachment").trigger("change");
            h.$("#createCalendarEventAllDayUncheckedStartDate").val("2024-05-01");
            h.$("#createCalendarEventAllDayUncheckedEndDate").val("2024-05-02");
            h.$("#formCreateCalendarEvent").trigger("submit");
            const created = h.ajaxCalls.find((a) => a.url === "/Calendar/CreateCalendarEvent");
            expect(((created?.data as FormData | undefined)?.get("CalendarEventUploadedFile") as File | null)?.name).toBe("ok.zip");
        });

        // ---- the attachment sent is always the one the form shows ------------------------------------------
        /** Puts `size` bytes named `name` (or nothing, for `null`) into the `which` attachment input and fires its change. */
        const choose = (h: Handle, which: string, file: { size: number; name: string } | null) => {
            const input = h.$(`#${which}CalendarEventAttachment`)[0] as HTMLInputElement;
            const files = file ? [new h.win.File([new Uint8Array(file.size)], file.name)] : [];
            Object.defineProperty(input, "files", { configurable: true, value: files });
            h.$(input).trigger("change");
        };
        /** Submits the `which` event form and returns the attachment it sent. */
        const sentFile = (h: Handle, which: string) => {
            const url = which === "create" ? "/Calendar/CreateCalendarEvent" : "/Calendar/UpdateCalendarEvent";
            const before = h.ajaxCalls.filter((a) => a.url === url).length;
            h.$(which === "create" ? "#formCreateCalendarEvent" : "#formEditCalendarEvent").trigger("submit");
            const calls = h.ajaxCalls.filter((a) => a.url === url);
            expect(calls).toHaveLength(before + 1);
            return { call: calls.at(-1)!, file: (calls.at(-1)!.data as FormData).get("CalendarEventUploadedFile") };
        };
        /** An event form ready to submit. */
        const ready = (which: string) => {
            if (which === "edit") return buildEdit();
            const h = build();
            h.$("#createCalendarEventAllDayUncheckedStartDate").val("2024-05-01");
            h.$("#createCalendarEventAllDayUncheckedEndDate").val("2024-05-02");
            return h;
        };

        it.each(["create", "edit"])("%s: a file rejected for its size replaces the one chosen before — nothing is sent", (which) => {
            const h = ready(which);
            choose(h, which, { size: 10, name: "first.zip" });
            choose(h, which, { size: 1_048_577, name: "too-big.zip" });
            expect(sentFile(h, which).file).not.toBeInstanceOf(h.win.File);
        });

        it.each(["create", "edit"])("%s: cancelling the file picker drops the file chosen before — nothing is sent", (which) => {
            const h = ready(which);
            choose(h, which, { size: 10, name: "first.zip" });
            choose(h, which, null);
            expect(sentFile(h, which).file).not.toBeInstanceOf(h.win.File);
        });

        it.each(["create", "edit"])("%s: once a submit has finished (accepted or refused) its file is not sent again", (which) => {
            for (const answer of [{ result: true, message: "ok" }, { result: false, error: "no" }]) {
                const h = ready(which);
                spyModal(h);
                choose(h, which, { size: 10, name: "first.zip" });
                const first = sentFile(h, which);
                expect((first.file as File).name).toBe("first.zip");
                first.call.success!(answer);
                first.call.complete?.();

                expect(sentFile(h, which).file).not.toBeInstanceOf(h.win.File);
                expect((h.$(`#${which}CalendarEventAttachment`)[0] as HTMLInputElement).value).toBe("");
            }
        });

        it("opening the create-event modal (drag-select or 'add event') starts without the file chosen for an earlier event", () => {
            for (const open of [
                (h: Handle) => h.calendarOptions[0].select({ start: new Date("2024-01-01"), end: new Date("2024-01-02") }),
                (h: Handle) => h.$("#aCreateCalendarEvent").trigger("click"),
            ]) {
                const h = ready("create");
                spyModal(h);
                choose(h, "create", { size: 10, name: "earlier.zip" });
                open(h);
                h.ajaxCalls.filter((a) => a.url === "/Calendar/GetCalendars").at(-1)!.complete?.();
                h.$("#createCalendarEventAllDayUncheckedStartDate").val("2024-05-01");
                h.$("#createCalendarEventAllDayUncheckedEndDate").val("2024-05-02");

                expect(sentFile(h, "create").file).not.toBeInstanceOf(h.win.File);
            }
        });

        it("opening the edit-event modal starts without the file chosen while editing another event", () => {
            const h = buildEdit();
            spyModal(h);
            choose(h, "edit", { size: 10, name: "other-event.zip" });
            clickEvent(h);
            h.$("#editCalendarEventPopup").trigger("click");
            h.lastAjax().success!({
                result: true, calendarEvent: {
                    id: 55, title: "Mine", allDay: false, displayStartDate: "2024-05-01 09:05:00", displayEndDate: "2024-05-02 10:10:00",
                    startDateTimeZoneIanaId: "UTC", endDateTimeZoneIanaId: "Asia/Seoul", location: "", description: "<p>d</p>",
                    calendarEventAttachedFile: null, calendarId: 1, status: "Busy", serializedCalendarReminders: "[]"
                }
            });
            h.ajaxCalls.filter((a) => a.url === "/Calendar/GetCalendars").at(-1)!.complete?.();

            expect(sentFile(h, "edit").file).not.toBeInstanceOf(h.win.File);
        });

        it.each([["create", 0], ["edit", 1]])("the %s editor uploads a dropped image and inserts it with the stored path as alt; a refusal alerts", (which, index) => {
            const h = build();
            h.summernoteInits[index].options.callbacks.onImageUpload([new h.win.File([new Uint8Array(4)], "p.png", { type: "image/png" })]);
            expect(h.lastAjax().url).toBe("/Calendar/UploadImageFile");
            h.lastAjax().success!({ result: true, file: { fileContents: btoa("abc"), contentType: "image/png" }, filePath: "enc" });
            const insert = h.summernoteCalls.find((c) => c.args[0] === "insertNode");
            expect(insert?.el?.id).toBe(`${which}CalendarEventDescription`);
            expect((insert!.args[1] as HTMLImageElement).getAttribute("alt")).toBe("enc");

            h.summernoteInits[index].options.callbacks.onImageUpload([new h.win.File([new Uint8Array(1)], "q.png")]);
            h.lastAjax().success!({ result: false, errorMessage: "png only" });
            expect(h.win.alert).toHaveBeenCalledWith("png only");
        });

        // ---- all-day toggle and reminder rows ------------------------------------------------------------------
        it.each(["create", "edit"])("the %s all-day box switches the date and reminder blocks", (which) => {
            const h = build();
            const box = h.$(`#${which}CalendarEventAllDay`);
            const cap = which[0].toUpperCase() + which.slice(1);
            box.prop("checked", false).trigger("click"); // a click toggles: false → true
            expect(display(h, `#div${cap}EventAllDayChecked`)).not.toBe("none");
            expect(display(h, `#div${cap}EventAllDayUnchecked`)).toBe("none");
            expect(display(h, `#div${cap}EventNotificationAllDayChecked`)).not.toBe("none");
            box.trigger("click"); // true → false
            expect(display(h, `#div${cap}EventAllDayUnchecked`)).not.toBe("none");
            expect(display(h, `#div${cap}EventAllDayChecked`)).toBe("none");
            expect(display(h, `#div${cap}EventNotificationAllDayUnchecked`)).not.toBe("none");
        });

        it.each([["create", true], ["create", false], ["edit", true], ["edit", false]])("the %s add-reminder button (all-day=%s) inserts a fresh row with the right units and defaults", (which, allDay) => {
            const suffix = allDay ? "AllDayChecked" : "AllDayUnchecked";
            const h = build({
                body: (html) => html.replace(`<div id="div${which === "create" ? "Create" : "Edit"}EventNotification${suffix}"></div>`,
                    `<div id="div${which === "create" ? "Create" : "Edit"}EventNotification${suffix}"><a id="${which}CalendarEventNotification${suffix}"></a></div>`)
            });
            const cap = which[0].toUpperCase() + which.slice(1);
            const before = h.$(`.div${cap}EventNotification${suffix}Row`).length; // some fixtures already carry a row
            h.$(`#${which}CalendarEventNotification${suffix}`).trigger("click");
            const rows = h.$(`.div${cap}EventNotification${suffix}Row`);
            expect(rows).toHaveLength(before + 1);
            const row = h.$(`#div${cap}EventNotification${suffix} .div${cap}EventNotification${suffix}Row`); // the inserted one lives in the block
            const units = row.find(`.${which}CalendarEventSelNotificationTimeType${suffix} option`).map((_, o) => (o as HTMLOptionElement).value).get();
            expect(units).toEqual(allDay ? ["Days", "Weeks"] : ["Minutes", "Hours", "Days", "Weeks"]);
            expect(row.find(`.${which}CalendarEventSelNotificationNumber${suffix}`).attr("max")).toBe(allDay ? "28" : "40320");
            if (allDay) expect(row.find(`.${which}CalendarEventSelNotificationTimeAllDayChecked option[selected]`).val()).toBe("09:00");

            row.find(`.a${cap}CalendarDeleteNotification${suffix}`).trigger("click");
            expect(h.$(`.div${cap}EventNotification${suffix}Row`)).toHaveLength(before);
        });
    });

    describe(`${label} — start/end date pickers`, () => {
        /** A controllable jQuery-UI datepicker: dates live in a map keyed by element id; `option` calls are recorded. */
        const fakeDatepicker = (h: Handle) => {
            const dates = new Map<string, Date | null>();
            const options: { id: string; args: unknown[] }[] = [];
            (h.$.fn as any).datepicker = function(this: any, cmd?: string, ...args: unknown[]) {
                const id = this[0]?.id as string;
                if (cmd === "getDate") return dates.get(id) ?? null;
                if (cmd === "setDate") {
                    dates.set(id, args[0] as Date);
                    return this;
                }
                if (cmd === "option") {
                    options.push({ id, args });
                    return this;
                }
                return this;
            };
            return { dates, options };
        };
        const pick = (h: Handle, id: string) => {
            const init = h.datepickerInits.find((i) => i.el?.id === id);
            expect(init, `a datepicker is attached to #${id}`).toBeDefined();
            init!.options.onSelect.call(init!.el);
        };
        const day = (d: number) => new Date(2024, 4, d);

        it.each([
            ["createCalendarEventAllDayUncheckedStartDate", "createCalendarEventAllDayUncheckedEndDate"],
            ["createCalendarEventAllDayCheckedStartDate", "createCalendarEventAllDayCheckedEndDate"],
            ["editCalendarEventAllDayUncheckedStartDate", "editCalendarEventAllDayUncheckedEndDate"],
            ["editCalendarEventAllDayCheckedStartDate", "editCalendarEventAllDayCheckedEndDate"],
        ])("%s → %s: the end never precedes the start", (startId, endId) => {
            const h = build();
            const { dates, options } = fakeDatepicker(h);

            // start moves past the end → the end is dragged forward, and never allowed earlier than the start
            dates.set(startId, day(10));
            dates.set(endId, day(5));
            pick(h, startId);
            expect(dates.get(endId)).toEqual(day(10));
            expect(options.at(-1)).toEqual({ id: endId, args: ["minDate", day(10)] });

            // start moves but the end is still after it → the end is untouched
            dates.set(startId, day(3));
            dates.set(endId, day(20));
            pick(h, startId);
            expect(dates.get(endId)).toEqual(day(20));
            expect(options.at(-1)).toEqual({ id: endId, args: ["minDate", day(3)] });

            // end picked before the start → snapped back to the start
            dates.set(startId, day(15));
            dates.set(endId, day(2));
            pick(h, endId);
            expect(dates.get(endId)).toEqual(day(15));

            // end picked on/after the start → kept
            dates.set(endId, day(16));
            pick(h, endId);
            expect(dates.get(endId)).toEqual(day(16));
        });

        it("a start pick with no end chosen yet only sets the end's minimum date", () => {
            const h = build();
            const { dates, options } = fakeDatepicker(h);
            dates.set("createCalendarEventAllDayUncheckedStartDate", day(10));
            pick(h, "createCalendarEventAllDayUncheckedStartDate");
            expect(dates.has("createCalendarEventAllDayUncheckedEndDate")).toBe(false);
            expect(options.at(-1)?.args).toEqual(["minDate", day(10)]);
        });

        it("the repaint-nudge callbacks (beforeShow / onChangeMonthYear) run without throwing", () => {
            const h = build();
            vi.useFakeTimers(); // after build: loadSite manages its own fake timers while it evaluates the script
            try {
                for (const { options } of h.datepickerInits) {
                    expect(() => {
                        options.beforeShow?.call(null);
                        options.onChangeMonthYear?.call(null);
                    }).not.toThrow();
                }
                expect(() => vi.runAllTimers()).not.toThrow();
            } finally {
                vi.useRealTimers();
            }
        });
    });
}

/** Registers the shared tests for the all-day layout, the event popup and the edit/delete event flows. */
export function describeCalendarExtras(c: { label: string; build: Build }): void {
    const { label, build } = c;
    /** The inline `display` style of the element matching `id` (jsdom does no layout). */
    const display = (h: Handle, id: string) => (h.$(id)[0] as HTMLElement).style.display;

    describe(`${label} — create/edit forms opened with the all-day box already checked`, () => {
        it.each(["create", "edit"])("shows the %s form's all-day layout at load", (which) => {
            const h = build({ body: (html) => html.replace(`id="${which}CalendarEventAllDay"`, `id="${which}CalendarEventAllDay" checked`) });
            const cap = which === "create" ? "Create" : "Edit";
            expect(display(h, `#div${cap}EventAllDayChecked`)).not.toBe("none");
            expect(display(h, `#div${cap}EventAllDayUnchecked`)).toBe("none");
            expect(display(h, `#div${cap}EventNotificationAllDayChecked`)).not.toBe("none");
            expect(display(h, `#div${cap}EventNotificationAllDayUnchecked`)).toBe("none");
        });
    });

    describe(`${label} — drag-select and calendar list order`, () => {
        it("drag-selecting a range: a failed calendar list is toasted and the modal still opens", () => {
            const h = build();
            const modal = spyModal(h);
            h.calendarOptions[0].select({ start: new Date("2024-01-01"), end: new Date("2024-01-02") });
            const call = h.lastAjax();
            expect(call.url).toBe("/Calendar/GetCalendars");
            call.error!();
            call.complete!();
            expect(h.toastr.error).toHaveBeenCalledWith("L_FailedToLoadCalendars");
            expect(modal).toHaveBeenCalledWith("show");
        });

        const labels = (h: Handle) => h.$("#myCalendars > label").map((_, l) => l.id).get();

        it("creating calendars whose names equal or follow an existing one keeps the list sorted (stable for equal names)", () => {
            const h = build();
            spyModal(h);
            const create = (id: number, name: string) => {
                h.$("#formCreateCalendar").trigger("submit");
                h.lastAjax().success!({ result: true, message: "made", calendar: { id, name, htmlColorCode: "#111111" } });
            };
            create(7, "Zeta");   // equal to the existing "Zeta"
            create(8, "Zzz");    // after every existing name
            create(9, "alpha");  // sorting is case-insensitive: equal to "Alpha"
            const names = labels(h).map((id) => h.$(`#${id} > label`).text().trim());
            expect(names.map((n) => n.toUpperCase())).toEqual(["ALPHA", "ALPHA", "MID", "ZETA", "ZETA", "ZZZ"]);
        });

        it("saving an edit to a name equal to, or after, the other calendars keeps the list sorted", () => {
            const h = build();
            vi.useFakeTimers(); // after build: loadSite manages its own fake timers while it evaluates the script
            try {
                spyModal(h);
                h.calendarInstances[0].getEvents = () => [];
                const edit = (name: string) => {
                    h.$("#editCalendarId").val("1");
                    h.$("#editCalendarName").val(name);
                    h.$("#formEditCalendar").trigger("submit");
                    h.lastAjax().success!({ result: true, message: "ok", calendar: { id: 1, name, htmlColorCode: "#222222" } });
                    vi.runAllTimers();
                };
                edit("Alpha"); // equal to calendar 2
                expect(labels(h).filter((id) => ["lblCalendar1", "lblCalendar2"].includes(id))).toHaveLength(2);
                edit("Zzz");   // after everything
                expect(labels(h).at(-1)).toBe("lblCalendar1");
            } finally {
                vi.useRealTimers();
            }
        });
    });

    describe(`${label} — refused lists, other clicks and unusual input`, () => {
        const labels = (h: Handle) => h.$("#myCalendars > label").map((_, l) => l.id).get();
        it("a refused calendar list leaves the create-event calendar select as it was (drag-select and 'add event')", () => {
            for (const open of [
                (h: Handle) => h.calendarOptions[0].select({ start: new Date("2024-01-01"), end: new Date("2024-01-02") }),
                (h: Handle) => h.$("#aCreateCalendarEvent").trigger("click"),
            ]) {
                const h = build();
                spyModal(h);
                h.$("#createCalendarEventMyCalendar").html("<option value=\"9\" selected>kept</option>");
                open(h);
                const call = h.ajaxCalls.filter((a) => a.url === "/Calendar/GetCalendars").at(-1)!;
                call.success!({ result: false, error: "no" });
                call.complete?.();
                expect(h.$("#createCalendarEventMyCalendar option").map((_, o) => o.textContent).get()).toEqual(["kept"]);
            }
        });

        it("saving an edit for a calendar that is not in the list changes no entry and does not throw", () => {
            const h = build();
            vi.useFakeTimers();
            try {
                spyModal(h);
                h.calendarInstances[0].getEvents = () => [];
                const before = labels(h).sort();
                h.$("#editCalendarId").val("1");
                h.$("#editCalendarName").val("Ghost");
                h.$("#formEditCalendar").trigger("submit");
                expect(() => {
                    h.lastAjax().success!({ result: true, message: "ok", calendar: { id: 999, name: "Ghost", htmlColorCode: "#333333" } });
                    vi.runAllTimers();
                }).not.toThrow();
                expect(labels(h).sort()).toEqual(before);
                expect(h.$("#myCalendars").text()).not.toContain("Ghost");
            } finally {
                vi.useRealTimers();
            }
        });

        it("a click on the open event popup itself, or on another calendar event, does not close it", () => {
            const h = build();
            const el = h.win.document.createElement("div");
            h.win.document.body.appendChild(el);
            h.calendarOptions[0].eventClick({
                el,
                event: {
                    id: "55",
                    title: "T",
                    allDay: false,
                    extendedProps: { calendarType: "My", displayStartDate: "", displayEndDate: "", displayStartDateTimeZone: "", displayEndDateTimeZone: "" }
                }
            });
            expect(display(h, "#calendarEventPopup")).toBe("block");
            h.$("#calendarEventPopup").trigger("click");
            const other = h.win.document.createElement("div");
            other.className = "fc-event";
            h.win.document.body.appendChild(other);
            h.$(other).trigger("click");
            expect(display(h, "#calendarEventPopup")).toBe("block");
        });

        it("a reminder row with a unit the form does not offer contributes no reminder", () => {
            const row = (kind: "Create" | "Edit", checked: boolean) => {
                const sfx = checked ? "AllDayChecked" : "AllDayUnchecked";
                const lower = kind.toLowerCase();
                return `<div class="div${kind}EventNotification${sfx}Row">
          <select class="${lower}CalendarEventSelNotificationMethod${sfx}"><option value="Email" selected>m</option></select>
          <input class="${lower}CalendarEventSelNotificationNumber${sfx}" value="1" />
          <select class="${lower}CalendarEventSelNotificationTimeType${sfx}"><option value="Years" selected>y</option></select>
          <select class="${lower}CalendarEventSelNotificationTime${sfx}"><option value="09:00" selected>t</option></select>
          <div class="error-message" style="display:none"></div></div>`;
            };
            let submitted = 0;
            const strip = (html: string) => html.replace(/<div class="div(Create|Edit)EventNotificationAllDay(Un)?checkedRow">[\s\S]*?<\/div>\s*<\/div>/g, "");
            for (const [kind, formId, url, allDay] of [
                ["Create", "#formCreateCalendarEvent", "/Calendar/CreateCalendarEvent", false], ["Create", "#formCreateCalendarEvent", "/Calendar/CreateCalendarEvent", true],
                ["Edit", "#formEditCalendarEvent", "/Calendar/UpdateCalendarEvent", false], ["Edit", "#formEditCalendarEvent", "/Calendar/UpdateCalendarEvent", true],
            ] as const) {
                const h = build({ body: (html) => strip(html) + row(kind, allDay) });
                const lower = kind.toLowerCase();
                h.$(`#${lower}CalendarEventAllDay`).prop("checked", allDay);
                h.$(`#${lower}CalendarEventAllDayUncheckedStartDate`).val("2024-05-01");
                h.$(`#${lower}CalendarEventAllDayUncheckedEndDate`).val("2024-05-02");
                h.$(`#${lower}CalendarEventAllDayCheckedStartDate`).val("2024-05-01");
                h.$(`#${lower}CalendarEventAllDayCheckedEndDate`).val("2024-05-02");
                h.$(`#${lower}CalendarEventMyCalendar`).html("<option value=\"1\" selected>c</option>");
                h.$(formId).trigger("submit");
                const call = h.ajaxCalls.find((a) => a.url === url);
                if (call) {
                    submitted++;
                    expect(JSON.parse(String((call.data as FormData).get("SerializedCalendarReminders")))).toEqual([]);
                }
            }
            expect(submitted).toBeGreaterThan(0); // (a form whose validation refuses the unit sends nothing at all)
        });
    });

    describe(`${label} — inline images in the event editors`, () => {
        const upload = (h: Handle, id: string) => {
            const init = h.summernoteInits.find((i) => (i.el as HTMLElement | undefined)?.id === id)!;
            init.options.callbacks.onImageUpload([new h.win.File([new Uint8Array(4)], "p.png", { type: "image/png" })]);
        };

        it.each([["create", "createCalendarEventDescription"], ["edit", "editCalendarEventDescription"]])(
            `the %s editor stores a dropped image, inserts it as <img alt="">, and releases its object URL once it loaded (or failed to)`, (_which, id) => {
                for (const event of ["onload", "onerror"] as const) {
                    const h = build();
                    (h.win as any).URL.createObjectURL = () => "blob:cal-upload";
                    const revoke = ((h.win as any).URL.revokeObjectURL = vi.fn());
                    upload(h, id);
                    const call = h.lastAjax();
                    expect(call.url).toBe("/Calendar/UploadImageFile");
                    call.success!({ result: true, file: { fileContents: btoa("abc"), contentType: "image/png" }, filePath: "enc-cal" });
                    const insert = h.summernoteCalls.find((x) => x.args[0] === "insertNode" && (x.el as HTMLElement | undefined)?.id === id)!;
                    const img = insert.args[1] as HTMLImageElement;
                    expect(img.getAttribute("alt")).toBe("enc-cal");
                    expect(revoke).not.toHaveBeenCalled();
                    (img[event] as () => void)();
                    expect(revoke).toHaveBeenCalledWith("blob:cal-upload");
                }
            });

        it.each([["create", "createCalendarEventDescription"], ["edit", "editCalendarEventDescription"]])("the %s editor alerts the server's message when an image is refused", (_which, id) => {
            const h = build();
            upload(h, id);
            h.lastAjax().success!({ result: false, errorMessage: "png only" });
            expect(h.win.alert).toHaveBeenCalledWith("png only");
        });
    });
}

/**
 * The edit-event form's detail — reminder rows rebuilt from the stored reminders, the attached file, inline images —
 * shared by the user and admin scripts (both build the same modal).
 */
export function describeEditFormDetail(c: { label: string; build: Build }): void {
    const { label, build } = c;
    const png = "iVBORw0KGgo="; // any base64 — only decoded into a Blob
    /** Edit-form attachment and reminder containers, each reminder list pre-filled with a stale row. */
    const editDom = `
    <div id="divEditCalendarEventAttachedFile" style="display:none"></div><span id="spanEditCalendarEventAttachedFile"></span>
    <div id="divEditEventNotificationAllDayChecked"><div class="divEditEventNotificationAllDayCheckedRow">stale</div><div id="editCalendarEventNotificationAllDayChecked"></div></div>
    <div id="divEditEventNotificationAllDayUnchecked"><div class="divEditEventNotificationAllDayUncheckedRow">stale</div><div id="editCalendarEventNotificationAllDayUnchecked"></div></div>`;
    /** The inline `display` style of the element matching `id` (jsdom does no layout). */
    const display = (h: Handle, id: string) => (h.$(id)[0] as HTMLElement).style.display;
    /** Loads the page with {@link editDom}, stubbed object URLs and a modal spy. */
    const setup = () => {
        // the base fixture already has empty edit-reminder containers; swap them for ones with a stale row and an insertion anchor
        const h = build({
            body: (html) => html
                .replace("<div id=\"divEditEventNotificationAllDayUnchecked\"></div>", "").replace("<div id=\"divEditEventNotificationAllDayChecked\"></div>", "") + editDom
        });
        (h.win as any).URL.createObjectURL = vi.fn(() => "blob:fake");
        (h.win as any).URL.revokeObjectURL = vi.fn();
        spyModal(h);
        return h;
    };
    /** Clicks "my" event 55 through the calendar's `eventClick` callback. */
    const click = (h: Handle) => {
        const el = h.win.document.createElement("div");
        h.win.document.body.appendChild(el);
        return h.calendarOptions[0].eventClick({ el, event: { id: "55", title: "T", allDay: false, extendedProps: { calendarType: "My" } } });
    };
    /** A stored attachment as the event reply describes it. */
    const attachment = { name: "spec", extension: ".pdf", size: 2_500_000 };
    /** An accepted IsCalendarEventExists reply for event 55, with `over` merged into the event. */
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

    /** Runs "my event → edit": the IsCalendarEventExists reply, then GetCalendars settles. */
    const openEdit = (h: Handle, calendarEvent: Record<string, unknown>, calendars = [{ id: 1, name: "One" }]) => {
        click(h);
        h.$("#editCalendarEventPopup").trigger("click");
        h.lastAjax().success!(payload(calendarEvent));
        const inner = h.lastAjax();
        inner.success!({ result: true, calendars });
        inner.complete!();
    };

    describe(`${label} — edit form in detail`, () => {
        it("an all-day event rebuilds its day/week reminder rows (replacing stale ones) with the stored method, number, unit and time", () => {
            const h = setup();
            openEdit(h, {
                allDay: true, displayStartDate: "2024-05-01", displayEndDate: "2024-05-03", serializedCalendarReminders: JSON.stringify([
                    reminder({ Method: "Notification", DaysBeforeEvent: 3, TimesBeforeEvent: "09:00:00" }), reminder({ WeeksBeforeEvent: 2 })])
            });
            const rows = h.$("#divEditEventNotificationAllDayChecked .divEditEventNotificationAllDayCheckedRow");
            expect(rows).toHaveLength(2); // the "stale" row is gone
            expect(rows.eq(0).find(".editCalendarEventSelNotificationMethodAllDayChecked").val()).toBe("Notification");
            expect(rows.eq(0).find(".editCalendarEventSelNotificationNumberAllDayChecked").val()).toBe("3");
            expect(rows.eq(0).find(".editCalendarEventSelNotificationTimeTypeAllDayChecked").val()).toBe("Days");
            expect(rows.eq(0).find(".editCalendarEventSelNotificationTimeAllDayChecked").val()).toBe("09:00");
            expect(rows.eq(1).find(".editCalendarEventSelNotificationMethodAllDayChecked").val()).toBe("Email");
            expect(rows.eq(1).find(".editCalendarEventSelNotificationNumberAllDayChecked").val()).toBe("2");
            expect(rows.eq(1).find(".editCalendarEventSelNotificationTimeTypeAllDayChecked").val()).toBe("Weeks");
            expect(h.$("#editCalendarEventAllDay").prop("checked")).toBe(true);
            expect(h.$("#editCalendarEventAllDayCheckedStartDate").val()).toBe("2024-05-01");
        });

        it("a timed event rebuilds its minute/hour/day/week reminder rows with the stored unit", () => {
            const h = setup();
            openEdit(h, {
                serializedCalendarReminders: JSON.stringify([
                    reminder({ MinutesBeforeEvent: 15 }), reminder({ Method: "Notification", HoursBeforeEvent: 2 }),
                    reminder({ DaysBeforeEvent: 1 }), reminder({ WeeksBeforeEvent: 4 })])
            });
            const rows = h.$("#divEditEventNotificationAllDayUnchecked .divEditEventNotificationAllDayUncheckedRow");
            expect(rows).toHaveLength(4);
            const summary = rows.map((_, r) => `${h.$(r).find(".editCalendarEventSelNotificationTimeTypeAllDayUnchecked").val()}:${h.$(r).find(".editCalendarEventSelNotificationNumberAllDayUnchecked").val()}:${h.$(r).find(".editCalendarEventSelNotificationMethodAllDayUnchecked").val()}`).get();
            expect(summary).toEqual(["Minutes:15:Email", "Hours:2:Notification", "Days:1:Email", "Weeks:4:Email"]);
        });

        it("a timed event with no reminders leaves only the (cleared) container", () => {
            const h = setup();
            openEdit(h, { serializedCalendarReminders: "[]" });
            expect(h.$(".divEditEventNotificationAllDayUncheckedRow")).toHaveLength(0);
        });

        it("an attached file shows its link, a comma-grouped size in KB and the event it is downloaded for — not the file's bytes", () => {
            const h = setup();
            openEdit(h, { calendarEventAttachedFile: attachment });
            expect(display(h, "#divEditCalendarEventAttachedFile")).not.toBe("none");
            expect(h.$("#aEditCalendarEventAttachedFile").text()).toBe("spec.pdf");
            expect(h.$("#aEditCalendarEventAttachedFile").attr("data-name")).toBe("spec.pdf");
            expect(h.$("#aEditCalendarEventAttachedFile").attr("data-calendareventid")).toBe("55");
            expect(h.$("#aEditCalendarEventAttachedFile").attr("data-file")).toBeUndefined();
            expect(h.$("#aEditCalendarEventAttachedFile").attr("data-contenttype")).toBeUndefined();
            expect(h.$("#spanEditCalendarEventAttachedFile").text()).toBe("2,441KB");
        });

        it("without an attachment the attachment row stays hidden", () => {
            const h = setup();
            openEdit(h, {});
            expect(display(h, "#divEditCalendarEventAttachedFile")).toBe("none");
        });

        it("inline images in the description are rebuilt from their embedded data into object URLs before the editor gets the HTML", () => {
            const h = setup();
            openEdit(h, { description: `<p>x</p><img data-file="${png}" data-contenttype="image/png" alt=""><img src="keep.png" alt="">` });
            const code = h.summernoteCalls.filter((c) => (c.el as HTMLElement | undefined)?.id === "editCalendarEventDescription" && c.args[0] === "code").pop();
            const html = String(code!.args[1]);
            expect(html).toContain("src=\"blob:fake\"");
            expect(html).not.toContain("data-file");
            expect(html).not.toContain("data-contenttype");
            expect(html).toContain("src=\"keep.png\"");
        });

        it.each(["onload", "onerror"] as const)("the edit form's rebuilt image %s releases the object URL it was given", (event) => {
            const h = build({
                body: (html) => html
                    .replace("<div id=\"divEditEventNotificationAllDayUnchecked\"></div>", "").replace("<div id=\"divEditEventNotificationAllDayChecked\"></div>", "")
                    .replace(`<div id="editCalendarEventDescription"></div>`, `<div id="editCalendarEventDescription"></div><div class="note-editor"><img src="blob:live" alt=""></div>`) + editDom
            });
            (h.win as any).URL.createObjectURL = vi.fn(() => "blob:fake");
            spyModal(h);
            openEdit(h, { description: `<img data-file="${png}" data-contenttype="image/png" alt="">` });
            const revoke = ((h.win as any).URL.revokeObjectURL = vi.fn());
            const img = h.$(".note-editor img")[0] as HTMLImageElement;

            (img[event] as () => void)();

            expect(revoke).toHaveBeenCalledWith("blob:live");
        });

        it("an inline image with no content type is left exactly as stored", () => {
            const h = setup();
            openEdit(h, { description: `<img data-file="${png}" alt="">` });
            const code = h.summernoteCalls.filter((c) => (c.el as HTMLElement | undefined)?.id === "editCalendarEventDescription" && c.args[0] === "code").pop();
            expect(String(code!.args[1])).toContain("data-file");
            expect(String(code!.args[1])).not.toContain("blob:");
        });

        it("a failing calendar list toasts but the modal still opens", () => {
            const h = setup();
            click(h);
            h.$("#editCalendarEventPopup").trigger("click");
            h.lastAjax().success!(payload());
            const inner = h.lastAjax();
            inner.success!({ result: false });
            inner.error!();
            inner.complete!();
            expect(h.toastr.error).toHaveBeenCalledWith("L_FailedToLoadCalendars");
            expect(h.$("#editCalendarEventStatus").val()).toBe("Busy");
        });

        it("choosing an attachment within the size limit is accepted silently", () => {
            const h = setup();
            const file = new h.win.File([new Uint8Array(10)], "ok.txt");
            Object.defineProperty(h.$("#editCalendarEventAttachment")[0], "files", { configurable: true, value: [file] });
            h.$("#editCalendarEventAttachment").trigger("change");
            expect(h.win.alert).not.toHaveBeenCalled();
        });
    });

    describe(`${label} — edit submit with every reminder shape, the event reload, the attachment download and the calendar menu`, () => {
        const timedRow = (method: string, unit: string, n: string, time = "") => `<div class="divEditEventNotificationAllDayUncheckedRow">
      <select class="editCalendarEventSelNotificationMethodAllDayUnchecked"><option value="${method}" selected>m</option></select>
      <input class="editCalendarEventSelNotificationNumberAllDayUnchecked" value="${n}" />
      <select class="editCalendarEventSelNotificationTimeTypeAllDayUnchecked"><option value="${unit}" selected>u</option></select>
      ${time ? `<select class="editCalendarEventSelNotificationTimeAllDayUnchecked"><option value="${time}" selected>t</option></select>` : ""}
      <div class="error-message" style="display:none"></div></div>`;
        const allDayRow = (method: string, unit: string, n: string, time: string) => `<div class="divEditEventNotificationAllDayCheckedRow">
      <select class="editCalendarEventSelNotificationMethodAllDayChecked"><option value="${method}" selected>m</option></select>
      <input class="editCalendarEventSelNotificationNumberAllDayChecked" value="${n}" />
      <select class="editCalendarEventSelNotificationTimeTypeAllDayChecked"><option value="${unit}" selected>u</option></select>
      <select class="editCalendarEventSelNotificationTimeAllDayChecked"><option value="${time}" selected>t</option></select>
      <div class="error-message" style="display:none"></div></div>`;
        const strip = (html: string) => html
            .replace(/<div class="divEditEventNotificationAllDay(Un)?checkedRow">[\s\S]*?<\/div>\s*<\/div>/g, "");
        const submitEdit = (rows: string, allDay: boolean) => {
            const h = build({ body: (html) => strip(html) + rows });
            h.$("#editCalendarEventId").val("55");
            h.$("#editCalendarEventAllDay").prop("checked", allDay);
            h.$("#editCalendarEventAllDayUncheckedStartDate").val("2024-05-01");
            h.$("#editCalendarEventAllDayUncheckedEndDate").val("2024-05-02");
            h.$("#editCalendarEventAllDayCheckedStartDate").val("2024-05-01");
            h.$("#editCalendarEventAllDayCheckedEndDate").val("2024-05-02");
            h.$("#editCalendarEventMyCalendar").html("<option value=\"1\" selected>c</option>");
            h.$("#formEditCalendarEvent").trigger("submit");
            const call = h.ajaxCalls.find((a) => a.url === "/Calendar/UpdateCalendarEvent");
            return { h, call, reminders: call ? JSON.parse(String((call.data as FormData).get("SerializedCalendarReminders"))) : undefined };
        };
        const shape = (o: Record<string, unknown>) => ({
            Method: "Email",
            MinutesBeforeEvent: null,
            HoursBeforeEvent: null,
            DaysBeforeEvent: null,
            WeeksBeforeEvent: null,
            TimesBeforeEvent: null, ...o
        });

        it("a timed edit maps minute, hour, day and week rows each to its own field", () => {
            const { reminders } = submitEdit(
                timedRow("Email", "Minutes", "15") + timedRow("Notification", "Hours", "2") + timedRow("Email", "Days", "3") + timedRow("Email", "Weeks", "1"), false);
            expect(reminders).toEqual([
                shape({ MinutesBeforeEvent: "15" }), shape({ Method: "Notification", HoursBeforeEvent: "2" }),
                shape({ DaysBeforeEvent: "3" }), shape({ WeeksBeforeEvent: "1" })]);
        });

        it("an all-day edit maps day and week rows with their time of day", () => {
            const { reminders } = submitEdit(allDayRow("Email", "Days", "3", "09:00") + allDayRow("Notification", "Weeks", "2", "18:30"), true);
            expect(reminders).toEqual([
                shape({ DaysBeforeEvent: "3", TimesBeforeEvent: "09:00" }), shape({
                    Method: "Notification",
                    WeeksBeforeEvent: "2",
                    TimesBeforeEvent: "18:30"
                })]);
        });

        it("an all-day edit with an out-of-range reminder is blocked and flags the row", () => {
            const { call, h } = submitEdit(allDayRow("Email", "Days", "99", "09:00"), true);
            expect(call).toBeUndefined();
            expect(h.$(".divEditEventNotificationAllDayCheckedRow").last().find(".error-message").text()).toBe("L_ErrorRangeDay");
        });

        it("after an accepted edit the reloaded events are added to the grid with their display fields; an empty reload only toasts", () => {
            const { h, call } = submitEdit(timedRow("Email", "Hours", "1"), false);
            call!.success!({ result: true, message: "updated" });
            const reload = eventCalls(h)[0];
            reload.success!({ result: true, calendarEvents: JSON.stringify([evt(50, 1)]) });
            const added = h.calendarInstances[0].addEvent.mock.calls.at(-1)[0];
            expect(added).toMatchObject({ id: 50, title: "Event 50", backgroundColor: "#123456" });
            expect(added.extendedProps).toMatchObject({ calendarId: 1, displayStartDate: "2024-05-01 10:00:00", displayEndDateTimeZone: "UTC" });

            reload.success!({ result: false, error: "no events for you" });
            expect(h.toastr.error).toHaveBeenCalledWith("no events for you"); // the reload's own message, not the (successful) edit reply's
        });

        it("clicking the attached-file link asks the download endpoint for that event's attachment as a blob", () => {
            const h = setup();
            openEdit(h, { calendarEventAttachedFile: attachment });
            const ev = h.$.Event("click");
            h.$("#aEditCalendarEventAttachedFile").trigger(ev);
            expect(ev.isDefaultPrevented()).toBe(true);
            const call = h.lastAjax() as any;
            expect(call.url).toBe("/Calendar/DownloadCalendarEventAttachedFile");
            expect(call.type).toBe("POST");
            expect(call.data).toEqual({ calendarEventId: "55" });
            expect(call.headers).toEqual({ RequestVerificationToken: "tok" });
            expect(call.xhrFields).toEqual({ responseType: "blob" });
        });

        it("the returned file is saved under the attachment's name through a temporary object URL, revoked shortly after", () => {
            const h = setup();
            openEdit(h, { calendarEventAttachedFile: attachment });
            const clicked: HTMLAnchorElement[] = [];
            (h.win as any).HTMLAnchorElement.prototype.click = function(this: HTMLAnchorElement) {
                clicked.push(this);
            };
            h.$("#aEditCalendarEventAttachedFile").trigger("click");
            vi.useFakeTimers();
            try {
                h.respond(0, new h.win.Blob(["pdf-bytes"], { type: "application/pdf" }));
                expect(clicked).toHaveLength(1);
                expect(clicked[0].download).toBe("spec.pdf");
                expect(clicked[0].href).toBe("blob:fake");
                expect((h.win as any).URL.revokeObjectURL).not.toHaveBeenCalled();
                vi.advanceTimersByTime(100);
                expect((h.win as any).URL.revokeObjectURL).toHaveBeenCalledWith("blob:fake");
            } finally {
                vi.useRealTimers();
            }
        });

        it("a refused download is shown as the server's error and nothing is saved", async () => {
            const h = setup();
            openEdit(h, { calendarEventAttachedFile: attachment });
            const clicked: unknown[] = [];
            (h.win as any).HTMLAnchorElement.prototype.click = function() {
                clicked.push(this);
            };
            h.$("#aEditCalendarEventAttachedFile").trigger("click");
            h.respondOverHttp(0, JSON.stringify({ result: false, error: "The calendar event could not be found." }), "application/json; charset=utf-8");
            await vi.waitFor(() => expect(h.toastr.error).toHaveBeenCalledWith("The calendar event could not be found."));
            expect(clicked).toHaveLength(0);
        });

        it("a link that names no event requests nothing", () => {
            const h = setup();
            const before = h.ajaxCalls.length;
            h.$("#aEditCalendarEventAttachedFile").trigger("click");
            expect(h.ajaxCalls).toHaveLength(before);
        });

        it("the calendar menu icon toggles the menu, a click outside or on a menu link hides it, and 'create calendar' opens its dialog", () => {
            const h = build({ body: (html) => html.replace("<div id=\"dropdown-content\"></div>", "<div id=\"dropdown-content\"><a id=\"aInMenu\" href=\"#\">x</a></div>") });
            const modal = spyModal(h);
            const hidden = () => (h.$("#dropdown-content")[0] as HTMLElement).style.display === "none";
            h.$("#dropdown-icon").trigger("click");
            expect(hidden()).toBe(true); // toggle() on jsdom's non-laid-out element hides it first
            h.$("#dropdown-icon").trigger("click");
            expect(hidden()).toBe(false);
            h.$("#aInMenu").trigger("click");
            expect(hidden()).toBe(true);

            h.$("#dropdown-icon").trigger("click");
            h.$(h.win as any).trigger("click");
            expect(hidden()).toBe(true);

            h.$("#aCreateCalendar").trigger("click");
            expect(modal).toHaveBeenCalledWith("show");
        });
    });
}

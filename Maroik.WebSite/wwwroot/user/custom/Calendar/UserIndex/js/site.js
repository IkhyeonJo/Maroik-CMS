function base64ToBlob(base64, mime) {
    const byteCharacters = atob(base64);
    const byteNumbers = new Array(byteCharacters.length);
    for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
    }
    const byteArray = new Uint8Array(byteNumbers);
    return new Blob([byteArray], { type: mime });
}

const localizer = {
    Email: $('#localizerEmail').val(),
    Notification: $('#localizerNotification').val(),
    Minutes: $('#localizerMinutes').val(),
    Hours: $('#localizerHours').val(),
    Days: $('#localizerDays').val(),
    Weeks: $('#localizerWeeks').val(),
    BeforeAt: $('#localizerBeforeAt').val(),
    PrevText: $('#localizerPrevText').val(),
    NextText: $('#localizerNextText').val(),
    January: $('#localizerJanuary').val(),
    February: $('#localizerFebruary').val(),
    March: $('#localizerMarch').val(),
    April: $('#localizerApril').val(),
    May: $('#localizerMay').val(),
    June: $('#localizerJune').val(),
    July: $('#localizerJuly').val(),
    August: $('#localizerAugust').val(),
    September: $('#localizerSeptember').val(),
    October: $('#localizerOctober').val(),
    November: $('#localizerNovember').val(),
    December: $('#localizerDecember').val(),
    Jan: $('#localizerJan').val(),
    Feb: $('#localizerFeb').val(),
    Mar: $('#localizerMar').val(),
    Apr: $('#localizerApr').val(),
    Jun: $('#localizerJun').val(),
    Jul: $('#localizerJul').val(),
    Aug: $('#localizerAug').val(),
    Sep: $('#localizerSep').val(),
    Oct: $('#localizerOct').val(),
    Nov: $('#localizerNov').val(),
    Dec: $('#localizerDec').val(),
    Sunday: $('#localizerSunday').val(),
    Monday: $('#localizerMonday').val(),
    Tuesday: $('#localizerTuesday').val(),
    Wednesday: $('#localizerWednesday').val(),
    Thursday: $('#localizerThursday').val(),
    Friday: $('#localizerFriday').val(),
    Saturday: $('#localizerSaturday').val(),
    Sun: $('#localizerSun').val(),
    Mon: $('#localizerMon').val(),
    Tue: $('#localizerTue').val(),
    Wed: $('#localizerWed').val(),
    Thu: $('#localizerThu').val(),
    Fri: $('#localizerFri').val(),
    Sat: $('#localizerSat').val(),
    Su: $('#localizerSu').val(),
    Mo: $('#localizerMo').val(),
    Tu: $('#localizerTu').val(),
    We: $('#localizerWe').val(),
    Th: $('#localizerTh').val(),
    Fr: $('#localizerFr').val(),
    Sa: $('#localizerSa').val(),
    YearSuffix: $('#localizerYearSuffix').val(),
    IETFLanguageTag: $('#localizerIETFLanguageTag').val(),
    Prev: $('#localizerPrev').val(),
    Next: $('#localizerNext').val(),
    PrevYear: $('#localizerPrevYear').val(),
    NextYear: $('#localizerNextYear').val(),
    Today: $('#localizerToday').val(),
    Month: $('#localizerMonth').val(),
    Week: $('#localizerWeek').val(),
    Day: $('#localizerDay').val(),
    List: $('#localizerList').val(),
    DayGridMonth: $('#localizerDayGridMonth').val(),
    DayGridWeek: $('#localizerDayGridWeek').val(),
    DayGridDay: $('#localizerDayGridDay').val(),
    TimeGridWeek: $('#localizerTimeGridWeek').val(),
    TimeGridDay: $('#localizerTimeGridDay').val(),
    ListYear: $('#localizerListYear').val(),
    ListMonth: $('#localizerListMonth').val(),
    ListWeek: $('#localizerListWeek').val(),
    ListDay: $('#localizerListDay').val(),
    ConfirmDelete: $('#localizerConfirmDelete').val(),
    ThisFieldRequired: $('#localizerThisFieldRequired').val(),
    ErrorInvalidNumber: $('#localizerErrorInvalidNumber').val(),
    ErrorRangeMinute: $('#localizerErrorRangeMinute').val(),
    ErrorRangeHour: $('#localizerErrorRangeHour').val(),
    ErrorRangeDay: $('#localizerErrorRangeDay').val(),
    ErrorRangeWeek: $('#localizerErrorRangeWeek').val()
};

let calendar;

$.datepicker.setDefaults({
    dateFormat: 'yy-mm-dd',
    prevText: localizer.prevText,
    nextText: localizer.nextText,
    monthNames: [localizer.January, localizer.February, localizer.March, localizer.April, localizer.May, localizer.June, localizer.July, localizer.August, localizer.September, localizer.October, localizer.November, localizer.December],
    monthNamesShort: [localizer.Jan, localizer.Feb, localizer.Mar, localizer.Apr, localizer.May, localizer.Jun, localizer.Jul, localizer.Aug, localizer.Sep, localizer.Oct, localizer.Nov, localizer.Dec],
    dayNames: [localizer.Sunday, localizer.Monday, localizer.Tuesday, localizer.Wednesday, localizer.Thursday, localizer.Friday, localizer.Saturday],
    dayNamesShort: [localizer.Sun, localizer.Mon, localizer.Tue, localizer.Wed, localizer.Thu, localizer.Fri, localizer.Sat],
    dayNamesMin: [localizer.Su, localizer.Mo, localizer.Tu, localizer.We, localizer.Th, localizer.Fr, localizer.Sa],
    showMonthAfterYear: true,
    yearSuffix: localizer.YearSuffix
});

$('#createCalendarEventDescription').summernote({
    lang: localizer.IETFLanguageTag,
    callbacks: {
        onImageUpload: function (files) {
            for (let i = 0; i < files.length; i++) {
                CreateUploadImageFile(files[i]);
            }
            $("#createCalendarEventTaskDialogModal").css("overflow", "scroll");
        }
    }
});

$('#editCalendarEventDescription').summernote({
    lang: localizer.IETFLanguageTag,
    callbacks: {
        onImageUpload: function (files) {
            for (let i = 0; i < files.length; i++) {
                EditUploadImageFile(files[i]);
            }
            $("#editCalendarEventTaskDialogModal").css("overflow", "scroll");
        }
    }
});

let createCalendarEventUploadedFile;
let editCalendarEventUploadedFile;

function CreateCalendarEventUploadedFile(obj, errorMessage) {
    if (obj.files[0].size > 10485760) {
        alert(errorMessage);
        document.getElementById("createCalendarEventAttachment").value = "";
        return false;
    }
    else {
        createCalendarEventUploadedFile = obj.files[0];
    }
}

function EditCalendarEventUploadedFile(obj, errorMessage) {
    if (obj.files[0].size > 10485760) {
        alert(errorMessage);
        document.getElementById("editCalendarEventAttachment").value = "";
        return false;
    }
    else {
        editCalendarEventUploadedFile = obj.files[0];
    }
}

$('#createCalendarEventAttachment').off('change').on('change', function () {
    return CreateCalendarEventUploadedFile(this, $(this).attr('data-errorMessage'));
});

$('#editCalendarEventAttachment').off('change').on('change', function () {
    return EditCalendarEventUploadedFile(this, $(this).attr('data-errorMessage'));
});

$(function () {
    $("#createCalendarTabs").tabs();
    $("#editCalendarTabs").tabs();
    $("#createCalendarEventTaskTabs").tabs();
    $("#editCalendarEventTaskTabs").tabs();
    $("#viewCalendarEventTaskTabs").tabs();
    $('#browseCalendarsOfInterestTabs').tabs();

    $("#createCalendarEventAllDayUncheckedStartDate").datepicker({
        showButtonPanel: true,
        beforeShow: function (input) {
            setTimeout(function () {
            }, 1);
        },
        onChangeMonthYear: function (year, month, instance) {
            setTimeout(function () {
            }, 1);
        },
        onSelect: function (selectedDate) {
            let startDate = $(this).datepicker('getDate');
            let endDate = $("#createCalendarEventAllDayUncheckedEndDate").datepicker('getDate');
            if (endDate && endDate < startDate) {
                $("#createCalendarEventAllDayUncheckedEndDate").datepicker('setDate', startDate);
            }
            $("#createCalendarEventAllDayUncheckedEndDate").datepicker('option', 'minDate', startDate);
        }
    });

    $("#createCalendarEventAllDayUncheckedEndDate").datepicker({
        showButtonPanel: true,
        beforeShow: function (input) {
            setTimeout(function () {
            }, 1);
        },
        onChangeMonthYear: function (year, month, instance) {
            setTimeout(function () {
            }, 1);
        },
        onSelect: function (selectedDate) {
            let endDate = $(this).datepicker('getDate');
            let startDate = $("#createCalendarEventAllDayUncheckedStartDate").datepicker('getDate');
            if (startDate && endDate < startDate) {
                $(this).datepicker('setDate', startDate);
            }
        }
    });

    $("#createCalendarEventAllDayCheckedStartDate").datepicker({
        showButtonPanel: true,
        beforeShow: function (input) {
            setTimeout(function () {
            }, 1);
        },
        onChangeMonthYear: function (year, month, instance) {
            setTimeout(function () {
            }, 1);
        },
        onSelect: function (selectedDate) {
            let startDate = $(this).datepicker('getDate');
            let endDate = $("#createCalendarEventAllDayCheckedEndDate").datepicker('getDate');
            if (endDate && endDate < startDate) {
                $("#createCalendarEventAllDayCheckedEndDate").datepicker('setDate', startDate);
            }
            $("#createCalendarEventAllDayCheckedEndDate").datepicker('option', 'minDate', startDate);
        }
    });

    $("#createCalendarEventAllDayCheckedEndDate").datepicker({
        showButtonPanel: true,
        beforeShow: function (input) {
            setTimeout(function () {
            }, 1);
        },
        onChangeMonthYear: function (year, month, instance) {
            setTimeout(function () {
            }, 1);
        },
        onSelect: function (selectedDate) {
            let endDate = $(this).datepicker('getDate');
            let startDate = $("#createCalendarEventAllDayCheckedStartDate").datepicker('getDate');
            if (startDate && endDate < startDate) {
                $(this).datepicker('setDate', startDate);
            }
        }
    });

    $("#editCalendarEventAllDayUncheckedStartDate").datepicker({
        showButtonPanel: true,
        beforeShow: function (input) {
            setTimeout(function () {
            }, 1);
        },
        onChangeMonthYear: function (year, month, instance) {
            setTimeout(function () {
            }, 1);
        },
        onSelect: function (selectedDate) {
            let startDate = $(this).datepicker('getDate');
            let endDate = $("#editCalendarEventAllDayUncheckedEndDate").datepicker('getDate');
            if (endDate && endDate < startDate) {
                $("#editCalendarEventAllDayUncheckedEndDate").datepicker('setDate', startDate);
            }
            $("#editCalendarEventAllDayUncheckedEndDate").datepicker('option', 'minDate', startDate);
        }
    });

    $("#editCalendarEventAllDayUncheckedEndDate").datepicker({
        showButtonPanel: true,
        beforeShow: function (input) {
            setTimeout(function () {
            }, 1);
        },
        onChangeMonthYear: function (year, month, instance) {
            setTimeout(function () {
            }, 1);
        },
        onSelect: function (selectedDate) {
            let endDate = $(this).datepicker('getDate');
            let startDate = $("#editCalendarEventAllDayUncheckedStartDate").datepicker('getDate');
            if (startDate && endDate < startDate) {
                $(this).datepicker('setDate', startDate);
            }
        }
    });

    $("#editCalendarEventAllDayCheckedStartDate").datepicker({
        showButtonPanel: true,
        beforeShow: function (input) {
            setTimeout(function () {
            }, 1);
        },
        onChangeMonthYear: function (year, month, instance) {
            setTimeout(function () {
            }, 1);
        },
        onSelect: function (selectedDate) {
            let startDate = $(this).datepicker('getDate');
            let endDate = $("#editCalendarEventAllDayCheckedEndDate").datepicker('getDate');
            if (endDate && endDate < startDate) {
                $("#editCalendarEventAllDayCheckedEndDate").datepicker('setDate', startDate);
            }
            $("#editCalendarEventAllDayCheckedEndDate").datepicker('option', 'minDate', startDate);
        }
    });

    $("#editCalendarEventAllDayCheckedEndDate").datepicker({
        showButtonPanel: true,
        beforeShow: function (input) {
            setTimeout(function () {
            }, 1);
        },
        onChangeMonthYear: function (year, month, instance) {
            setTimeout(function () {
            }, 1);
        },
        onSelect: function (selectedDate) {
            let endDate = $(this).datepicker('getDate');
            let startDate = $("#editCalendarEventAllDayCheckedStartDate").datepicker('getDate');
            if (startDate && endDate < startDate) {
                $(this).datepicker('setDate', startDate);
            }
        }
    });

    $('<style type="text/css"> .ui-datepicker-close { display: none; } </style>').appendTo("head");
    $('<style type="text/css"> .ui-datepicker-current { display: none; } </style>').appendTo("head");

    if ($("#createCalendarEventAllDay").is(':checked')) {
        $("#divCreateEventAllDayUnchecked").hide();
        $("#divCreateEventAllDayChecked").hide();
        $("#divCreateEventAllDayChecked").show();

        $("#divCreateEventNotificationAllDayUnchecked").hide();
        $("#divCreateEventNotificationAllDayChecked").hide();
        $("#divCreateEventNotificationAllDayChecked").show();
    }
    else {
        $("#divCreateEventAllDayUnchecked").hide();
        $("#divCreateEventAllDayChecked").hide();
        $("#divCreateEventAllDayUnchecked").show();

        $("#divCreateEventNotificationAllDayUnchecked").hide();
        $("#divCreateEventNotificationAllDayChecked").hide();
        $("#divCreateEventNotificationAllDayUnchecked").show();
    }

    if ($("#editCalendarEventAllDay").is(':checked')) {
        $("#divEditEventAllDayUnchecked").hide();
        $("#divEditEventAllDayChecked").hide();
        $("#divEditEventAllDayChecked").show();

        $("#divEditEventNotificationAllDayUnchecked").hide();
        $("#divEditEventNotificationAllDayChecked").hide();
        $("#divEditEventNotificationAllDayChecked").show();
    }
    else {
        $("#divEditEventAllDayUnchecked").hide();
        $("#divEditEventAllDayChecked").hide();
        $("#divEditEventAllDayUnchecked").show();

        $("#divEditEventNotificationAllDayUnchecked").hide();
        $("#divEditEventNotificationAllDayChecked").hide();
        $("#divEditEventNotificationAllDayUnchecked").show();
    }

    calendar = new FullCalendar.Calendar(document.getElementById('calendar'), {
        locale: localizer.IETFLanguageTag,
        headerToolbar: {
            left: 'prevYear,prev,next,nextYear today',
            center: 'title',
            right: 'dayGridMonth'
        },
        buttonText: {
            today: localizer.Today,
            dayGridMonth: localizer.Month
        },
        themeSystem: 'bootstrap',
        navLinks: false,
        editable: false,
        selectable: true,
        selectMirror: true,
        select: function (arg) {
            $('#createCalendarEventAllDayUncheckedStartDate').val(moment.utc(arg.start).add(1, 'days').format("YYYY-MM-DD"));
            $('#createCalendarEventAllDayCheckedStartDate').val(moment.utc(arg.start).add(1, 'days').format("YYYY-MM-DD"));

            $('#createCalendarEventAllDayUncheckedEndDate').val(moment.utc(arg.end).format("YYYY-MM-DD"));
            $('#createCalendarEventAllDayCheckedEndDate').val(moment.utc(arg.end).format("YYYY-MM-DD"));

            $.ajax({
                url: '/Calendar/GetCalendars',
                method: 'POST',
                headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                dataType: 'json',
                data: null,
                contentType: 'application/json; charset=utf-8',
                async: false,
                success: function (response) {
                    if (response.result) {
                        $('#createCalendarEventMyCalendar').empty();

                        $.each(response.calendars, function (index, calendar) {
                            $('#createCalendarEventMyCalendar').append($('<option>', {
                                value: calendar.id,
                                text: calendar.name
                            }));
                        });

                        $('#createCalendarEventMyCalendar').val(data.calendarEvent.calendarId);
                    }
                }
            });

            $('#createCalendarEventAllDayUncheckedStartTimeZone').val($('#loginedAccountTimeZoneIanaId').val());
            $('#createCalendarEventAllDayUncheckedEndTimeZone').val($('#loginedAccountTimeZoneIanaId').val());

            $('#createCalendarEventTaskDialogModal').modal('show');

            calendar.unselect();
        },
        eventClick: function (arg) {
            if (arg.event.extendedProps.calendarType === 'My') {

                let eventEl = $(arg.el);
                let offset = eventEl.offset();
                let popup = $('#calendarEventPopup');
                let currentEventId = popup.data('event-id');

                if (popup.is(':visible') && currentEventId === arg.event.id) {
                    popup.hide();
                    return;
                }

                $('#calendarEventPopupTitle').text(arg.event.title);
                $('#divCalendarEventPopupAllDayChecked').hide();
                $('#divCalendarEventPopupAllDayUnchecked').hide();

                if (arg.event.allDay === true) {
                    $('#calendarEventPopupStartAllDayChecked').text(arg.event.extendedProps.displayStartDate);
                    $('#calendarEventPopupEndAllDayChecked').text(arg.event.extendedProps.displayEndDate);
                    $('#divCalendarEventPopupAllDayChecked').show();
                } else {
                    $('#calendarEventPopupStartAllDayUnchecked').text(arg.event.extendedProps.displayStartDate);
                    $('#calendarEventPopupStartTimeZoneAllDayUnchecked').text(`(${arg.event.extendedProps.displayStartDateTimeZone})`);
                    $('#calendarEventPopupEndAllDayUnchecked').text(arg.event.extendedProps.displayEndDate);
                    $('#calendarEventPopupEndTimeZoneAllDayUnchecked').text(`(${arg.event.extendedProps.displayEndDateTimeZone})`);
                    $('#divCalendarEventPopupAllDayUnchecked').show();
                }

                popup.css({
                    top: offset.top + eventEl.outerHeight(),
                    left: offset.left + eventEl.outerWidth(),
                    display: 'block'
                }).data('event-id', arg.event.id);

                $('#editCalendarEventPopup').focus();

                $(document).on('click', function (e) {
                    if (!$(e.target).closest('#calendarEventPopup, .fc-event').length) {
                        popup.hide();
                    }
                });

                $('#closeCalendarEventPopup').off('click').on('click', function () {
                    popup.hide();
                });

                $('#editCalendarEventPopup').off('click').on('click', function () {

                    $.ajax({
                        url: '/Calendar/IsCalendarEventExists' + '?id=' + popup.data('event-id'),
                        type: 'POST',
                        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                        dataType: 'json',
                        data: null,
                        contentType: 'application/json; charset=utf-8',
                        success: function (data) {
                            if (data.result) {

                                let formEditCalendarEvent = $('#formEditCalendarEvent');

                                formEditCalendarEvent.find('input[id="editCalendarEventId"]').val(data.calendarEvent.id);
                                formEditCalendarEvent.find('input[id="editCalendarEventName"]').val(data.calendarEvent.title);
                                formEditCalendarEvent.find('input[id="editCalendarEventAllDay"]').val(data.calendarEvent.allDay);

                                $('#divEditEventAllDayChecked').hide();
                                $('#divEditEventAllDayUnchecked').hide();

                                if (data.calendarEvent.allDay === true) {
                                    $('#divEditEventAllDayChecked').show();
                                    formEditCalendarEvent.find('input[id="editCalendarEventAllDay"]').prop('checked', true);
                                    formEditCalendarEvent.find('input[id="editCalendarEventAllDayCheckedStartDate"]').val(data.calendarEvent.displayStartDate);
                                    formEditCalendarEvent.find('input[id="editCalendarEventAllDayCheckedEndDate"]').val(data.calendarEvent.displayEndDate);

                                }
                                else {
                                    $('#divEditEventAllDayUnchecked').show();

                                    formEditCalendarEvent.find('input[id="editCalendarEventAllDay"]').prop('checked', false);

                                    formEditCalendarEvent.find('input[id="editCalendarEventAllDayUncheckedStartDate"]').val(data.calendarEvent.displayStartDate.split(' ')[0]);
                                    formEditCalendarEvent.find('select[id="editCalendarEventAllDayUncheckedStartTime"]').val(data.calendarEvent.displayStartDate.split(' ')[1].substring(0, 5));
                                    formEditCalendarEvent.find('select[id="editCalendarEventAllDayUncheckedStartTimeZone"]').val(data.calendarEvent.startDateTimeZoneIanaId);

                                    formEditCalendarEvent.find('input[id="editCalendarEventAllDayUncheckedEndDate"]').val(data.calendarEvent.displayEndDate.split(' ')[0]);
                                    formEditCalendarEvent.find('select[id="editCalendarEventAllDayUncheckedEndTime"]').val(data.calendarEvent.displayEndDate.split(' ')[1].substring(0, 5));
                                    formEditCalendarEvent.find('select[id="editCalendarEventAllDayUncheckedEndTimeZone"]').val(data.calendarEvent.endDateTimeZoneIanaId);
                                }

                                formEditCalendarEvent.find('input[id="editCalendarEventLocation"]').val(data.calendarEvent.location);

                                const parser = new DOMParser();
                                const htmlDoc = parser.parseFromString(data.calendarEvent.description, 'text/html');

                                const imgTags = htmlDoc.querySelectorAll('img[data-file]');

                                imgTags.forEach(function (imgTag) {
                                    const base64Data = imgTag.getAttribute('data-file');
                                    const contentType = imgTag.getAttribute('data-contenttype');

                                    if (base64Data && contentType) {
                                        const blob = base64ToBlob(base64Data, contentType);
                                        const imgURL = URL.createObjectURL(blob);

                                        imgTag.src = imgURL;

                                        imgTag.onload = function () {
                                            URL.revokeObjectURL(imgURL);
                                        };
                                        imgTag.onerror = function () {
                                            URL.revokeObjectURL(imgURL);
                                        };

                                        imgTag.removeAttribute('data-file');
                                        imgTag.removeAttribute('data-contenttype');
                                    }
                                });

                                const updatedHtml = htmlDoc.body.innerHTML;

                                formEditCalendarEvent.find('textarea[id="editCalendarEventDescription"]').summernote('code', updatedHtml);

                                $('#divEditCalendarEventAttachedFile').hide();

                                if (data.calendarEvent.calendarEventAttachedFile !== null) {
                                    $('#divEditCalendarEventAttachedFile').show();

                                    $("#aEditCalendarEventAttachedFile").attr("href", '#');
                                    $("#aEditCalendarEventAttachedFile").attr("data-file", data.calendarEvent.calendarEventAttachedFileBase64Data);
                                    $("#aEditCalendarEventAttachedFile").attr("data-contenttype", data.calendarEvent.calendarEventAttachedFileContentType);
                                    $("#aEditCalendarEventAttachedFile").attr("data-name",`${data.calendarEvent.calendarEventAttachedFile.name}${data.calendarEvent.calendarEventAttachedFile.extension}`);

                                    $("#aEditCalendarEventAttachedFile").text(`${data.calendarEvent.calendarEventAttachedFile.name}${data.calendarEvent.calendarEventAttachedFile.extension}`);

                                    $("#spanEditCalendarEventAttachedFile").text(`${Math.round(data.calendarEvent.calendarEventAttachedFile.size / 1024).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ",")}KB`);
                                }

                                $.ajax({
                                    url: '/Calendar/GetCalendars',
                                    method: 'POST',
                                    headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                                    dataType: 'json',
                                    data: null,
                                    contentType: 'application/json; charset=utf-8',
                                    async: false,
                                    success: function (response) {
                                        if (response.result) {
                                            $('#editCalendarEventMyCalendar').empty();

                                            $.each(response.calendars, function (index, calendar) {
                                                $('#editCalendarEventMyCalendar').append($('<option>', {
                                                    value: calendar.id,
                                                    text: calendar.name
                                                }));
                                            });

                                            $('#editCalendarEventMyCalendar').val(data.calendarEvent.calendarId);
                                        }
                                    }
                                });

                                $('#editCalendarEventStatus').val(data.calendarEvent.status);

                                $('#divEditEventNotificationAllDayChecked').hide();
                                $('#divEditEventNotificationAllDayUnchecked').hide();

                                if (data.calendarEvent.allDay === true) {
                                    $("#divEditEventNotificationAllDayChecked .divEditEventNotificationAllDayCheckedRow").remove();
                                    $('#divEditEventNotificationAllDayChecked').show();

                                    if (data.calendarEvent.serializedCalendarReminders !== '[]') {
                                        const timeIntervals = Array.from({ length: 96 }, (_, i) => {
                                            const hours = String(Math.floor(i / 4)).padStart(2, '0');
                                            const minutes = String((i % 4) * 15).padStart(2, '0');
                                            return `${hours}:${minutes}`;
                                        });

                                        JSON.parse(data.calendarEvent.serializedCalendarReminders).forEach(reminder => {

                                            const selectedMethodOption = `
                                            <option value='Email' ${reminder.Method === 'Email' ? 'selected' : ''}>${localizer.Email}</option>
                                            <option value='Notification' ${reminder.Method === 'Notification' ? 'selected' : ''}>${localizer.Notification}</option>
                                        `;

                                            let value = reminder.DaysBeforeEvent !== null ? reminder.DaysBeforeEvent : reminder.WeeksBeforeEvent;
                                            let max = reminder.DaysBeforeEvent !== null ? 28 : 4;
                                            let selectedDays = reminder.DaysBeforeEvent !== null ? 'selected' : '';
                                            let selectedWeeks = reminder.WeeksBeforeEvent !== null ? 'selected' : '';

                                            const selNotificationTimeTypeAllDayChecked = `
                                        <input type='number' class='form-control-sm editCalendarEventSelNotificationNumberAllDayChecked' style='width:20%;text-overflow:ellipsis; background-color:#343a40; color:#fff; border-color:#6c757d;' min="0" max="${max}" value="${value}">
                                        <select class='form-control-sm editCalendarEventSelNotificationTimeTypeAllDayChecked' style='width:15%;text-overflow:ellipsis;'>
                                            <option value='Days' ${selectedDays}>${localizer.Days}</option>
                                            <option value='Weeks' ${selectedWeeks}>${localizer.Weeks}</option>
                                        </select>`;

                                            const htmlString = String.raw`
                                        <div class="divEditEventNotificationAllDayCheckedRow">
                                            <select class='form-control-sm editCalendarEventSelNotificationMethodAllDayChecked' style='width:20%;text-overflow:ellipsis;'>
                                                ${selectedMethodOption}
                                            </select>

                                            ${selNotificationTimeTypeAllDayChecked}

                                            <label style="padding-left:5px;padding-right:5px;">${localizer.BeforeAt}</label>
                                            <select class='form-control-sm editCalendarEventSelNotificationTimeAllDayChecked' style='width:11%;text-overflow:ellipsis;'>
                                                ${timeIntervals.map(time => time === `${reminder.TimesBeforeEvent.substring(0, 5)}` ? `<option value="${time}" selected>${time}</option>` : `<option value="${time}">${time}</option>`).join('')}
                                            </select>
                                            <a class='hover aEditCalendarDeleteNotificationAllDayChecked' href='#' style='width:10%;'>
                                                <i class='fa fa-trash' aria-hidden='true' style='margin-left: 7px;'></i>
                                            </a>

                                            <br />
                                            <div class="error-message" style="color: red; font-size: 0.9em; display: none;"></div>
                                        </div>
                                    `;

                                            $(htmlString).insertBefore('#editCalendarEventNotificationAllDayChecked');
                                        });
                                    }
                                }
                                else {
                                    $("#divEditEventNotificationAllDayUnchecked .divEditEventNotificationAllDayUncheckedRow").remove();
                                    $('#divEditEventNotificationAllDayUnchecked').show();

                                    if (data.calendarEvent.serializedCalendarReminders !== '[]') {

                                        JSON.parse(data.calendarEvent.serializedCalendarReminders).forEach(reminder => {

                                            const selectedMethodOption = `
                                            <option value='Email' ${reminder.Method === 'Email' ? 'selected' : ''}>${localizer.Email}</option>
                                            <option value='Notification' ${reminder.Method === 'Notification' ? 'selected' : ''}>${localizer.Notification}</option>
                                        `;

                                            let value = reminder.MinutesBeforeEvent !== null ? reminder.MinutesBeforeEvent : reminder.HoursBeforeEvent !== null ? reminder.HoursBeforeEvent : reminder.DaysBeforeEvent !== null ? reminder.DaysBeforeEvent : reminder.WeeksBeforeEvent;
                                            let max = reminder.MinutesBeforeEvent !== null ? 40320 : reminder.HoursBeforeEvent !== null ? 672 : reminder.DaysBeforeEvent !== null ? 28 : 4;

                                            let selectedMinutes = reminder.MinutesBeforeEvent !== null ? 'selected' : '';
                                            let selectedHours = reminder.HoursBeforeEvent !== null ? 'selected' : '';
                                            let selectedDays = reminder.DaysBeforeEvent !== null ? 'selected' : '';
                                            let selectedWeeks = reminder.WeeksBeforeEvent !== null ? 'selected' : '';

                                            const selNotificationTimeTypeAllDayUnchecked = `
                                        <input type='number' class='form-control-sm editCalendarEventSelNotificationNumberAllDayUnchecked' style='width:20%;text-overflow:ellipsis; background-color:#343a40; color:#fff; border-color:#6c757d;' min="0" max="${max}" value="${value}">
                                        <select class='form-control-sm editCalendarEventSelNotificationTimeTypeAllDayUnchecked' style='width:15%;text-overflow:ellipsis;'>
                                            <option value='Minutes' ${selectedMinutes}>${localizer.Minutes}</option>
                                            <option value='Hours' ${selectedHours}>${localizer.Hours}</option>
                                            <option value='Days' ${selectedDays}>${localizer.Days}</option>
                                            <option value='Weeks' ${selectedWeeks}>${localizer.Weeks}</option>
                                        </select>`;

                                            const htmlString = String.raw`
                                        <div class="divEditEventNotificationAllDayUncheckedRow" style="padding-bottom:5px;">
                                            <select class='form-control-sm editCalendarEventSelNotificationMethodAllDayUnchecked' style='width:20%;text-overflow:ellipsis;'>
                                                ${selectedMethodOption}
                                            </select>

                                            ${selNotificationTimeTypeAllDayUnchecked}

                                            <a class='hover aEditCalendarDeleteNotificationAllDayUnchecked' href='#' style='width:10%;'>
                                                <i class='fa fa-trash' aria-hidden='true' style='margin-left: 7px;'></i>
                                            </a>

                                            <br />
                                            <div class="error-message" style="color: red; font-size: 0.9em; display: none;"></div>
                                        </div>
                                    `;

                                            $(htmlString).insertBefore('#editCalendarEventNotificationAllDayUnchecked');
                                        });
                                    }
                                }

                                $('#editCalendarEventTaskDialogModal').modal({
                                    keyboard: false,
                                    backdrop: "static"
                                });

                                $('#editCalendarEventTaskDialogModal').modal('toggle');
                                $('#editCalendarEventTaskDialogModal').modal('show');
                            }
                            else {
                                toastr.error(data.error);
                            }
                        }
                    });

                    popup.hide();
                });

                $('#deleteCalendarEventPopup').off('click').on('click', function () {
                    if (confirm(`${localizer.ConfirmDelete}`)) {
                        $.ajax({
                            url: '/Calendar/DeleteCalendarEvent?id=' + popup.data('event-id'),
                            type: 'POST',
                            headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                            dataType: 'json',
                            data: null,
                            contentType: 'application/json; charset=utf-8',
                            success: function (data) {
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
            } else if (arg.event.extendedProps.calendarType === 'Other') {
                let eventEl = $(arg.el);
                let offset = eventEl.offset();
                let popup = $('#otherCalendarEventPopup');
                let currentEventId = popup.data('event-id');

                if (popup.is(':visible') && currentEventId === arg.event.id) {
                    popup.hide();
                    return;
                }

                $('#otherCalendarEventPopupTitle').text(arg.event.title);
                $('#divOtherCalendarEventPopupAllDayChecked').hide();
                $('#divOtherCalendarEventPopupAllDayUnchecked').hide();

                if (arg.event.allDay === true) {
                    $('#otherCalendarEventPopupStartAllDayChecked').text(arg.event.extendedProps.displayStartDate);
                    $('#otherCalendarEventPopupEndAllDayChecked').text(arg.event.extendedProps.displayEndDate);
                    $('#divOtherCalendarEventPopupAllDayChecked').show();
                } else {
                    $('#otherCalendarEventPopupStartAllDayUnchecked').text(arg.event.extendedProps.displayStartDate);
                    $('#otherCalendarEventPopupStartTimeZoneAllDayUnchecked').text(`(${arg.event.extendedProps.displayStartDateTimeZone})`);
                    $('#otherCalendarEventPopupEndAllDayUnchecked').text(arg.event.extendedProps.displayEndDate);
                    $('#otherCalendarEventPopupEndTimeZoneAllDayUnchecked').text(`(${arg.event.extendedProps.displayEndDateTimeZone})`);
                    $('#divOtherCalendarEventPopupAllDayUnchecked').show();
                }

                popup.css({
                    top: offset.top + eventEl.outerHeight(),
                    left: offset.left + eventEl.outerWidth(),
                    display: 'block'
                }).data('event-id', arg.event.id);

                $('#viewOtherCalendarEventPopup').focus();

                $(document).on('click', function (e) {
                    if (!$(e.target).closest('#otherCalendarEventPopup, .fc-event').length) {
                        popup.hide();
                    }
                });

                $('#closeOtherCalendarEventPopup').off('click').on('click', function () {
                    popup.hide();
                });

                $('#viewOtherCalendarEventPopup').off('click').on('click', function () {
                    $.ajax({
                        url: '/Calendar/IsOtherCalendarEventExists' + '?id=' + popup.data('event-id'),
                        type: 'POST',
                        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                        dataType: 'json',
                        data: null,
                        contentType: 'application/json; charset=utf-8',
                        success: function (data) {
                            if (data.result) {

                                let formViewCalendarEvent = $('#formViewCalendarEvent');

                                formViewCalendarEvent.find('input[id="viewCalendarEventId"]').val(data.calendarEvent.id);
                                formViewCalendarEvent.find('input[id="viewCalendarEventName"]').val(data.calendarEvent.title);
                                formViewCalendarEvent.find('input[id="viewCalendarEventAllDay"]').val(data.calendarEvent.allDay);

                                $('#divViewEventAllDayChecked').hide();
                                $('#divViewEventAllDayUnchecked').hide();

                                if (data.calendarEvent.allDay === true) {
                                    $('#divViewEventAllDayChecked').show();
                                    formViewCalendarEvent.find('input[id="viewCalendarEventAllDay"]').prop('checked', true);
                                    formViewCalendarEvent.find('input[id="viewCalendarEventAllDayCheckedStartDate"]').val(data.calendarEvent.displayStartDate);
                                    formViewCalendarEvent.find('input[id="viewCalendarEventAllDayCheckedEndDate"]').val(data.calendarEvent.displayEndDate);

                                }
                                else {
                                    $('#divViewEventAllDayUnchecked').show();

                                    formViewCalendarEvent.find('input[id="viewCalendarEventAllDay"]').prop('checked', false);

                                    formViewCalendarEvent.find('input[id="viewCalendarEventAllDayUncheckedStartDate"]').val(data.calendarEvent.displayStartDate.split(' ')[0]);
                                    formViewCalendarEvent.find('select[id="viewCalendarEventAllDayUncheckedStartTime"]').val(data.calendarEvent.displayStartDate.split(' ')[1].substring(0, 5));
                                    formViewCalendarEvent.find('select[id="viewCalendarEventAllDayUncheckedStartTimeZone"]').val(data.calendarEvent.startDateTimeZoneIanaId);

                                    formViewCalendarEvent.find('input[id="viewCalendarEventAllDayUncheckedEndDate"]').val(data.calendarEvent.displayEndDate.split(' ')[0]);
                                    formViewCalendarEvent.find('select[id="viewCalendarEventAllDayUncheckedEndTime"]').val(data.calendarEvent.displayEndDate.split(' ')[1].substring(0, 5));
                                    formViewCalendarEvent.find('select[id="viewCalendarEventAllDayUncheckedEndTimeZone"]').val(data.calendarEvent.endDateTimeZoneIanaId);
                                }

                                formViewCalendarEvent.find('input[id="viewCalendarEventLocation"]').val(data.calendarEvent.location);

                                const parser = new DOMParser();
                                const htmlDoc = parser.parseFromString(data.calendarEvent.description, 'text/html');

                                const imgTags = htmlDoc.querySelectorAll('img[data-file]');

                                imgTags.forEach(function (imgTag) {
                                    const base64Data = imgTag.getAttribute('data-file');
                                    const contentType = imgTag.getAttribute('data-contenttype');

                                    if (base64Data && contentType) {
                                        const blob = base64ToBlob(base64Data, contentType);
                                        const imgURL = URL.createObjectURL(blob);

                                        imgTag.src = imgURL;

                                        imgTag.onload = function () {
                                            URL.revokeObjectURL(imgURL);
                                        };
                                        imgTag.onerror = function () {
                                            URL.revokeObjectURL(imgURL);
                                        };

                                        imgTag.removeAttribute('data-file');
                                        imgTag.removeAttribute('data-contenttype');
                                    }
                                });

                                const updatedHtml = htmlDoc.body.innerHTML;

                                formViewCalendarEvent.find('textarea[id="viewCalendarEventDescription"]').summernote('code', updatedHtml);
                                formViewCalendarEvent.find('textarea[id="viewCalendarEventDescription"]').summernote('disable');

                                $('#divViewCalendarEventAttachedFile').hide();

                                if (data.calendarEvent.calendarEventAttachedFile !== null) {
                                    $('#divViewCalendarEventAttachedFile').show();

                                    $("#aViewCalendarEventAttachedFile").attr("href", '#');
                                    $("#aViewCalendarEventAttachedFile").attr("data-file", data.calendarEvent.calendarEventAttachedFileBase64Data);
                                    $("#aViewCalendarEventAttachedFile").attr("data-contenttype", data.calendarEvent.calendarEventAttachedFileContentType);
                                    $("#aViewCalendarEventAttachedFile").attr("data-name", `${data.calendarEvent.calendarEventAttachedFile.name}${data.calendarEvent.calendarEventAttachedFile.extension}`);

                                    $("#aViewCalendarEventAttachedFile").text(`${data.calendarEvent.calendarEventAttachedFile.name}${data.calendarEvent.calendarEventAttachedFile.extension}`);

                                    $("#spanViewCalendarEventAttachedFile").text(`${Math.round(data.calendarEvent.calendarEventAttachedFile.size / 1024).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ",")}KB`);
                                }


                                $.ajax({
                                    url: '/Calendar/GetOtherCalendars',
                                    method: 'POST',
                                    headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                                    dataType: 'json',
                                    data: null,
                                    contentType: 'application/json; charset=utf-8',
                                    async: false,
                                    success: function (response) {
                                        if (response.result) {
                                            $('#viewCalendarEventMyCalendar').empty();

                                            $.each(response.tempOtherCalendars, function (index, tempOtherCalendar) {
                                                $('#viewCalendarEventMyCalendar').append($('<option>', {
                                                    value: tempOtherCalendar.id,
                                                    text: tempOtherCalendar.name
                                                }));
                                            });

                                            $('#viewCalendarEventMyCalendar').val(data.calendarEvent.calendarId);
                                        }
                                    }
                                });

                                $('#viewCalendarEventStatus').val(data.calendarEvent.status);

                                $('#divViewEventNotificationAllDayChecked').hide();
                                $('#divViewEventNotificationAllDayUnchecked').hide();

                                if (data.calendarEvent.allDay === true) {
                                    $("#divViewEventNotificationAllDayChecked .divViewEventNotificationAllDayCheckedRow").remove();
                                    $('#divViewEventNotificationAllDayChecked').show();

                                    if (data.calendarEvent.serializedCalendarReminders !== '[]') {
                                        const timeIntervals = Array.from({ length: 96 }, (_, i) => {
                                            const hours = String(Math.floor(i / 4)).padStart(2, '0');
                                            const minutes = String((i % 4) * 15).padStart(2, '0');
                                            return `${hours}:${minutes}`;
                                        });

                                        JSON.parse(data.calendarEvent.serializedCalendarReminders).forEach(reminder => {

                                            const selectedMethodOption = `
                                                <option value='Email' ${reminder.Method === 'Email' ? 'selected' : ''}>${localizer.Email}</option>
                                                <option value='Notification' ${reminder.Method === 'Notification' ? 'selected' : ''}>${localizer.Notification}</option>
                                            `;

                                            let value = reminder.DaysBeforeEvent !== null ? reminder.DaysBeforeEvent : reminder.WeeksBeforeEvent;
                                            let max = reminder.DaysBeforeEvent !== null ? 28 : 4;
                                            let selectedDays = reminder.DaysBeforeEvent !== null ? 'selected' : '';
                                            let selectedWeeks = reminder.WeeksBeforeEvent !== null ? 'selected' : '';

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
                                                        ${timeIntervals.map(time => time === `${reminder.TimesBeforeEvent.substring(0, 5)}` ? `<option value="${time}" selected>${time}</option>` : `<option value="${time}">${time}</option>`).join('')}
                                                    </select>

                                                    <br />
                                                    <div class="error-message" style="color: red; font-size: 0.9em; display: none;"></div>
                                                </div>
                                            `;

                                            $(htmlString).insertBefore('#viewCalendarEventNotificationAllDayChecked');
                                        });
                                    }
                                }
                                else {
                                    $("#divViewEventNotificationAllDayUnchecked .divViewEventNotificationAllDayUncheckedRow").remove();
                                    $('#divViewEventNotificationAllDayUnchecked').show();

                                    if (data.calendarEvent.serializedCalendarReminders !== '[]') {

                                        JSON.parse(data.calendarEvent.serializedCalendarReminders).forEach(reminder => {

                                            const selectedMethodOption = `
                                                <option value='Email' ${reminder.Method === 'Email' ? 'selected' : ''}>${localizer.Email}</option>
                                                <option value='Notification' ${reminder.Method === 'Notification' ? 'selected' : ''}>${localizer.Notification}</option>
                                            `;

                                            let value = reminder.MinutesBeforeEvent !== null ? reminder.MinutesBeforeEvent : reminder.HoursBeforeEvent !== null ? reminder.HoursBeforeEvent : reminder.DaysBeforeEvent !== null ? reminder.DaysBeforeEvent : reminder.WeeksBeforeEvent;
                                            let max = reminder.MinutesBeforeEvent !== null ? 40320 : reminder.HoursBeforeEvent !== null ? 672 : reminder.DaysBeforeEvent !== null ? 28 : 4;

                                            let selectedMinutes = reminder.MinutesBeforeEvent !== null ? 'selected' : '';
                                            let selectedHours = reminder.HoursBeforeEvent !== null ? 'selected' : '';
                                            let selectedDays = reminder.DaysBeforeEvent !== null ? 'selected' : '';
                                            let selectedWeeks = reminder.WeeksBeforeEvent !== null ? 'selected' : '';

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

                                            $(htmlString).insertBefore('#viewCalendarEventNotificationAllDayUnchecked');
                                        });
                                    }
                                }

                                $('#viewCalendarEventTaskDialogModal').modal({
                                    keyboard: false,
                                    backdrop: "static"
                                });

                                $('#viewCalendarEventTaskDialogModal').modal('toggle');
                                $('#viewCalendarEventTaskDialogModal').modal('show');
                            }
                            else {
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

    JSON.parse($('#calendarEventOutputViewModels').val()).forEach(item => {
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
                calendarType: 'My'
            }
        });
    });

    JSON.parse($('#otherCalendarEventOutputViewModels').val()).forEach(item => {
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
                calendarType: 'Other'
            }
        });
    });

    calendar.render();

    function RefreshCalendarEvents() {
        calendar.removeAllEvents();

        let paramValue = {
            Calendars: []
        };

        $('#myCalendars input[type="checkbox"]:checked').each(function () {
            let calendarsArray = [
                { Id: Number($(this).closest('label').attr('id').replace('lblCalendar', '')) }
            ];

            for (let i = 0; i < calendarsArray.length; i++) {
                paramValue.Calendars.push(calendarsArray[i]);
            }
        });

        $('#otherCalendars input[type="checkbox"]:checked').each(function () {
            let calendarsArray = [
                { Id: Number($(this).closest('label').attr('id').replace('lblOtherCalendar', '')) }
            ];

            for (let i = 0; i < calendarsArray.length; i++) {
                paramValue.Calendars.push(calendarsArray[i]);
            }
        });

        $.ajax({
            url: '/Calendar/GetCalendarEvents',
            method: 'POST',
            headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
            dataType: 'json',
            data: JSON.stringify(paramValue),
            contentType: 'application/json; charset=utf-8',
            success: function (response) {
                if (response.result) {
                    JSON.parse(response.calendarEvents).forEach(item => {
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

    function ValidateCreateEventInputAllDayChecked(row) {
        let numberInput = row.find('.createCalendarEventSelNotificationNumberAllDayChecked');
        let value = numberInput.val().trim();
        let timeType = row.find('.createCalendarEventSelNotificationTimeTypeAllDayChecked').val();
        let isValid = true;
        let errorMessage = '';

        if (value === '') {
            isValid = false;
            errorMessage = `${localizer.ThisFieldRequired}`;
        } else if (isNaN(value)) {
            isValid = false;
            errorMessage = `${localizer.ErrorInvalidNumber}`;
        } else {
            value = parseInt(value, 10);
            if (timeType === 'Days') {
                if (value < 0 || value > 28) {
                    isValid = false;
                    errorMessage = `${localizer.ErrorRangeDay}`;
                }
            }
            else if (timeType === 'Weeks') {
                if (value < 0 || value > 4) {
                    isValid = false;
                    errorMessage = `${localizer.ErrorRangeWeek}`;
                }
            }
        }

        if (!isValid) {
            numberInput.css('text-decoration', 'underline');
            numberInput.css('text-decoration-color', 'red');
        } else {
            numberInput.css('text-decoration', 'none');
        }

        row.find('.error-message').text(errorMessage).toggle(!isValid);
        return isValid;
    }
    function ValidateCreateEventInputAllDayUnchecked(row) {
        let numberInput = row.find('.createCalendarEventSelNotificationNumberAllDayUnchecked');
        let value = numberInput.val().trim();
        let timeType = row.find('.createCalendarEventSelNotificationTimeTypeAllDayUnchecked').val();
        let isValid = true;
        let errorMessage = '';

        if (value === '') {
            isValid = false;
            errorMessage = `${localizer.ThisFieldRequired}`;
        } else if (isNaN(value)) {
            isValid = false;
            errorMessage = `${localizer.ErrorInvalidNumber}`;
        } else {
            value = parseInt(value, 10);
            if (timeType === 'Minutes') {
                if (value < 0 || value > 40320) {
                    isValid = false;
                    errorMessage = `${localizer.ErrorRangeMinute}`;
                }
            }
            else if (timeType === 'Hours') {
                if (value < 0 || value > 672) {
                    isValid = false;
                    errorMessage = `${localizer.ErrorRangeHour}`;
                }
            }
            else if (timeType === 'Days') {
                if (value < 0 || value > 28) {
                    isValid = false;
                    errorMessage = `${localizer.ErrorRangeDay}`;
                }
            }
            else if (timeType === 'Weeks') {
                if (value < 0 || value > 4) {
                    isValid = false;
                    errorMessage = `${localizer.ErrorRangeWeek}`;
                }
            }
        }

        if (!isValid) {
            numberInput.css('text-decoration', 'underline');
            numberInput.css('text-decoration-color', 'red');
        } else {
            numberInput.css('text-decoration', 'none');
        }

        row.find('.error-message').text(errorMessage).toggle(!isValid);
        return isValid;
    }

    function ValidateEditEventInputAllDayUnchecked(row) {
        let numberInput = row.find('.editCalendarEventSelNotificationNumberAllDayUnchecked');
        let value = numberInput.val().trim();
        let timeType = row.find('.editCalendarEventSelNotificationTimeTypeAllDayUnchecked').val();
        let isValid = true;
        let errorMessage = '';

        if (value === '') {
            isValid = false;
            errorMessage = `${localizer.ThisFieldRequired}`;
        } else if (isNaN(value)) {
            isValid = false;
            errorMessage = `${localizer.ErrorInvalidNumber}`;
        } else {
            value = parseInt(value, 10);
            if (timeType === 'Minutes') {
                if (value < 0 || value > 40320) {
                    isValid = false;
                    errorMessage = `${localizer.ErrorRangeMinute}`;
                }
            }
            else if (timeType === 'Hours') {
                if (value < 0 || value > 672) {
                    isValid = false;
                    errorMessage = `${localizer.ErrorRangeHour}`;
                }
            }
            else if (timeType === 'Days') {
                if (value < 0 || value > 28) {
                    isValid = false;
                    errorMessage = `${localizer.ErrorRangeDay}`;
                }
            }
            else if (timeType === 'Weeks') {
                if (value < 0 || value > 4) {
                    isValid = false;
                    errorMessage = `${localizer.ErrorRangeWeek}`;
                }
            }
        }

        if (!isValid) {
            numberInput.css('text-decoration', 'underline');
            numberInput.css('text-decoration-color', 'red');
        } else {
            numberInput.css('text-decoration', 'none');
        }

        row.find('.error-message').text(errorMessage).toggle(!isValid);
        return isValid;
    }

    function ValidateEditEventInputAllDayChecked(row) {
        let numberInput = row.find('.editCalendarEventSelNotificationNumberAllDayChecked');
        let value = numberInput.val().trim();
        let timeType = row.find('.editCalendarEventSelNotificationTimeTypeAllDayChecked').val();
        let isValid = true;
        let errorMessage = '';

        if (value === '') {
            isValid = false;
            errorMessage = `${localizer.ThisFieldRequired}`;
        } else if (isNaN(value)) {
            isValid = false;
            errorMessage = `${localizer.ErrorInvalidNumber}`;
        } else {
            value = parseInt(value, 10);
            if (timeType === 'Days') {
                if (value < 0 || value > 28) {
                    isValid = false;
                    errorMessage = `${localizer.ErrorRangeDay}`;
                }
            }
            else if (timeType === 'Weeks') {
                if (value < 0 || value > 4) {
                    isValid = false;
                    errorMessage = `${localizer.ErrorRangeWeek}`;
                }
            }
        }

        if (!isValid) {
            numberInput.css('text-decoration', 'underline');
            numberInput.css('text-decoration-color', 'red');
        } else {
            numberInput.css('text-decoration', 'none');
        }

        row.find('.error-message').text(errorMessage).toggle(!isValid);
        return isValid;
    }

    $(document).on('change', '.chkCalendar', function () {
        RefreshCalendarEvents();
    });

    $(document).on('change', '.chkOtherCalendar', function () {
        RefreshCalendarEvents();
    });

    $(document).on('change', '.createCalendarEventSelNotificationTimeTypeAllDayChecked', function () {
        let row = $(this).closest('.divCreateEventNotificationAllDayCheckedRow');
        ValidateCreateEventInputAllDayChecked(row);
        row.find('.error-message').hide();
    });

    $(document).on('input', '.createCalendarEventSelNotificationNumberAllDayChecked', function () {
        let row = $(this).closest('.divCreateEventNotificationAllDayCheckedRow');
        ValidateCreateEventInputAllDayChecked(row);
        row.find('.error-message').hide();
    });

    $(document).on('change', '.createCalendarEventSelNotificationTimeTypeAllDayUnchecked', function () {
        let row = $(this).closest('.divCreateEventNotificationAllDayUncheckedRow');
        ValidateCreateEventInputAllDayUnchecked(row);
        row.find('.error-message').hide();
    });

    $(document).on('input', '.createCalendarEventSelNotificationNumberAllDayUnchecked', function () {
        let row = $(this).closest('.divCreateEventNotificationAllDayUncheckedRow');
        ValidateCreateEventInputAllDayUnchecked(row);
        row.find('.error-message').hide();
    });

    $(document).on('change', '.editCalendarEventSelNotificationTimeTypeAllDayChecked', function () {
        let row = $(this).closest('.divEditEventNotificationAllDayCheckedRow');
        ValidateEditEventInputAllDayChecked(row);
        row.find('.error-message').hide();
    });

    $(document).on('input', '.editCalendarEventSelNotificationNumberAllDayChecked', function () {
        let row = $(this).closest('.divEditEventNotificationAllDayCheckedRow');
        ValidateEditEventInputAllDayChecked(row);
        row.find('.error-message').hide();
    });

    $(document).on('change', '.editCalendarEventSelNotificationTimeTypeAllDayUnchecked', function () {
        let row = $(this).closest('.divEditEventNotificationAllDayUncheckedRow');
        ValidateEditEventInputAllDayUnchecked(row);
        row.find('.error-message').hide();
    });

    $(document).on('input', '.editCalendarEventSelNotificationNumberAllDayUnchecked', function () {
        let row = $(this).closest('.divEditEventNotificationAllDayUncheckedRow');
        ValidateEditEventInputAllDayUnchecked(row);
        row.find('.error-message').hide();
    });

    $('#formCreateCalendarEvent').off('submit').on('submit', function (event) {
        event.preventDefault();

        let isValidForm = true;

        if ($('#createCalendarEventAllDay').is(':checked')) {
            $('.divCreateEventNotificationAllDayCheckedRow').each(function () {
                let isValidRow = ValidateCreateEventInputAllDayChecked($(this));
                isValidForm = isValidForm && isValidRow;
            });
        }
        else {
            $('.divCreateEventNotificationAllDayUncheckedRow').each(function () {
                let isValidRow = ValidateCreateEventInputAllDayUnchecked($(this));
                isValidForm = isValidForm && isValidRow;
            });
        }

        if (!$('#formCreateCalendarEvent').valid() || !isValidForm) {
            return false;
        }

        let calendarEventName = $('#createCalendarEventName').val();
        let allDay = $('#createCalendarEventAllDay').is(':checked');

        let startDate;
        let endDate;

        if ($('#createCalendarEventAllDay').is(':checked')) {
            startDate = $('#createCalendarEventAllDayCheckedStartDate').val();
            endDate = $('#createCalendarEventAllDayCheckedEndDate').val();
        }
        else {
            startDate = $('#createCalendarEventAllDayUncheckedStartDate').val() + ' ' + $('#createCalendarEventAllDayUncheckedStartTime').val();
            endDate = $('#createCalendarEventAllDayUncheckedEndDate').val() + ' ' + $('#createCalendarEventAllDayUncheckedEndTime').val();
        }

        let startDateTimeZoneIanaId;
        let endDateTimeZoneIanaId;

        if ($('#createCalendarEventAllDay').is(':checked')) {
            startDateTimeZoneIanaId = '';
            endDateTimeZoneIanaId = '';
        }
        else {
            startDateTimeZoneIanaId = $('#createCalendarEventAllDayUncheckedStartTimeZone').val();
            endDateTimeZoneIanaId = $('#createCalendarEventAllDayUncheckedEndTimeZone').val();
        }

        let location = $('#createCalendarEventLocation').val();
        let description = $('#createCalendarEventDescription').val();

        let calendarId = $('#createCalendarEventMyCalendar').val();
        let status = $('#createCalendarEventStatus').val();

        let calendarReminders = [];

        if ($('#createCalendarEventAllDay').is(':checked')) {
            $('.divCreateEventNotificationAllDayCheckedRow').each(function (index, value) {
                let method = $(this).find('.createCalendarEventSelNotificationMethodAllDayChecked').val();
                let number = $(this).find('.createCalendarEventSelNotificationNumberAllDayChecked').val();
                let timeType = $(this).find('.createCalendarEventSelNotificationTimeTypeAllDayChecked').val();
                let time = $(this).find('.createCalendarEventSelNotificationTimeAllDayChecked').val();

                if (timeType === "Days") {
                    calendarReminders.push({
                        Method: method,
                        MinutesBeforeEvent: null,
                        HoursBeforeEvent: null,
                        DaysBeforeEvent: number,
                        WeeksBeforeEvent: null,
                        TimesBeforeEvent: time 
                    });
                }
                else if (timeType === "Weeks") {
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
        }
        else {
            $('.divCreateEventNotificationAllDayUncheckedRow').each(function (index, value) {
                let method = $(this).find('.createCalendarEventSelNotificationMethodAllDayUnchecked').val();
                let number = $(this).find('.createCalendarEventSelNotificationNumberAllDayUnchecked').val();
                let timeType = $(this).find('.createCalendarEventSelNotificationTimeTypeAllDayUnchecked').val();

                if (timeType === "Minutes") {
                    calendarReminders.push({
                        Method: method,
                        MinutesBeforeEvent: number,
                        HoursBeforeEvent: null,
                        DaysBeforeEvent: null,
                        WeeksBeforeEvent: null,
                        TimesBeforeEvent: null
                    });
                }
                else if (timeType === "Hours") {
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
        formData.append('Title', calendarEventName);
        formData.append('AllDay', allDay);

        formData.append('StartDate', startDate);
        formData.append('EndDate', endDate);

        formData.append('StartDateTimeZoneIanaId', startDateTimeZoneIanaId);
        formData.append('EndDateTimeZoneIanaId', endDateTimeZoneIanaId);

        formData.append('Location', location);
        formData.append('Description', description);

        formData.append('CalendarEventUploadedFile', createCalendarEventUploadedFile);

        formData.append('CalendarId', calendarId);
        formData.append('Status', status);

        formData.append('SerializedCalendarReminders', JSON.stringify(calendarReminders));

        $.ajax({
            url: '/Calendar/CreateCalendarEvent',
            type: 'POST',
            headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
            dataType: 'json',
            data: formData,
            contentType: false,
            processData: false,
            success: function (data) {
                if (data.result) {

                    calendar.removeAllEvents();

                    let paramValue = {
                        Calendars: []
                    };

                    $('#myCalendars input[type="checkbox"]:checked').each(function () {
                        let calendarsArray = [
                            { Id: Number($(this).closest('label').attr('id').replace('lblCalendar', '')) }
                        ];

                        for (let i = 0; i < calendarsArray.length; i++) {
                            paramValue.Calendars.push(calendarsArray[i]);
                        }
                    });

                    $('#otherCalendars input[type="checkbox"]:checked').each(function () {
                        let calendarsArray = [
                            { Id: Number($(this).closest('label').attr('id').replace('lblOtherCalendar', '')) }
                        ];

                        for (let i = 0; i < calendarsArray.length; i++) {
                            paramValue.Calendars.push(calendarsArray[i]);
                        }
                    });

                    $.ajax({
                        url: '/Calendar/GetCalendarEvents',
                        method: 'POST',
                        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                        dataType: 'json',
                        data: JSON.stringify(paramValue),
                        contentType: 'application/json; charset=utf-8',
                        success: function (response) {
                            if (response.result) {
                                JSON.parse(response.calendarEvents).forEach(item => {
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
                            } else {
                                toastr.error(data.error);
                            }
                        }
                    });

                    toastr.success(data.message);

                    $('#createCalendarEventTaskDialogModal').modal('hide');
                }
                else {
                    toastr.error(data.error);
                }
            }
        });

        return false;

    });

    $('#formEditCalendarEvent').off('submit').on('submit', function (event) {
        event.preventDefault();

        let isValidForm = true;

        if ($('#editCalendarEventAllDay').is(':checked')) {
            $('.divEditEventNotificationAllDayCheckedRow').each(function () {
                let isValidRow = ValidateEditEventInputAllDayChecked($(this));
                isValidForm = isValidForm && isValidRow;
            });
        }
        else {
            $('.divEditEventNotificationAllDayUncheckedRow').each(function () {
                let isValidRow = ValidateEditEventInputAllDayUnchecked($(this));
                isValidForm = isValidForm && isValidRow;
            });
        }

        if (!$('#formEditCalendarEvent').valid() || !isValidForm) {
            return false;
        }

        let calendarEventName = $('#editCalendarEventName').val();
        let allDay = $('#editCalendarEventAllDay').is(':checked');

        let startDate;
        let endDate;

        if ($('#editCalendarEventAllDay').is(':checked')) {
            startDate = $('#editCalendarEventAllDayCheckedStartDate').val();
            endDate = $('#editCalendarEventAllDayCheckedEndDate').val();
        }
        else {
            startDate = $('#editCalendarEventAllDayUncheckedStartDate').val() + ' ' + $('#editCalendarEventAllDayUncheckedStartTime').val();
            endDate = $('#editCalendarEventAllDayUncheckedEndDate').val() + ' ' + $('#editCalendarEventAllDayUncheckedEndTime').val();
        }

        let startDateTimeZoneIanaId;
        let endDateTimeZoneIanaId;

        if ($('#editCalendarEventAllDay').is(':checked')) {
            startDateTimeZoneIanaId = '';
            endDateTimeZoneIanaId = '';
        }
        else {
            startDateTimeZoneIanaId = $('#editCalendarEventAllDayUncheckedStartTimeZone').val();
            endDateTimeZoneIanaId = $('#editCalendarEventAllDayUncheckedEndTimeZone').val();
        }

        let location = $('#editCalendarEventLocation').val();
        let description = $('#editCalendarEventDescription').val();

        let id = $('#editCalendarEventId').val();
        let calendarId = $('#editCalendarEventMyCalendar').val();
        let status = $('#editCalendarEventStatus').val();

        let calendarReminders = [];

        if ($('#editCalendarEventAllDay').is(':checked')) {
            $('.divEditEventNotificationAllDayCheckedRow').each(function (index, value) {
                let method = $(this).find('.editCalendarEventSelNotificationMethodAllDayChecked').val();
                let number = $(this).find('.editCalendarEventSelNotificationNumberAllDayChecked').val();
                let timeType = $(this).find('.editCalendarEventSelNotificationTimeTypeAllDayChecked').val();
                let time = $(this).find('.editCalendarEventSelNotificationTimeAllDayChecked').val();

                if (timeType === "Days") {
                    calendarReminders.push({
                        Method: method,
                        MinutesBeforeEvent: null,
                        HoursBeforeEvent: null,
                        DaysBeforeEvent: number,
                        WeeksBeforeEvent: null,
                        TimesBeforeEvent: time
                    });
                }
                else if (timeType === "Weeks") {
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
        }
        else {
            $('.divEditEventNotificationAllDayUncheckedRow').each(function (index, value) {
                let method = $(this).find('.editCalendarEventSelNotificationMethodAllDayUnchecked').val();
                let number = $(this).find('.editCalendarEventSelNotificationNumberAllDayUnchecked').val();
                let timeType = $(this).find('.editCalendarEventSelNotificationTimeTypeAllDayUnchecked').val();

                if (timeType === "Minutes") {
                    calendarReminders.push({
                        Method: method,
                        MinutesBeforeEvent: number,
                        HoursBeforeEvent: null,
                        DaysBeforeEvent: null,
                        WeeksBeforeEvent: null,
                        TimesBeforeEvent: null
                    });
                }
                else if (timeType === "Hours") {
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
        formData.append('Id', id);
        formData.append('CalendarId', calendarId);

        formData.append('Title', calendarEventName);
        formData.append('AllDay', allDay);

        formData.append('StartDate', startDate);
        formData.append('EndDate', endDate);

        formData.append('StartDateTimeZoneIanaId', startDateTimeZoneIanaId);
        formData.append('EndDateTimeZoneIanaId', endDateTimeZoneIanaId);

        formData.append('Location', location);
        formData.append('Description', description);

        formData.append('CalendarEventUploadedFile', editCalendarEventUploadedFile);
        formData.append('Status', status);

        formData.append('SerializedCalendarReminders', JSON.stringify(calendarReminders));

        $.ajax({
            url: '/Calendar/UpdateCalendarEvent',
            type: 'POST',
            headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
            dataType: 'json',
            data: formData,
            contentType: false,
            processData: false,
            success: function (data) {
                if (data.result) {

                    calendar.removeAllEvents();

                    let paramValue = {
                        Calendars: []
                    };

                    $('#myCalendars input[type="checkbox"]:checked').each(function () {
                        let calendarsArray = [
                            { Id: Number($(this).closest('label').attr('id').replace('lblCalendar', '')) }
                        ];

                        for (let i = 0; i < calendarsArray.length; i++) {
                            paramValue.Calendars.push(calendarsArray[i]);
                        }
                    });

                    $('#otherCalendars input[type="checkbox"]:checked').each(function () {
                        let calendarsArray = [
                            { Id: Number($(this).closest('label').attr('id').replace('lblOtherCalendar', '')) }
                        ];

                        for (let i = 0; i < calendarsArray.length; i++) {
                            paramValue.Calendars.push(calendarsArray[i]);
                        }
                    });

                    $.ajax({
                        url: '/Calendar/GetCalendarEvents',
                        method: 'POST',
                        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                        dataType: 'json',
                        data: JSON.stringify(paramValue),
                        contentType: 'application/json; charset=utf-8',
                        success: function (response) {
                            if (response.result) {
                                JSON.parse(response.calendarEvents).forEach(item => {
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
                            } else {
                                toastr.error(data.error);
                            }
                        }
                    });

                    toastr.success(data.message);

                    $('#editCalendarEventTaskDialogModal').modal('hide');
                }
                else {
                    toastr.error(data.error);
                }
            }
        });

        return false;

    });

    function UpdateCalendar() {

        if (!$('#formEditCalendar').valid()) {
            return false;
        }

        let editForm = $('#formEditCalendar');

        let id = editForm.find('input[id="editCalendarId"]').val();
        let name = editForm.find('input[id="editCalendarName"]').val();
        let description = editForm.find('textarea[id="editCalendarDescription"]').val();
        let htmlColorCode = editForm.find('input[id="editCalendarHtmlColorCode"]').val();
        let timeZoneIanaId = editForm.find('select[id="editCalendarTimeZone"]').val();

        let calendarsArray = [
            { Id: id, Name: name, Description: description, HtmlColorCode: htmlColorCode, TimeZoneIanaId: timeZoneIanaId }
        ];

        let paramValue = {
            Calendars: []
        };

        for (let i = 0; i < calendarsArray.length; i++) {
            paramValue.Calendars.push(calendarsArray[i]);
        }

        $.ajax({
            url: '/Calendar/UpdateCalendar',
            type: 'POST',
            headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
            dataType: 'json',
            data: JSON.stringify(paramValue),
            contentType: 'application/json; charset=utf-8',
            success: function (data) {
                if (data.result) {
                    $('#editCalendarDialogModal').modal('hide');

                    let label = $('#lblCalendar' + data.calendar.id).find('label');
                    if (label.length > 0) {
                        label.text(data.calendar.name);
                    }

                    let checkbox = $('#lblCalendar' + data.calendar.id).find('input[type="checkbox"]');
                    if (checkbox.length > 0) {
                        checkbox.css('accent-color', data.calendar.htmlColorCode);
                    }

                    calendar.getEvents().forEach(function (event) {
                        if (event.extendedProps.calendarId === data.calendar.id) {
                            event.setProp('backgroundColor', data.calendar.htmlColorCode);
                            event.setProp('borderColor', data.calendar.htmlColorCode);
                        }
                    });

                    setTimeout(function () {
                        let calendars = $('#myCalendars label[id^="lblCalendar"]').get();

                        calendars.sort(function (a, b) {
                            let nameA = $(a).text().trim().toUpperCase();
                            let nameB = $(b).text().trim().toUpperCase();

                            if (nameA < nameB) return -1;
                            if (nameA > nameB) return 1;
                            return 0;
                        });

                        $.each(calendars, function (index, label) {
                            $('#myCalendars').append(label);
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

    $('#formEditCalendar').off('submit').on('submit', function () {
        return UpdateCalendar();
    });

    function DeleteCalendar() {
        let selectedCalendarId = $('#confirmDeleteCalendarDialogModal').data('calendar-id');

        $.ajax({
            url: '/Calendar/IsCalendarExists' + '?id=' + selectedCalendarId,
            type: 'POST',
            headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
            dataType: 'json',
            data: null,
            contentType: 'application/json; charset=utf-8',
            success: function (data) {
                if (data.result) {

                    let calendarsArray = [
                        { Id: data.calendar.id }
                    ];

                    let paramValue = {
                        Calendars: []
                    };

                    for (let i = 0; i < calendarsArray.length; i++) {
                        paramValue.Calendars.push(calendarsArray[i]);
                    }


                    $.ajax({
                        url: '/Calendar/DeleteCalendar',
                        type: 'POST',
                        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                        dataType: 'json',
                        data: JSON.stringify(paramValue),
                        contentType: 'application/json; charset=utf-8',
                        success: function (data) {
                            if (data.result) {
                                $('#confirmDeleteCalendarDialogModal').modal('hide');

                                $('#lblCalendar' + data.calendar.id).remove();

                                calendar.getEvents().forEach(function (event) {
                                    if (event.extendedProps.calendarId === data.calendar.id) {
                                        event.remove();
                                    }
                                });

                                toastr.success(data.message);
                            }
                            else {
                                toastr.error(data.error);
                            }
                        }
                    });
                }
                else {
                    toastr.error(data.error);
                }
            }
        });
    }

    $('#btnDeleteCalendar').off('click').on('click', function () {
        DeleteCalendar();
    });
});

function CreateUploadImageFile(file) {

    formData = new FormData();
    formData.append("summernoteImageFile", file);

    $.ajax({
        url: "/Calendar/UploadImageFile",
        data: formData,
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        type: 'POST',
        enctype: 'multipart/form-data',
        processData: false,
        contentType: false,
        dataType: 'json',
        cache: false,
        success: function (data) {
            if (data.result) {
                const imgURL = URL.createObjectURL(base64ToBlob(data.file.fileContents, data.file.contentType));
                const imgNode = document.createElement('img');
                imgNode.src = imgURL;
                imgNode.style.maxWidth = "170px";
                imgNode.style.maxHeight = "209px";
                imgNode.setAttribute('alt', data.filePath);

                $('#createCalendarEventDescription').summernote('insertNode', imgNode);

                imgNode.onload = function () {
                    URL.revokeObjectURL(imgURL);
                };
                imgNode.onerror = function () {
                    URL.revokeObjectURL(imgURL);
                };
            }
            else {
                alert(data.errorMessage);
            }
        }
    });
}

function EditUploadImageFile(file) {

    formData = new FormData();
    formData.append("summernoteImageFile", file);

    $.ajax({
        url: "/Calendar/UploadImageFile",
        data: formData,
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        type: 'POST',
        enctype: 'multipart/form-data',
        processData: false,
        contentType: false,
        dataType: 'json',
        cache: false,
        success: function (data) {
            if (data.result) {
                const imgURL = URL.createObjectURL(base64ToBlob(data.file.fileContents, data.file.contentType));
                const imgNode = document.createElement('img');
                imgNode.src = imgURL;
                imgNode.style.maxWidth = "170px";
                imgNode.style.maxHeight = "209px";
                imgNode.setAttribute('alt', data.filePath);

                $('#editCalendarEventDescription').summernote('insertNode', imgNode);

                imgNode.onload = function () {
                    URL.revokeObjectURL(imgURL);
                };
                imgNode.onerror = function () {
                    URL.revokeObjectURL(imgURL);
                };
            }
            else {
                alert(data.errorMessage);
            }
        }
    });
}

function CreateCalendar() {

    if (!$('#formCreateCalendar').valid()) {
        return false;
    }

    let createForm = $('#formCreateCalendar');
    let name = createForm.find('input[id="createCalendarName"]').val();
    let description = createForm.find('textarea[id="createCalendarDescription"]').val();
    let htmlColorCode = createForm.find('input[id="createCalendarHtmlColorCode"]').val();
    let timeZoneIanaId = createForm.find('select[id="createCalendarTimeZone"]').val();

    let calendarsArray = [
        { Name: name, Description: description, HtmlColorCode: htmlColorCode, TimeZoneIanaId: timeZoneIanaId }
    ];

    let paramValue = {
        Calendars: []
    };

    for (let i = 0; i < calendarsArray.length; i++) {
        paramValue.Calendars.push(calendarsArray[i]);
    }

    $.ajax({
        url: '/Calendar/CreateCalendar',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: JSON.stringify(paramValue),
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                $('#createCalendarDialogModal').modal('hide');

                $('#myCalendars').append(`<label id="lblCalendar${data.calendar.id}" style="position: relative; border:1px solid #ccc; padding:10px; margin:0 0 10px; display:block"><input type="checkbox" style="accent-color:${data.calendar.htmlColorCode}" checked /><label style="padding-left:0.5em;"> ${data.calendar.name}</label><i id="${data.calendar.id}" class="edit-calendar fas fa-edit" style="position: absolute; right: 40px; top: 50%; transform: translateY(-50%); cursor: pointer;"></i><i id="${data.calendar.id}" class="delete-calendar fas fa-trash" style="position: absolute; right: 15px; top: 50%; transform: translateY(-50%); cursor: pointer;"></i></label>`);

                let calendars = $('#myCalendars label[id^="lblCalendar"]').get();

                calendars.sort(function (a, b) {
                    let nameA = $(a).text().trim().toUpperCase();
                    let nameB = $(b).text().trim().toUpperCase();

                    if (nameA < nameB) return -1;
                    if (nameA > nameB) return 1;
                    return 0;
                });

                $.each(calendars, function (index, label) {
                    $('#myCalendars').append(label);
                });

                toastr.success(data.message);
            }
            else {
                toastr.error(data.error);
            }
        }
    });
    return false;
}

$('#formCreateCalendar').off('submit').on('submit', function () {
    return CreateCalendar();
});

$('#aCreateCalendarEvent').off('click').on('click', function () {

    $.ajax({
        url: '/Calendar/GetCalendars',
        method: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        async: false,
        success: function (response) {
            if (response.result) {
                $('#createCalendarEventMyCalendar').empty();

                $.each(response.calendars, function (index, calendar) {
                    $('#createCalendarEventMyCalendar').append($('<option>', {
                        value: calendar.id,
                        text: calendar.name
                    }));
                });
            }
        }
    });

    $('#createCalendarEventAllDayUncheckedStartTimeZone').val($('#loginedAccountTimeZoneIanaId').val());
    $('#createCalendarEventAllDayUncheckedEndTimeZone').val($('#loginedAccountTimeZoneIanaId').val());

    $('#createCalendarEventTaskDialogModal').modal('show');

    $('#createCalendarEventAllDayUncheckedStartDate').val(moment().format("YYYY-MM-DD"));
    $('#createCalendarEventAllDayCheckedStartDate').val(moment().format("YYYY-MM-DD"));

    $('#createCalendarEventAllDayUncheckedEndDate').val(moment().format("YYYY-MM-DD"));
    $('#createCalendarEventAllDayCheckedEndDate').val(moment().format("YYYY-MM-DD"));
});

$('#createCalendarEventAllDay').off('click').on('click', function () {
    if ($(this).is(':checked')) {
        $("#divCreateEventAllDayUnchecked").hide();
        $("#divCreateEventAllDayChecked").hide();
        $("#divCreateEventAllDayChecked").show();

        $("#divCreateEventNotificationAllDayUnchecked").hide();
        $("#divCreateEventNotificationAllDayChecked").hide();
        $("#divCreateEventNotificationAllDayChecked").show();
    }
    else {
        $("#divCreateEventAllDayUnchecked").hide();
        $("#divCreateEventAllDayChecked").hide();
        $("#divCreateEventAllDayUnchecked").show();

        $("#divCreateEventNotificationAllDayUnchecked").hide();
        $("#divCreateEventNotificationAllDayChecked").hide();
        $("#divCreateEventNotificationAllDayUnchecked").show();
    }
});

$('#editCalendarEventAllDay').off('click').on('click', function () {
    if ($(this).is(':checked')) {
        $("#divEditEventAllDayUnchecked").hide();
        $("#divEditEventAllDayChecked").hide();
        $("#divEditEventAllDayChecked").show();

        $("#divEditEventNotificationAllDayUnchecked").hide();
        $("#divEditEventNotificationAllDayChecked").hide();
        $("#divEditEventNotificationAllDayChecked").show();
    }
    else {
        $("#divEditEventAllDayUnchecked").hide();
        $("#divEditEventAllDayChecked").hide();
        $("#divEditEventAllDayUnchecked").show();

        $("#divEditEventNotificationAllDayUnchecked").hide();
        $("#divEditEventNotificationAllDayChecked").hide();
        $("#divEditEventNotificationAllDayUnchecked").show();
    }
});

$('#createCalendarEventNotificationAllDayChecked').off('click').on('click', function () {
    const timeIntervals = Array.from({ length: 96 }, (_, i) => {
        const hours = String(Math.floor(i / 4)).padStart(2, '0');
        const minutes = String((i % 4) * 15).padStart(2, '0');
        return `${hours}:${minutes}`;
    });

    const htmlString = String.raw`
    <div class="divCreateEventNotificationAllDayCheckedRow">
        <select class='form-control-sm createCalendarEventSelNotificationMethodAllDayChecked' style='width:20%;text-overflow:ellipsis;'>
            <option value='Email'>${localizer.Email}</option>
            <option value='Notification'>${localizer.Notification}</option>
        </select>

        <input type='number' class='form-control-sm createCalendarEventSelNotificationNumberAllDayChecked' style='width:20%;text-overflow:ellipsis; background-color:#343a40; color:#fff; border-color:#6c757d;' min="0" max="28">

        <select class='form-control-sm createCalendarEventSelNotificationTimeTypeAllDayChecked' style='width:15%;text-overflow:ellipsis;'>
            <option value='Days' selected>${localizer.Days}</option>
            <option value='Weeks'>${localizer.Weeks}</option>
        </select>
        <label style="padding-left:5px;padding-right:5px;">${localizer.BeforeAt}</label>
        <select class='form-control-sm createCalendarEventSelNotificationTimeAllDayChecked' style='width:11%;text-overflow:ellipsis;'>
            ${timeIntervals.map(time => time === "09:00" ? `<option value="${time}" selected>${time}</option>` : `<option value="${time}">${time}</option>`).join('')}
        </select>
        <a class='hover aCreateCalendarDeleteNotificationAllDayChecked' href='#' style='width:10%;'>
            <i class='fa fa-trash' aria-hidden='true' style='margin-left: 7px;'></i>
        </a>

        <br />
        <div class="error-message" style="color: red; font-size: 0.9em; display: none;"></div>
    </div>
`;

    $(htmlString).insertBefore(this);
});

$('#createCalendarEventNotificationAllDayUnchecked').off('click').on('click', function () {
    const htmlString = String.raw`
    <div class="divCreateEventNotificationAllDayUncheckedRow" style="padding-bottom:5px;">
        <select class='form-control-sm createCalendarEventSelNotificationMethodAllDayUnchecked' style='width:20%;text-overflow:ellipsis;'>
            <option value='Email'>${localizer.Email}</option>
            <option value='Notification'>${localizer.Notification}</option>
        </select>

        <input type='number' class='form-control-sm createCalendarEventSelNotificationNumberAllDayUnchecked' style='width:20%;text-overflow:ellipsis; background-color:#343a40; color:#fff; border-color:#6c757d;' min="0" max="40320">

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
        <div class="error-message" style="color: red; font-size: 0.9em; display: none;"></div>
    </div>
`;

    $(htmlString).insertBefore(this);
});

$('#editCalendarEventNotificationAllDayChecked').off('click').on('click', function () {
    const timeIntervals = Array.from({ length: 96 }, (_, i) => {
        const hours = String(Math.floor(i / 4)).padStart(2, '0');
        const minutes = String((i % 4) * 15).padStart(2, '0');
        return `${hours}:${minutes}`;
    });

    const htmlString = String.raw`
    <div class="divEditEventNotificationAllDayCheckedRow">
        <select class='form-control-sm editCalendarEventSelNotificationMethodAllDayChecked' style='width:20%;text-overflow:ellipsis;'>
            <option value='Email'>${localizer.Email}</option>
            <option value='Notification'>${localizer.Notification}</option>
        </select>

        <input type='number' class='form-control-sm editCalendarEventSelNotificationNumberAllDayChecked' style='width:20%;text-overflow:ellipsis; background-color:#343a40; color:#fff; border-color:#6c757d;' min="0" max="28">

        <select class='form-control-sm editCalendarEventSelNotificationTimeTypeAllDayChecked' style='width:15%;text-overflow:ellipsis;'>
            <option value='Days' selected>${localizer.Days}</option>
            <option value='Weeks'>${localizer.Weeks}</option>
        </select>
        <label style="padding-left:5px;padding-right:5px;">${localizer.BeforeAt}</label>
        <select class='form-control-sm editCalendarEventSelNotificationTimeAllDayChecked' style='width:11%;text-overflow:ellipsis;'>
            ${timeIntervals.map(time => time === "09:00" ? `<option value="${time}" selected>${time}</option>` : `<option value="${time}">${time}</option>`).join('')}
        </select>
        <a class='hover aEditCalendarDeleteNotificationAllDayChecked' href='#' style='width:10%;'>
            <i class='fa fa-trash' aria-hidden='true' style='margin-left: 7px;'></i>
        </a>

        <br />
        <div class="error-message" style="color: red; font-size: 0.9em; display: none;"></div>
    </div>
`;

    $(htmlString).insertBefore(this);
});

$('#editCalendarEventNotificationAllDayUnchecked').off('click').on('click', function () {

    const htmlString = String.raw`
    <div class="divEditEventNotificationAllDayUncheckedRow" style="padding-bottom:5px;">
        <select class='form-control-sm editCalendarEventSelNotificationMethodAllDayUnchecked' style='width:20%;text-overflow:ellipsis;'>
            <option value='Email'>${localizer.Email}</option>
            <option value='Notification'>${localizer.Notification}</option>
        </select>

        <input type='number' class='form-control-sm editCalendarEventSelNotificationNumberAllDayUnchecked' style='width:20%;text-overflow:ellipsis; background-color:#343a40; color:#fff; border-color:#6c757d;' min="0" max="40320">

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
        <div class="error-message" style="color: red; font-size: 0.9em; display: none;"></div>
    </div>
`;

    $(htmlString).insertBefore(this);
});

$(document).on('click', '.aCreateCalendarDeleteNotificationAllDayChecked', function () {
    $(this).parent().remove();
});

$(document).on('click', '.aCreateCalendarDeleteNotificationAllDayUnchecked', function () {
    $(this).parent().remove();
});

$(document).on('click', '.aEditCalendarDeleteNotificationAllDayChecked', function () {
    $(this).parent().remove();
});

$(document).on('click', '.aEditCalendarDeleteNotificationAllDayUnchecked', function () {
    $(this).parent().remove();
});

$('#dropdown-icon').click(function () {
    $('#dropdown-content').toggle();
});

$(window).click(function (event) {
    if (!$(event.target).closest('#dropdown-icon, #dropdown-content').length) {
        $('#dropdown-content').hide();
    }
});

$('#dropdown-content a').click(function () {
    $('#dropdown-content').hide();
});

$('#aCreateCalendar').off('click').on('click', function () {
    $('#createCalendarDialogModal').modal('show');
});

$('#aBrowseCalendarsOfInterest').off('click').on('click', function () {
    $.ajax({
        url: '/Calendar/GetBrowseCalendarsOfInterest',
        method: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        async: false,
        success: function (response) {
            if (response.result) {
                let htmlString = '';
                $.each(response.browseCalendarsOfInterests, function (index, browseCalendarsOfInterest) {
                    htmlString += String.raw`
                        <div class="form-row browseCalendarsOfInterest" style="margin-top: 20px;">
                            <div id="divBrowseCalendarsOfInterest${browseCalendarsOfInterest.id}" class="form-group col-md-3 mb-3 text-center">
                                <input class="form-control form-control-sm checkBoxBrowseCalendarsOfInterest" type="checkbox" ${browseCalendarsOfInterest.checked === true ? "checked" : ""}/>
                            </div>
                            <div class="form-group col-md-9 mb-9 text-center">
                                <label style="word-break: break-all;">${browseCalendarsOfInterest.name}</label>
                            </div>
                        </div>
                    `;
                });

                $('#divBrowseCalendarsOfInterest').html(htmlString);
            }
        }
    });

    if ($('.checkBoxBrowseCalendarsOfInterest').length === 0) {
        $('#allCheckBrowseCalendarsOfInterest').prop('checked', false);
    }
    else {
        $('#allCheckBrowseCalendarsOfInterest').prop('checked', $('.checkBoxBrowseCalendarsOfInterest').length === $('.checkBoxBrowseCalendarsOfInterest:checked').length);
    }

    $('#browseCalendarsOfInterestDialogModal').modal('show');
});

$('#allCheckBrowseCalendarsOfInterest').off('change').on('change', function () {
    $('.checkBoxBrowseCalendarsOfInterest').prop('checked', $(this).prop('checked'));
});

$(document).on('change', '.checkBoxBrowseCalendarsOfInterest', function () {
    $('#allCheckBrowseCalendarsOfInterest').prop('checked', $('.checkBoxBrowseCalendarsOfInterest').length === $('.checkBoxBrowseCalendarsOfInterest:checked').length);
});

$('#formUpdateBrowseCalendarsOfInterest').off('submit').on('submit', function (event) {
    event.preventDefault();

    let calendarBeOtherCalendar = [];

    $('#divBrowseCalendarsOfInterest > .browseCalendarsOfInterest').each(function (index, element) {
        let checkBox = $(element).find('.checkBoxBrowseCalendarsOfInterest');
        if (checkBox.is(':checked')) {
            calendarBeOtherCalendar.push({
                CalendarId: checkBox.parent().attr('id').replace("divBrowseCalendarsOfInterest", "")
            });
        }
    });

    $.ajax({
        url: '/Calendar/UpdateOtherCalendar',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: JSON.stringify(calendarBeOtherCalendar),
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {

                $.ajax({
                    url: '/Calendar/GetOtherCalendars',
                    method: 'POST',
                    headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                    dataType: 'json',
                    data: null,
                    contentType: 'application/json; charset=utf-8',
                    async: false,
                    success: function (response) {
                        if (response.result) {
                            let htmlString = '';
                
                            $.each(response.tempOtherCalendars, function (index, otherCalendar) {
                                htmlString += String.raw`
                                    <label id="lblOtherCalendar${otherCalendar.id}" style="position: relative; border:1px solid #ccc; padding:10px; margin:0 0 10px; display:block">
                                        <input type="checkbox" class="chkOtherCalendar" style="accent-color:${otherCalendar.htmlColorCode}" checked />
                                        <label style="padding-left:0.3em;">${otherCalendar.name}</label>
                                    </label>
                                `;
                            });
                
                            $('#otherCalendars').html(htmlString);

                            calendar.removeAllEvents();

                            let paramValue = {
                                Calendars: []
                            };

                            $('#myCalendars input[type="checkbox"]:checked').each(function () {
                                let calendarsArray = [
                                    { Id: Number($(this).closest('label').attr('id').replace('lblCalendar', '')) }
                                ];

                                for (let i = 0; i < calendarsArray.length; i++) {
                                    paramValue.Calendars.push(calendarsArray[i]);
                                }
                            });

                            $('#otherCalendars input[type="checkbox"]:checked').each(function () {
                                let calendarsArray = [
                                    { Id: Number($(this).closest('label').attr('id').replace('lblOtherCalendar', '')) }
                                ];

                                for (let i = 0; i < calendarsArray.length; i++) {
                                    paramValue.Calendars.push(calendarsArray[i]);
                                }
                            });

                            $.ajax({
                                url: '/Calendar/GetCalendarEvents',
                                method: 'POST',
                                headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                                dataType: 'json',
                                data: JSON.stringify(paramValue),
                                contentType: 'application/json; charset=utf-8',
                                success: function (response) {
                                    if (response.result) {
                                        JSON.parse(response.calendarEvents).forEach(item => {
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
                    }
                });

                $('#browseCalendarsOfInterestDialogModal').modal('hide');
                toastr.success(data.message);
            }
            else {
                toastr.error(data.error);
            }
        }
    });
    
    return false;
});

$(document).on('click', '.edit-calendar', function (e) {
    e.preventDefault();
    e.stopPropagation();

    let selectedCalendarId = $(this).parent().attr('id').replace("lblCalendar", "");

    $.ajax({
        url: '/Calendar/IsCalendarExists' + '?id=' + selectedCalendarId,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                let formEditCalendar = $('#formEditCalendar');

                formEditCalendar.find('input[id="editCalendarId"]').val(data.calendar.id);
                formEditCalendar.find('input[id="editCalendarName"]').val(data.calendar.name);
                formEditCalendar.find('textarea[id="editCalendarDescription"]').val(data.calendar.description);
                formEditCalendar.find('input[id="editCalendarHtmlColorCode"]').val(data.calendar.htmlColorCode);
                formEditCalendar.find('select[id="editCalendarTimeZone"]').val(data.calendar.timeZoneIanaId).change();

                $('#editCalendarDialogModal').modal({
                    keyboard: false,
                    backdrop: "static"
                });

                $('#editCalendarDialogModal').modal('toggle');
                $('#editCalendarDialogModal').modal('show');
            }
            else {
                toastr.error(data.error);
            }
        }
    });
});

$(document).on('mouseenter', '.edit-calendar', function () {
    $(this).css('color', 'blue');
});

$(document).on('mouseleave', '.edit-calendar', function () {
    $(this).css('color', '');
});

$(document).on('click', '.delete-calendar', function (e) {
    e.preventDefault();
    e.stopPropagation();

    let calendarId = $(this).parent().attr('id').replace("lblCalendar", "");
    $('#confirmDeleteCalendarDialogModal').data('calendar-id', calendarId);

    $('#confirmDeleteCalendarDialogModal').modal({
        keyboard: false,
        backdrop: "static"
    });

    $('#confirmDeleteCalendarDialogModal').modal('toggle');
    $('#confirmDeleteCalendarDialogModal').modal('show');
});

$(document).on('mouseenter', '.delete-calendar', function () {
    $(this).css('color', 'blue');
});

$(document).on('mouseleave', '.delete-calendar', function () {
    $(this).css('color', '');
});

$('#aEditCalendarEventAttachedFile').off('click').on('click', function (event) {
    event.preventDefault();
    let base64Data = $(this).attr('data-file');
    let contentType = $(this).attr('data-contenttype');
    let name = $(this).attr('data-name')

    if (base64Data && contentType) {
        let blob = base64ToBlob(base64Data, contentType);
        let url = URL.createObjectURL(blob);
        let a = document.createElement('a');
        try {
            a.href = url;
            a.download = name;
            a.click();
        } finally {
            setTimeout(() => URL.revokeObjectURL(url), 100);
            a.remove();
        }
    }
});

$('#aViewCalendarEventAttachedFile').off('click').on('click', function (event) {
    event.preventDefault();
    let base64Data = $(this).attr('data-file');
    let contentType = $(this).attr('data-contenttype');
    let name = $(this).attr('data-name')

    if (base64Data && contentType) {
        let blob = base64ToBlob(base64Data, contentType);
        let url = URL.createObjectURL(blob);
        let a = document.createElement('a');
        try {
            a.href = url;
            a.download = name;
            a.click();
        } finally {
            setTimeout(() => URL.revokeObjectURL(url), 100);
            a.remove();
        }
    }
});
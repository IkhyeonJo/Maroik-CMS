// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
$.datepicker.setDefaults({
    dateFormat: 'yy-mm-dd',
    prevText: '이전 달',
    nextText: '다음 달',
    monthNames: ['1월', '2월', '3월', '4월', '5월', '6월', '7월', '8월', '9월', '10월', '11월', '12월'],
    monthNamesShort: ['1월', '2월', '3월', '4월', '5월', '6월', '7월', '8월', '9월', '10월', '11월', '12월'],
    dayNames: ['일', '월', '화', '수', '목', '금', '토'],
    dayNamesShort: ['일', '월', '화', '수', '목', '금', '토'],
    dayNamesMin: ['일', '월', '화', '수', '목', '금', '토'],
    showMonthAfterYear: true,
    yearSuffix: '년'
});

$(function () {
    $("#createFixedExpenditureTabs").tabs();
    $("#editFixedExpenditureTabs").tabs();
    $("#createFixedExpenditureMaturityDate").datepicker({
        showButtonPanel: true,
        beforeShow: function (input) {
            setTimeout(function () {
                var buttonPane = $(input)
                    .datepicker("widget")
                    .find(".ui-datepicker-buttonpane");

                $("<button>", {
                    text: "만기일 없음",
                    click: function () {
                        $.datepicker._clearDate(input);
                        $("#createFixedExpenditureMaturityDate").datepicker("setDate", new Date('9999', '11', '31'));
                    }
                }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                $("<button>", {
                    text: "오늘",
                    click: function () {
                        $.datepicker._clearDate(input);
                        $("#createFixedExpenditureMaturityDate").datepicker("setDate", new Date());
                    }
                }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

            }, 1);
        },
        onChangeMonthYear: function (year, month, instance) {
            setTimeout(function () {
                var buttonPane = $(instance)
                    .datepicker("widget")
                    .find(".ui-datepicker-buttonpane");

                $("<button>", {
                    text: "만기일 없음",
                    click: function () {
                        $.datepicker._clearDate(instance.input);
                        $("#createFixedExpenditureMaturityDate").datepicker("setDate", new Date('9999', '11', '31'));
                    }
                }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                $("<button>", {
                    text: "오늘",
                    click: function () {
                        $.datepicker._clearDate(instance.input);
                        $("#createFixedExpenditureMaturityDate").datepicker("setDate", new Date());
                    }
                }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

            }, 1);
        },
        onSelect: function (date) {
            if (!date) {
                date = '9999-12-31';
            }
            let tempCurrentDate = new Date();
            let month = tempCurrentDate.getMonth() + 1;
            let day = tempCurrentDate.getDate();

            month = month >= 10 ? month : '0' + month;
            day = day >= 0 ? day : '0' + day;

            let currentDate = tempCurrentDate.getFullYear() + '-' + month + '-' + day;

            if (date < currentDate) {
                alert('만기일이 현재 날짜보다 앞설 수 없습니다.');
                $("#createFixedExpenditureMaturityDate").datepicker("setDate", new Date());
            }
        }
    });

    $("#editFixedExpenditureMaturityDate").datepicker({
        showButtonPanel: true,
        beforeShow: function (input) {
            setTimeout(function () {
                var buttonPane = $(input)
                    .datepicker("widget")
                    .find(".ui-datepicker-buttonpane");

                $("<button>", {
                    text: "만기일 없음",
                    click: function () {
                        $.datepicker._clearDate(input);
                        $("#editFixedExpenditureMaturityDate").datepicker("setDate", new Date('9999', '11', '31'));
                    }
                }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                $("<button>", {
                    text: "오늘",
                    click: function () {
                        $.datepicker._clearDate(input);
                        $("#editFixedExpenditureMaturityDate").datepicker("setDate", new Date());
                    }
                }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

            }, 1);
        },
        onChangeMonthYear: function (year, month, instance) {
            setTimeout(function () {
                var buttonPane = $(instance)
                    .datepicker("widget")
                    .find(".ui-datepicker-buttonpane");

                $("<button>", {
                    text: "만기일 없음",
                    click: function () {
                        $.datepicker._clearDate(instance.input);
                        $("#editFixedExpenditureMaturityDate").datepicker("setDate", new Date('9999', '11', '31'));
                    }
                }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                $("<button>", {
                    text: "오늘",
                    click: function () {
                        $.datepicker._clearDate(instance.input);
                        $("#editFixedExpenditureMaturityDate").datepicker("setDate", new Date());
                    }
                }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

            }, 1);
        },
        onSelect: function (date) {
            if (!date) {
                date = '9999-12-31';
            }
            let tempCurrentDate = new Date();
            let month = tempCurrentDate.getMonth() + 1;
            let day = tempCurrentDate.getDate();

            month = month >= 10 ? month : '0' + month;
            day = day >= 0 ? day : '0' + day;

            let currentDate = tempCurrentDate.getFullYear() + '-' + month + '-' + day;

            if (date < currentDate) {
                alert('만기일이 현재 날짜보다 앞설 수 없습니다.');
                $("#editFixedExpenditureMaturityDate").datepicker("setDate", new Date());
            }
        }
    });

    $("#createFixedExpenditureMaturityDate").datepicker("setDate", new Date());
    $("#editFixedExpenditureMaturityDate").datepicker("setDate", new Date());

    $('<style type="text/css"> .ui-datepicker-close { display: none; } </style>').appendTo("head");
    $('<style type="text/css"> .ui-datepicker-current { display: none; } </style>').appendTo("head");
});
$(document).on("ajaxError", function () {
    window.location.href = "/DashBoard/AnonymousIndex";
});

function ChangeCulture(culture) {

    let paramValue = JSON.stringify({
        Culture: culture
    });

    $.ajax({
        url: '/DashBoard/CultureManagement',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                window.location.href = $("#returnUri").val();
            }
            else {
                window.location.href = $("#returnUri").val();
            }
        }
    });
}

$('#aChangeCultureEnUS').off('click').on('click', function () {
    ChangeCulture('en-US');
});

$('#aChangeCultureKoKR').off('click').on('click', function () {
    ChangeCulture('ko-KR');
});
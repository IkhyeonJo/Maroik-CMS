function base64ToBlob(base64, mime) {
    const byteCharacters = atob(base64);
    const byteNumbers = new Array(byteCharacters.length);
    for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
    }
    const byteArray = new Uint8Array(byteNumbers);
    return new Blob([byteArray], { type: mime });
}

$(function () {

    if ($('#free_forum_detail_updated').length > 0) {
        let utcDateStr = $('#free_forum_detail_updated').text();
        let utcDate = new Date(utcDateStr);
        let localDateStr = utcDate.toLocaleString();
        $('#free_forum_detail_updated').text(localDateStr);
    }

    $('#tblFreeForum tbody tr .free_forum_board_created').each(function () {
        let $element = $(this);
        let $span = $element.find('span');
        let utcDateStr = $span.length > 0 ? $span.html() : $element.text();
        let utcDate = new Date(utcDateStr);
        let localDateStr = utcDate.toLocaleString();
        $span.length > 0 ? $span.html(localDateStr) : $element.text(localDateStr);
    });

    $('.free_forum_detail_comment_created').each(function () {
        let utcDateStr = $(this).text();
        let utcDate = new Date(utcDateStr);
        let localDateStr = utcDate.toLocaleString();
        $(this).text(localDateStr);
    });

    function SearchBoard() {
        window.location.href = "/Forum/FreeForum?searchType=" + $('#searchType').val() + "&searchText=" + $('#btnFreeForumSearchText').val();
    }

    $('#btnFreeForumSearchBoard').off('click').on('click', function () {
        SearchBoard();
    });

    $('#btnFreeForumWrite').off('click').on('click', function () {
        location.href = "/Forum/FreeForum?method=write";
    });

    $('#btnFreeForumList').off('click').on('click', function () {
        location.href = $(this).attr("data-link");
    });

    $('#btnFreeForumSearchText').off('keypress').on('keypress', function (event) {
        if (event.keyCode === 13 || event.which === 13) {
            SearchBoard();
        }
    });

    $('#formWriteFreeComment').off('keydown').on('keydown', function (event) {
        return event.key != 'Enter';
    });

    if ($('#detailBoardContent').length > 0 && $('#detailBoardContent').is(":hidden")) {
        const parser = new DOMParser();
        const htmlDoc = parser.parseFromString($('#detailBoardContent').html(), 'text/html');

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
        $('#detailBoardContent').html(updatedHtml);
        $('#detailBoardContent').show();
    }

    if ($('#aDetailBoardAttachedFile').length > 0) {
        $('#aDetailBoardAttachedFile').off('click').on('click', function (event) {
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
    } else if ($('#aEditBoardAttachedFile').length > 0) {
        $('#aEditBoardAttachedFile').off('click').on('click', function (event) {
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
    }
});
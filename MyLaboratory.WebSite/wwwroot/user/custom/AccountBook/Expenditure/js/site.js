// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
$(function () {
    $("#createExpenditureTabs").tabs();
    $("#editExpenditureTabs").tabs();
});

var selectedRowColor = "table-primary";

// Triggered when grid's row is clicked. It's recommended to use event delegation in ajax scenarios.
document.addEventListener("rowclick", e => {
    //e.detail.data - clicked row's data from columns.
    //e.detail.grid - grid's instance.
    //e.detail.originalEvent - original tr click event which triggered the rowclick.

    var selectedRow = e.detail;
    var selectedRowId = selectedRow.data.Id;

    $('#expenditureGrid > table > tbody  > tr').each(function (index, tr) {
        $(this).removeClass(selectedRowColor);
    }); // remove all selected row color

    $('#expenditureGrid > table > tbody  > tr').each(function (index, tr) {
        if ((new String(selectedRowId).valueOf() === new String($(this).attr('data-id')).valueOf())) {
            $(this).addClass(selectedRowColor);
        }
    }); // set selected row color class
});

// Triggered before grid starts loading. It's recommended to use event delegation in ajax scenarios.
document.addEventListener("reloadstart", e => {
    //e.detail.grid - grid's instance.
});

// Triggered after grid stop loading. It's recommended to use event delegation in ajax scenarios.
document.addEventListener("reloadend", e => {
    //e.detail.grid - grid's instance.
});

// Triggered after grid reload fails. It's recommended to use event delegation in ajax scenarios.
document.addEventListener("reloadfail", e => {
    //e.detail.reason - failed response promise reason.
    //e.detail.grid - grid's instance.

    // Preventing default stops failed promise from bubbling out.
});

// Triggered after grid configuration changes. It's recommended to use event delegation in ajax scenarios.
document.addEventListener("gridconfigure", e => {
    //e.detail.grid - grid's instance.
});


$(document).on("dblclick", "#expenditureGrid > table > tbody  > tr", function () {
    //console.log('dblclick-start');
    $('#expenditureGrid > table > tbody  > tr').each(function (index, tr) {

        if ($(this).attr("class").includes(selectedRowColor)) { // Get SelectedRow by Color
            EditExpenditureGridRow();
        }
    });
    //console.log('dblclick-end');
});
$('#gridSearch').on('input', function () {
    const grid = new MvcGrid(document.querySelector(".mvc-grid"));

    grid.url.searchParams.set("wholeSearch", this.value);

    grid.reload();
});

function CreateFormShowExpenditureSubClassBySelectedExpenditureMainClass(createExpenditureMainClass) {

    if (createExpenditureMainClass.value == "RegularSavings") {
        $('#createExpenditureSubClass option[value=Deposit]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=Investment]').prop('disabled', true).prop('hidden', true);

        $('#createExpenditureSubClass option[value=PublicPension]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=DebtRepayment]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=Tax]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=SocialInsurance]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', true).prop('hidden', true);

        $('#createExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=EducationExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=TransportationCost]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=CommunicationCost]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=PinMoney]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=OtherExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', true).prop('hidden', true);

        $("#createExpenditureSubClass option:selected").removeAttr("selected");

        $('#createExpenditureSubClass option[value=Deposit]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#createExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', false).prop('hidden', false);
        $('#createExpenditureSubClass option[value=Investment]').prop('disabled', false).prop('hidden', false);

        $("#createExpenditureSubClass").val("Deposit").change();

        $("#divCreateExpenditureMyDepositAsset").show();
    }
    else if (createExpenditureMainClass.value == "NonConsumerSpending") {
        $('#createExpenditureSubClass option[value=Deposit]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=Investment]').prop('disabled', true).prop('hidden', true);

        $('#createExpenditureSubClass option[value=PublicPension]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=DebtRepayment]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=Tax]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=SocialInsurance]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', true).prop('hidden', true);

        $('#createExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=EducationExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=TransportationCost]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=CommunicationCost]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=PinMoney]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=OtherExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', true).prop('hidden', true);

        $("#createExpenditureSubClass option:selected").removeAttr("selected");

        $('#createExpenditureSubClass option[value=PublicPension]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#createExpenditureSubClass option[value=DebtRepayment]').prop('disabled', false).prop('hidden', false);
        $('#createExpenditureSubClass option[value=Tax]').prop('disabled', false).prop('hidden', false);
        $('#createExpenditureSubClass option[value=SocialInsurance]').prop('disabled', false).prop('hidden', false);
        $('#createExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', false).prop('hidden', false);
        $('#createExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', false).prop('hidden', false);

        $("#createExpenditureSubClass").val("PublicPension").change();

        $("#divCreateExpenditureMyDepositAsset").show();
    }
    else if (createExpenditureMainClass.value == "ConsumerSpending") {
        $('#createExpenditureSubClass option[value=Deposit]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=Investment]').prop('disabled', true).prop('hidden', true);

        $('#createExpenditureSubClass option[value=PublicPension]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=DebtRepayment]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=Tax]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=SocialInsurance]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', true).prop('hidden', true);

        $('#createExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=EducationExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=TransportationCost]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=CommunicationCost]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=PinMoney]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=OtherExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', true).prop('hidden', true);

        $("#createExpenditureSubClass option:selected").removeAttr("selected");

        $('#createExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#createExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', false).prop('hidden', false);
        $('#createExpenditureSubClass option[value=EducationExpenses]').prop('disabled', false).prop('hidden', false);
        $('#createExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', false).prop('hidden', false);
        $('#createExpenditureSubClass option[value=TransportationCost]').prop('disabled', false).prop('hidden', false);
        $('#createExpenditureSubClass option[value=CommunicationCost]').prop('disabled', false).prop('hidden', false);
        $('#createExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', false).prop('hidden', false);
        $('#createExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', false).prop('hidden', false);
        $('#createExpenditureSubClass option[value=PinMoney]').prop('disabled', false).prop('hidden', false);
        $('#createExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', false).prop('hidden', false);
        $('#createExpenditureSubClass option[value=OtherExpenses]').prop('disabled', false).prop('hidden', false);
        $('#createExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', false).prop('hidden', false);

        $("#createExpenditureSubClass").val("MealOrEatOutExpenses").change();

        $("#divCreateExpenditureMyDepositAsset").hide();
    }
}

function CreateFormShowExpenditureDivCreateExpenditureMyDepositAssetBySelectedExpenditureSubClass(createExpenditureSubClass) {

    if (createExpenditureSubClass.value == "Deposit" ||
        createExpenditureSubClass.value == "MyAssetTransfer" ||
        createExpenditureSubClass.value == "Investment" ||
        createExpenditureSubClass.value == "PublicPension" ||
        createExpenditureSubClass.value == "DebtRepayment") {
        $("#divCreateExpenditureMyDepositAsset").show();
    }
    else {
        $("#divCreateExpenditureMyDepositAsset").hide();
    }
}

function CreateExpenditure() {

    if (!$('#formCreateExpenditure').valid()) {
        return false;
    }

    var createForm = $('#formCreateExpenditure');

    var mainClass = createForm.find('select[id="createExpenditureMainClass"]').val();
    var subClass = createForm.find('select[id="createExpenditureSubClass"]').val();
    var contents = createForm.find('input[id="createExpenditureContents"]').val();
    var amount = createForm.find('input[id="createExpenditureAmount"]').val();
    var paymentMethod = createForm.find('select[id="createExpenditurePaymentMethod"]').val();
    var note = createForm.find('input[id="createExpenditureNote"]').val();
    var myDepositAsset = createForm.find('select[id="createExpenditureMyDepositAsset"]').val();

    var paramValue = JSON.stringify({
        MainClass: mainClass,
        SubClass: subClass,
        Contents: contents,
        Amount: amount,
        PaymentMethod: paymentMethod,
        Note: note,
        MyDepositAsset: myDepositAsset
    });

    $.ajax({
        url: '/AccountBook/CreateExpenditure',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        //async: false,
        //cache: false,
        success: function (data) {
            if (data.result) {
                $('#createExpenditureDialogModal').modal('hide'); // hide Modal

                const grid = new MvcGrid(document.querySelector(".mvc-grid")); //reload grid
                grid.reload(); //reload grid

                toastr.success(data.message);
            }
            else {
                toastr.error(data.error);
            }
        }
    });

    return false;
}

function EditExpenditureGridRow(errorMessageSelectGridRow) {

    var selectedRowId = "";

    $('#expenditureGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) { // Get SelectedRow by Color
            selectedRowId = $(this).attr("data-id");
        }
    }); // Search Selected Row

    if (selectedRowId === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    } // check whether selected row exist or not

    $.ajax({
        url: '/AccountBook/IsExpenditureExists' + '?id=' + selectedRowId,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        //async: false,
        //cache: false,
        success: function (data) {
            if (data.result) {

                var formEditExpenditure = $('#formEditExpenditure');

                formEditExpenditure.find('input[id="editExpenditureId"]').val(data.expenditure.id);
                formEditExpenditure.find('select[id="editExpenditureMainClass"]').val(data.expenditure.mainClass).change();

                if (data.expenditure.mainClass == "RegularSavings") {
                    $('#editExpenditureSubClass option[value=Deposit]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=Investment]').prop('disabled', true).prop('hidden', true);

                    $('#editExpenditureSubClass option[value=PublicPension]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=DebtRepayment]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=Tax]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=SocialInsurance]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', true).prop('hidden', true);

                    $('#editExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=EducationExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=TransportationCost]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=CommunicationCost]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=PinMoney]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=OtherExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', true).prop('hidden', true);

                    $("#editExpenditureSubClass option:selected").removeAttr("selected");

                    $('#editExpenditureSubClass option[value=Deposit]').prop('disabled', false).prop('hidden', false);
                    $('#editExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', false).prop('hidden', false);
                    $('#editExpenditureSubClass option[value=Investment]').prop('disabled', false).prop('hidden', false);

                    formEditExpenditure.find('select[id="editExpenditureSubClass"]').val(data.expenditure.subClass).change();
                }
                else if (data.expenditure.mainClass == "NonConsumerSpending") {
                    $('#editExpenditureSubClass option[value=Deposit]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=Investment]').prop('disabled', true).prop('hidden', true);

                    $('#editExpenditureSubClass option[value=PublicPension]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=DebtRepayment]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=Tax]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=SocialInsurance]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', true).prop('hidden', true);

                    $('#editExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=EducationExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=TransportationCost]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=CommunicationCost]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=PinMoney]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=OtherExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', true).prop('hidden', true);

                    $("#editExpenditureSubClass option:selected").removeAttr("selected");

                    $('#editExpenditureSubClass option[value=PublicPension]').prop('disabled', false).prop('hidden', false);
                    $('#editExpenditureSubClass option[value=DebtRepayment]').prop('disabled', false).prop('hidden', false);
                    $('#editExpenditureSubClass option[value=Tax]').prop('disabled', false).prop('hidden', false);
                    $('#editExpenditureSubClass option[value=SocialInsurance]').prop('disabled', false).prop('hidden', false);
                    $('#editExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', false).prop('hidden', false);
                    $('#editExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', false).prop('hidden', false);

                    formEditExpenditure.find('select[id="editExpenditureSubClass"]').val(data.expenditure.subClass).change();
                }
                else if (data.expenditure.mainClass == "ConsumerSpending") {
                    $('#editExpenditureSubClass option[value=Deposit]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=Investment]').prop('disabled', true).prop('hidden', true);

                    $('#editExpenditureSubClass option[value=PublicPension]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=DebtRepayment]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=Tax]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=SocialInsurance]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', true).prop('hidden', true);

                    $('#editExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=EducationExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=TransportationCost]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=CommunicationCost]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=PinMoney]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=OtherExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', true).prop('hidden', true);

                    $("#editExpenditureSubClass option:selected").removeAttr("selected");

                    $('#editExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', false).prop('hidden', false);
                    $('#editExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', false).prop('hidden', false);
                    $('#editExpenditureSubClass option[value=EducationExpenses]').prop('disabled', false).prop('hidden', false);
                    $('#editExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', false).prop('hidden', false);
                    $('#editExpenditureSubClass option[value=TransportationCost]').prop('disabled', false).prop('hidden', false);
                    $('#editExpenditureSubClass option[value=CommunicationCost]').prop('disabled', false).prop('hidden', false);
                    $('#editExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', false).prop('hidden', false);
                    $('#editExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', false).prop('hidden', false);
                    $('#editExpenditureSubClass option[value=PinMoney]').prop('disabled', false).prop('hidden', false);
                    $('#editExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', false).prop('hidden', false);
                    $('#editExpenditureSubClass option[value=OtherExpenses]').prop('disabled', false).prop('hidden', false);
                    $('#editExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', false).prop('hidden', false);

                    formEditExpenditure.find('select[id="editExpenditureSubClass"]').val(data.expenditure.subClass).change();
                }

                if (data.expenditure.subClass == "Deposit" ||
                    data.expenditure.subClass == "MyAssetTransfer" ||
                    data.expenditure.subClass == "Investment" ||
                    data.expenditure.subClass == "PublicPension" ||
                    data.expenditure.subClass == "DebtRepayment") {
                    $("#divEditExpenditureMyDepositAsset").show();
                }
                else {
                    $("#divEditExpenditureMyDepositAsset").hide();
                }


                formEditExpenditure.find('input[id="editExpenditureContents"]').val(data.expenditure.contents);
                formEditExpenditure.find('input[id="editExpenditureAmount"]').val(data.expenditure.amount);
                formEditExpenditure.find('select[id="editExpenditurePaymentMethod"]').val(data.expenditure.paymentMethod).change();
                formEditExpenditure.find('input[id="editExpenditureNote"]').val(data.expenditure.note);
                if (data.expenditure.myDepositAsset) {
                    formEditExpenditure.find('select[id="editExpenditureMyDepositAsset"]').val(data.expenditure.myDepositAsset).change();
                }
                else {
                    formEditExpenditure.find('select[id="editExpenditureMyDepositAsset"]').val(data.expenditure.paymentMethod).change();
                }
                

                $('#editExpenditureDialogModal').modal({
                    keyboard: false,
                    backdrop: "static"
                });

                $('#editExpenditureDialogModal').modal('toggle');
                $('#editExpenditureDialogModal').modal('show');
            }
            else {
                toastr.error(data.error);
            }
        }
    });
}

function EditFormShowExpenditureSubClassBySelectedExpenditureMainClass(editExpenditureMainClass) {

    if (editExpenditureMainClass.value == "RegularSavings") {
        $('#editExpenditureSubClass option[value=Deposit]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=Investment]').prop('disabled', true).prop('hidden', true);

        $('#editExpenditureSubClass option[value=PublicPension]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=DebtRepayment]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=Tax]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=SocialInsurance]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', true).prop('hidden', true);

        $('#editExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=EducationExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=TransportationCost]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=CommunicationCost]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=PinMoney]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=OtherExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', true).prop('hidden', true);

        $("#editExpenditureSubClass option:selected").removeAttr("selected");

        $('#editExpenditureSubClass option[value=Deposit]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#editExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', false).prop('hidden', false);
        $('#editExpenditureSubClass option[value=Investment]').prop('disabled', false).prop('hidden', false);

        $("#editExpenditureSubClass").val("Deposit").change();

        $("#divEditExpenditureMyDepositAsset").show();
    }
    else if (editExpenditureMainClass.value == "NonConsumerSpending") {
        $('#editExpenditureSubClass option[value=Deposit]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=Investment]').prop('disabled', true).prop('hidden', true);

        $('#editExpenditureSubClass option[value=PublicPension]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=DebtRepayment]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=Tax]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=SocialInsurance]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', true).prop('hidden', true);

        $('#editExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=EducationExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=TransportationCost]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=CommunicationCost]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=PinMoney]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=OtherExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', true).prop('hidden', true);

        $("#editExpenditureSubClass option:selected").removeAttr("selected");

        $('#editExpenditureSubClass option[value=PublicPension]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#editExpenditureSubClass option[value=DebtRepayment]').prop('disabled', false).prop('hidden', false);
        $('#editExpenditureSubClass option[value=Tax]').prop('disabled', false).prop('hidden', false);
        $('#editExpenditureSubClass option[value=SocialInsurance]').prop('disabled', false).prop('hidden', false);
        $('#editExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', false).prop('hidden', false);
        $('#editExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', false).prop('hidden', false);

        $("#editExpenditureSubClass").val("PublicPension").change();

        $("#divEditExpenditureMyDepositAsset").show();
    }
    else if (editExpenditureMainClass.value == "ConsumerSpending") {
        $('#editExpenditureSubClass option[value=Deposit]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=Investment]').prop('disabled', true).prop('hidden', true);

        $('#editExpenditureSubClass option[value=PublicPension]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=DebtRepayment]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=Tax]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=SocialInsurance]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', true).prop('hidden', true);

        $('#editExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=EducationExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=TransportationCost]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=CommunicationCost]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=PinMoney]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=OtherExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', true).prop('hidden', true);

        $("#editExpenditureSubClass option:selected").removeAttr("selected");

        $('#editExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#editExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', false).prop('hidden', false);
        $('#editExpenditureSubClass option[value=EducationExpenses]').prop('disabled', false).prop('hidden', false);
        $('#editExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', false).prop('hidden', false);
        $('#editExpenditureSubClass option[value=TransportationCost]').prop('disabled', false).prop('hidden', false);
        $('#editExpenditureSubClass option[value=CommunicationCost]').prop('disabled', false).prop('hidden', false);
        $('#editExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', false).prop('hidden', false);
        $('#editExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', false).prop('hidden', false);
        $('#editExpenditureSubClass option[value=PinMoney]').prop('disabled', false).prop('hidden', false);
        $('#editExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', false).prop('hidden', false);
        $('#editExpenditureSubClass option[value=OtherExpenses]').prop('disabled', false).prop('hidden', false);
        $('#editExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', false).prop('hidden', false);

        $("#editExpenditureSubClass").val("MealOrEatOutExpenses").change();

        $("#divEditExpenditureMyDepositAsset").hide();
    }
}

function EditFormShowExpenditureDivCreateExpenditureMyDepositAssetBySelectedExpenditureSubClass(editExpenditureSubClass) {

    if (editExpenditureSubClass.value == "Deposit" ||
        editExpenditureSubClass.value == "MyAssetTransfer" ||
        editExpenditureSubClass.value == "Investment" ||
        editExpenditureSubClass.value == "PublicPension" ||
        editExpenditureSubClass.value == "DebtRepayment") {
        $("#divEditExpenditureMyDepositAsset").show();
    }
    else {
        $("#divEditExpenditureMyDepositAsset").hide();
    }
}

function UpdateExpenditure() {

    if (!$('#formEditExpenditure').valid()) {
        return false;
    }

    var createForm = $('#formEditExpenditure');

    var id = createForm.find('input[id="editExpenditureId"]').val();
    var mainClass = createForm.find('select[id="editExpenditureMainClass"]').val();
    var subClass = createForm.find('select[id="editExpenditureSubClass"]').val();
    var contents = createForm.find('input[id="editExpenditureContents"]').val();
    var amount = createForm.find('input[id="editExpenditureAmount"]').val();
    var paymentMethod = createForm.find('select[id="editExpenditurePaymentMethod"]').val();
    var note = createForm.find('input[id="editExpenditureNote"]').val();
    var myDepositAsset = createForm.find('select[id="editExpenditureMyDepositAsset"]').val();

    var paramValue = JSON.stringify({
        Id: id,
        MainClass: mainClass,
        SubClass: subClass,
        Contents: contents,
        Amount: amount,
        PaymentMethod: paymentMethod,
        Note: note,
        MyDepositAsset: myDepositAsset
    });

    $.ajax({
        url: '/AccountBook/UpdateExpenditure',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        //async: false,
        //cache: false,
        success: function (data) {
            if (data.result) {
                $('#editExpenditureDialogModal').modal('hide'); // hide Modal

                const grid = new MvcGrid(document.querySelector(".mvc-grid")); //reload grid
                grid.reload(); //reload grid

                toastr.success(data.message);
            }
            else {
                toastr.error(data.error);
            }
        }
    });
    return false;
}

function ConfirmDeleteExpenditure(errorMessageSelectGridRow) {

    var selectedRowId = "";

    $('#expenditureGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) { // Get SelectedRow by Color
            selectedRowId = $(this).attr("data-id");
        }
    }); // Search Selected Row

    if (selectedRowId === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    } // check whether selected row exist or not

    $('#confirmDeleteExpenditureDialogModal').modal({
        keyboard: false,
        backdrop: "static"
    });

    $('#confirmDeleteExpenditureDialogModal').modal('toggle');
    $('#confirmDeleteExpenditureDialogModal').modal('show');
}

function DeleteExpenditure(errorMessageSelectGridRow) {

    var selectedRowId = "";

    $('#expenditureGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) { // Get SelectedRow by Color
            selectedRowId = $(this).attr("data-id");
        }
    }); // Search Selected Row

    if (selectedRowId === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    } // check whether selected row exist or not

    $.ajax({
        url: '/AccountBook/IsExpenditureExists' + '?id=' + selectedRowId,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        //async: false,
        //cache: false,
        success: function (data) {
            if (data.result) {

                var paramValue = JSON.stringify({
                    Id: data.expenditure.id
                });

                $.ajax({
                    url: '/AccountBook/DeleteExpenditure',
                    type: 'POST',
                    headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                    dataType: 'json',
                    data: paramValue,
                    contentType: 'application/json; charset=utf-8',
                    //async: false,
                    //cache: false,
                    success: function (data) {
                        if (data.result) {
                            $('#confirmDeleteExpenditureDialogModal').modal('hide'); // hide Modal

                            const grid = new MvcGrid(document.querySelector(".mvc-grid")); //reload grid
                            grid.reload(); //reload grid

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

function ExportExcelExpenditure() {
    var form = document.createElement("form");
    var element1 = document.createElement("input");
    var element2 = document.createElement("input");

    form.method = "POST";
    form.action = "/AccountBook/ExportExcelExpenditure";

    element1.name = "__RequestVerificationToken";
    element1.value = $('input[name="__RequestVerificationToken"]').val();
    form.appendChild(element1);

    element2.name = "fileName";
    element2.value = "Expenditure";
    form.appendChild(element2);

    document.body.appendChild(form);

    form.submit();
}
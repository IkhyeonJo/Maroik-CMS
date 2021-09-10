// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

var selectedRowColor = "table-primary";

// Triggered when grid's row is clicked. It's recommended to use event delegation in ajax scenarios.
document.addEventListener("rowclick", e => {
    //e.detail.data - clicked row's data from columns.
    //e.detail.grid - grid's instance.
    //e.detail.originalEvent - original tr click event which triggered the rowclick.

    var selectedRow = e.detail;
    var selectedRowId = selectedRow.data.Id;

    $('#fixedExpenditureGrid > table > tbody  > tr').each(function (index, tr) {
        $(this).removeClass(selectedRowColor);
    }); // remove all selected row color

    $('#fixedExpenditureGrid > table > tbody  > tr').each(function (index, tr) {
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


$(document).on("dblclick", "#fixedExpenditureGrid > table > tbody  > tr", function () {
    //console.log('dblclick-start');
    $('#fixedExpenditureGrid > table > tbody  > tr').each(function (index, tr) {

        if ($(this).attr("class").includes(selectedRowColor)) { // Get SelectedRow by Color
            EditFixedExpenditureGridRow();
        }
    });
    //console.log('dblclick-end');
});
$('#gridSearch').on('input', function () {
    const grid = new MvcGrid(document.querySelector(".mvc-grid"));

    grid.url.searchParams.set("wholeSearch", this.value);

    grid.reload();
});

function CreateFormShowFixedExpenditureSubClassBySelectedFixedExpenditureMainClass(createFixedExpenditureMainClass) {

    if (createFixedExpenditureMainClass.value == "RegularSavings") {
        $('#createFixedExpenditureSubClass option[value=Deposit]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=Investment]').prop('disabled', true).prop('hidden', true);

        $('#createFixedExpenditureSubClass option[value=PublicPension]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=DebtRepayment]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=Tax]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=SocialInsurance]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', true).prop('hidden', true);

        $('#createFixedExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=EducationExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=TransportationCost]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=CommunicationCost]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=PinMoney]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=OtherExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', true).prop('hidden', true);

        $("#createFixedExpenditureSubClass option:selected").removeAttr("selected");

        $('#createFixedExpenditureSubClass option[value=Deposit]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#createFixedExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', false).prop('hidden', false);
        $('#createFixedExpenditureSubClass option[value=Investment]').prop('disabled', false).prop('hidden', false);

        $("#createFixedExpenditureSubClass").val("Deposit").change();

        $("#divCreateFixedExpenditureMyDepositAsset").show();
    }
    else if (createFixedExpenditureMainClass.value == "NonConsumerSpending") {
        $('#createFixedExpenditureSubClass option[value=Deposit]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=Investment]').prop('disabled', true).prop('hidden', true);

        $('#createFixedExpenditureSubClass option[value=PublicPension]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=DebtRepayment]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=Tax]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=SocialInsurance]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', true).prop('hidden', true);

        $('#createFixedExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=EducationExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=TransportationCost]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=CommunicationCost]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=PinMoney]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=OtherExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', true).prop('hidden', true);

        $("#createFixedExpenditureSubClass option:selected").removeAttr("selected");

        $('#createFixedExpenditureSubClass option[value=PublicPension]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#createFixedExpenditureSubClass option[value=DebtRepayment]').prop('disabled', false).prop('hidden', false);
        $('#createFixedExpenditureSubClass option[value=Tax]').prop('disabled', false).prop('hidden', false);
        $('#createFixedExpenditureSubClass option[value=SocialInsurance]').prop('disabled', false).prop('hidden', false);
        $('#createFixedExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', false).prop('hidden', false);
        $('#createFixedExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', false).prop('hidden', false);

        $("#createFixedExpenditureSubClass").val("PublicPension").change();

        $("#divCreateFixedExpenditureMyDepositAsset").show();
    }
    else if (createFixedExpenditureMainClass.value == "ConsumerSpending") {
        $('#createFixedExpenditureSubClass option[value=Deposit]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=Investment]').prop('disabled', true).prop('hidden', true);

        $('#createFixedExpenditureSubClass option[value=PublicPension]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=DebtRepayment]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=Tax]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=SocialInsurance]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', true).prop('hidden', true);

        $('#createFixedExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=EducationExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=TransportationCost]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=CommunicationCost]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=PinMoney]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=OtherExpenses]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', true).prop('hidden', true);

        $("#createFixedExpenditureSubClass option:selected").removeAttr("selected");

        $('#createFixedExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#createFixedExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', false).prop('hidden', false);
        $('#createFixedExpenditureSubClass option[value=EducationExpenses]').prop('disabled', false).prop('hidden', false);
        $('#createFixedExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', false).prop('hidden', false);
        $('#createFixedExpenditureSubClass option[value=TransportationCost]').prop('disabled', false).prop('hidden', false);
        $('#createFixedExpenditureSubClass option[value=CommunicationCost]').prop('disabled', false).prop('hidden', false);
        $('#createFixedExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', false).prop('hidden', false);
        $('#createFixedExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', false).prop('hidden', false);
        $('#createFixedExpenditureSubClass option[value=PinMoney]').prop('disabled', false).prop('hidden', false);
        $('#createFixedExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', false).prop('hidden', false);
        $('#createFixedExpenditureSubClass option[value=OtherExpenses]').prop('disabled', false).prop('hidden', false);
        $('#createFixedExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', false).prop('hidden', false);

        $("#createFixedExpenditureSubClass").val("MealOrEatOutExpenses").change();

        $("#divCreateFixedExpenditureMyDepositAsset").hide();
    }
}

function CreateFormShowFixedExpenditureDivCreateFixedExpenditureMyDepositAssetBySelectedFixedExpenditureSubClass(createFixedExpenditureSubClass) {

    if (createFixedExpenditureSubClass.value == "Deposit" ||
        createFixedExpenditureSubClass.value == "MyAssetTransfer" ||
        createFixedExpenditureSubClass.value == "Investment" ||
        createFixedExpenditureSubClass.value == "PublicPension" ||
        createFixedExpenditureSubClass.value == "DebtRepayment") {
        $("#divCreateFixedExpenditureMyDepositAsset").show();
    }
    else {
        $("#divCreateFixedExpenditureMyDepositAsset").hide();
    }
}

function CreateFormShowFixedExpenditureDepositDayBySelectedFixedExpenditureDepositMonth(createFixedExpenditureDepositMonth) {

    if (createFixedExpenditureDepositMonth.value == "1" ||
        createFixedExpenditureDepositMonth.value == "3" ||
        createFixedExpenditureDepositMonth.value == "5" ||
        createFixedExpenditureDepositMonth.value == "7" ||
        createFixedExpenditureDepositMonth.value == "8" ||
        createFixedExpenditureDepositMonth.value == "10" ||
        createFixedExpenditureDepositMonth.value == "12"
    ) {
        $('#createFixedExpenditureDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

        $("#createFixedExpenditureDepositDay option:selected").removeAttr("selected");

        $('#createFixedExpenditureDepositDay option[value=1]').attr('selected', 'selected');
        $('#createFixedExpenditureDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);
        $('#createFixedExpenditureDepositDay option[value=30]').prop('disabled', false).prop('hidden', false);
        $('#createFixedExpenditureDepositDay option[value=31]').prop('disabled', false).prop('hidden', false);

        $("#createFixedExpenditureDepositDay").val("1").change();
    }
    else if (createFixedExpenditureDepositMonth.value == "2") {
        $('#createFixedExpenditureDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

        $("#createFixedExpenditureDepositDay option:selected").removeAttr("selected");

        $('#createFixedExpenditureDepositDay option[value=1]').attr('selected', 'selected');
        $('#createFixedExpenditureDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);

        $("#createFixedExpenditureDepositDay").val("1").change();
    }
    else if (createFixedExpenditureDepositMonth.value == "4" ||
        createFixedExpenditureDepositMonth.value == "6" ||
        createFixedExpenditureDepositMonth.value == "9" ||
        createFixedExpenditureDepositMonth.value == "11"
    ) {
        $('#createFixedExpenditureDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
        $('#createFixedExpenditureDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

        $("#createFixedExpenditureDepositDay option:selected").removeAttr("selected");

        $('#createFixedExpenditureDepositDay option[value=1]').attr('selected', 'selected');
        $('#createFixedExpenditureDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);
        $('#createFixedExpenditureDepositDay option[value=30]').prop('disabled', false).prop('hidden', false);

        $("#createFixedExpenditureDepositDay").val("1").change();
    }
}

function EditFormShowFixedExpenditureSubClassBySelectedFixedExpenditureMainClass(editFixedExpenditureMainClass) {

    if (editFixedExpenditureMainClass.value == "RegularSavings") {
        $('#editFixedExpenditureSubClass option[value=Deposit]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=Investment]').prop('disabled', true).prop('hidden', true);

        $('#editFixedExpenditureSubClass option[value=PublicPension]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=DebtRepayment]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=Tax]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=SocialInsurance]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', true).prop('hidden', true);

        $('#editFixedExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=EducationExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=TransportationCost]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=CommunicationCost]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=PinMoney]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=OtherExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', true).prop('hidden', true);

        $("#editFixedExpenditureSubClass option:selected").removeAttr("selected");

        $('#editFixedExpenditureSubClass option[value=Deposit]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#editFixedExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', false).prop('hidden', false);
        $('#editFixedExpenditureSubClass option[value=Investment]').prop('disabled', false).prop('hidden', false);

        $("#editFixedExpenditureSubClass").val("Deposit").change();

        $("#divEditFixedExpenditureMyDepositAsset").show();
    }
    else if (editFixedExpenditureMainClass.value == "NonConsumerSpending") {
        $('#editFixedExpenditureSubClass option[value=Deposit]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=Investment]').prop('disabled', true).prop('hidden', true);

        $('#editFixedExpenditureSubClass option[value=PublicPension]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=DebtRepayment]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=Tax]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=SocialInsurance]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', true).prop('hidden', true);

        $('#editFixedExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=EducationExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=TransportationCost]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=CommunicationCost]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=PinMoney]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=OtherExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', true).prop('hidden', true);

        $("#editFixedExpenditureSubClass option:selected").removeAttr("selected");

        $('#editFixedExpenditureSubClass option[value=PublicPension]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#editFixedExpenditureSubClass option[value=DebtRepayment]').prop('disabled', false).prop('hidden', false);
        $('#editFixedExpenditureSubClass option[value=Tax]').prop('disabled', false).prop('hidden', false);
        $('#editFixedExpenditureSubClass option[value=SocialInsurance]').prop('disabled', false).prop('hidden', false);
        $('#editFixedExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', false).prop('hidden', false);
        $('#editFixedExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', false).prop('hidden', false);

        $("#editFixedExpenditureSubClass").val("PublicPension").change();

        $("#divEditFixedExpenditureMyDepositAsset").show();
    }
    else if (editFixedExpenditureMainClass.value == "ConsumerSpending") {
        $('#editFixedExpenditureSubClass option[value=Deposit]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=Investment]').prop('disabled', true).prop('hidden', true);

        $('#editFixedExpenditureSubClass option[value=PublicPension]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=DebtRepayment]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=Tax]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=SocialInsurance]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', true).prop('hidden', true);

        $('#editFixedExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=EducationExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=TransportationCost]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=CommunicationCost]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=PinMoney]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=OtherExpenses]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', true).prop('hidden', true);

        $("#editFixedExpenditureSubClass option:selected").removeAttr("selected");

        $('#editFixedExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#editFixedExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', false).prop('hidden', false);
        $('#editFixedExpenditureSubClass option[value=EducationExpenses]').prop('disabled', false).prop('hidden', false);
        $('#editFixedExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', false).prop('hidden', false);
        $('#editFixedExpenditureSubClass option[value=TransportationCost]').prop('disabled', false).prop('hidden', false);
        $('#editFixedExpenditureSubClass option[value=CommunicationCost]').prop('disabled', false).prop('hidden', false);
        $('#editFixedExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', false).prop('hidden', false);
        $('#editFixedExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', false).prop('hidden', false);
        $('#editFixedExpenditureSubClass option[value=PinMoney]').prop('disabled', false).prop('hidden', false);
        $('#editFixedExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', false).prop('hidden', false);
        $('#editFixedExpenditureSubClass option[value=OtherExpenses]').prop('disabled', false).prop('hidden', false);
        $('#editFixedExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', false).prop('hidden', false);

        $("#editFixedExpenditureSubClass").val("MealOrEatOutExpenses").change();

        $("#divEditFixedExpenditureMyDepositAsset").hide();
    }
}

function EditFormShowFixedExpenditureDivCreateFixedExpenditureMyDepositAssetBySelectedFixedExpenditureSubClass(editFixedExpenditureSubClass) {

    if (editFixedExpenditureSubClass.value == "Deposit" ||
        editFixedExpenditureSubClass.value == "MyAssetTransfer" ||
        editFixedExpenditureSubClass.value == "Investment" ||
        editFixedExpenditureSubClass.value == "PublicPension" ||
        editFixedExpenditureSubClass.value == "DebtRepayment") {
        $("#divEditFixedExpenditureMyDepositAsset").show();
    }
    else {
        $("#divEditFixedExpenditureMyDepositAsset").hide();
    }
}

function EditFormShowFixedExpenditureDepositDayBySelectedFixedExpenditureDepositMonth(editFixedExpenditureDepositMonth) {

    if (editFixedExpenditureDepositMonth.value == "1" ||
        editFixedExpenditureDepositMonth.value == "3" ||
        editFixedExpenditureDepositMonth.value == "5" ||
        editFixedExpenditureDepositMonth.value == "7" ||
        editFixedExpenditureDepositMonth.value == "8" ||
        editFixedExpenditureDepositMonth.value == "10" ||
        editFixedExpenditureDepositMonth.value == "12"
    ) {
        $('#editFixedExpenditureDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

        $("#editFixedExpenditureDepositDay option:selected").removeAttr("selected");

        $('#editFixedExpenditureDepositDay option[value=1]').attr('selected', 'selected');
        $('#editFixedExpenditureDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);
        $('#editFixedExpenditureDepositDay option[value=30]').prop('disabled', false).prop('hidden', false);
        $('#editFixedExpenditureDepositDay option[value=31]').prop('disabled', false).prop('hidden', false);

        $("#editFixedExpenditureDepositDay").val("1").change();
    }
    else if (editFixedExpenditureDepositMonth.value == "2") {
        $('#editFixedExpenditureDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

        $("#editFixedExpenditureDepositDay option:selected").removeAttr("selected");

        $('#editFixedExpenditureDepositDay option[value=1]').attr('selected', 'selected');
        $('#editFixedExpenditureDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);

        $("#editFixedExpenditureDepositDay").val("1").change();
    }
    else if (editFixedExpenditureDepositMonth.value == "4" ||
        editFixedExpenditureDepositMonth.value == "6" ||
        editFixedExpenditureDepositMonth.value == "9" ||
        editFixedExpenditureDepositMonth.value == "11"
    ) {
        $('#editFixedExpenditureDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
        $('#editFixedExpenditureDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

        $("#editFixedExpenditureDepositDay option:selected").removeAttr("selected");

        $('#editFixedExpenditureDepositDay option[value=1]').attr('selected', 'selected');
        $('#editFixedExpenditureDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);
        $('#editFixedExpenditureDepositDay option[value=30]').prop('disabled', false).prop('hidden', false);

        $("#editFixedExpenditureDepositDay").val("1").change();
    }
}

function CreateFixedExpenditure() {

    if (!$('#formCreateFixedExpenditure').valid()) {
        return false;
    }

    var createForm = $('#formCreateFixedExpenditure');

    var mainClass = createForm.find('select[id="createFixedExpenditureMainClass"]').val();
    var subClass = createForm.find('select[id="createFixedExpenditureSubClass"]').val();
    var contents = createForm.find('input[id="createFixedExpenditureContents"]').val();
    var amount = createForm.find('input[id="createFixedExpenditureAmount"]').val();
    var depositMonth = createForm.find('select[id="createFixedExpenditureDepositMonth"]').val();
    var depositDay = createForm.find('select[id="createFixedExpenditureDepositDay"]').val();
    var maturityDate = createForm.find('input[id="createFixedExpenditureMaturityDate"]').val();
    var note = createForm.find('input[id="createFixedExpenditureNote"]').val();
    var paymentMethod = createForm.find('select[id="createFixedExpenditurePaymentMethod"]').val();
    var myDepositAsset = createForm.find('select[id="createFixedExpenditureMyDepositAsset"]').val();

    var paramValue = JSON.stringify({
        MainClass: mainClass,
        SubClass: subClass,
        Contents: contents,
        Amount: amount,
        DepositMonth: depositMonth,
        DepositDay: depositDay,
        MaturityDate: maturityDate,
        Note: note,
        PaymentMethod: paymentMethod,
        MyDepositAsset: myDepositAsset
    });

    $.ajax({
        url: '/Notice/CreateFixedExpenditure',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        //async: false,
        //cache: false,
        success: function (data) {
            if (data.result) {
                $('#createFixedExpenditureDialogModal').modal('hide'); // hide Modal

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

function EditFixedExpenditureGridRow(errorMessageSelectGridRow) {

    var selectedRowId = "";

    $('#fixedExpenditureGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) { // Get SelectedRow by Color
            selectedRowId = $(this).attr("data-id");
        }
    }); // Search Selected Row

    if (selectedRowId === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    } // check whether selected row exist or not

    $.ajax({
        url: '/Notice/IsFixedExpenditureExists' + '?id=' + selectedRowId,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        //async: false,
        //cache: false,
        success: function (data) {
            if (data.result) {

                var formEditFixedExpenditure = $('#formEditFixedExpenditure');

                formEditFixedExpenditure.find('input[id="editFixedExpenditureId"]').val(data.fixedExpenditure.id);
                formEditFixedExpenditure.find('select[id="editFixedExpenditureMainClass"]').val(data.fixedExpenditure.mainClass).change();

                if (data.fixedExpenditure.mainClass == "RegularSavings") {
                    $('#editFixedExpenditureSubClass option[value=Deposit]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=Investment]').prop('disabled', true).prop('hidden', true);

                    $('#editFixedExpenditureSubClass option[value=PublicPension]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=DebtRepayment]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=Tax]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=SocialInsurance]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', true).prop('hidden', true);

                    $('#editFixedExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=EducationExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=TransportationCost]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=CommunicationCost]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=PinMoney]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=OtherExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', true).prop('hidden', true);

                    $("#editFixedExpenditureSubClass option:selected").removeAttr("selected");

                    $('#editFixedExpenditureSubClass option[value=Deposit]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureSubClass option[value=Investment]').prop('disabled', false).prop('hidden', false);

                    formEditFixedExpenditure.find('select[id="editFixedExpenditureSubClass"]').val(data.fixedExpenditure.subClass).change();
                }
                else if (data.fixedExpenditure.mainClass == "NonConsumerSpending") {
                    $('#editFixedExpenditureSubClass option[value=Deposit]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=Investment]').prop('disabled', true).prop('hidden', true);

                    $('#editFixedExpenditureSubClass option[value=PublicPension]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=DebtRepayment]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=Tax]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=SocialInsurance]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', true).prop('hidden', true);

                    $('#editFixedExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=EducationExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=TransportationCost]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=CommunicationCost]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=PinMoney]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=OtherExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', true).prop('hidden', true);

                    $("#editFixedExpenditureSubClass option:selected").removeAttr("selected");

                    $('#editFixedExpenditureSubClass option[value=PublicPension]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureSubClass option[value=DebtRepayment]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureSubClass option[value=Tax]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureSubClass option[value=SocialInsurance]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', false).prop('hidden', false);

                    formEditFixedExpenditure.find('select[id="editFixedExpenditureSubClass"]').val(data.fixedExpenditure.subClass).change();
                }
                else if (data.fixedExpenditure.mainClass == "ConsumerSpending") {
                    $('#editFixedExpenditureSubClass option[value=Deposit]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=MyAssetTransfer]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=Investment]').prop('disabled', true).prop('hidden', true);

                    $('#editFixedExpenditureSubClass option[value=PublicPension]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=DebtRepayment]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=Tax]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=SocialInsurance]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=InterHouseholdTranserExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=NonProfitOrganizationTransfer]').prop('disabled', true).prop('hidden', true);

                    $('#editFixedExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=EducationExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=TransportationCost]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=CommunicationCost]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=PinMoney]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=OtherExpenses]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', true).prop('hidden', true);

                    $("#editFixedExpenditureSubClass option:selected").removeAttr("selected");

                    $('#editFixedExpenditureSubClass option[value=MealOrEatOutExpenses]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureSubClass option[value=HousingOrSuppliesCost]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureSubClass option[value=EducationExpenses]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureSubClass option[value=MedicalExpenses]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureSubClass option[value=TransportationCost]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureSubClass option[value=CommunicationCost]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureSubClass option[value=LeisureOrCulture]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureSubClass option[value=ClothingOrShoes]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureSubClass option[value=PinMoney]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureSubClass option[value=ProtectionTypeInsurance]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureSubClass option[value=OtherExpenses]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureSubClass option[value=UnknownExpenditure]').prop('disabled', false).prop('hidden', false);

                    formEditFixedExpenditure.find('select[id="editFixedExpenditureSubClass"]').val(data.fixedExpenditure.subClass).change();
                }

                formEditFixedExpenditure.find('input[id="editFixedExpenditureContents"]').val(data.fixedExpenditure.contents);
                formEditFixedExpenditure.find('input[id="editFixedExpenditureAmount"]').val(data.fixedExpenditure.amount);

                formEditFixedExpenditure.find('select[id="editFixedExpenditureDepositMonth"]').val(data.fixedExpenditure.depositMonth.toString()).change();

                if (data.fixedExpenditure.depositMonth.toString() == "1" ||
                    data.fixedExpenditure.depositMonth.toString() == "3" ||
                    data.fixedExpenditure.depositMonth.toString() == "5" ||
                    data.fixedExpenditure.depositMonth.toString() == "7" ||
                    data.fixedExpenditure.depositMonth.toString() == "8" ||
                    data.fixedExpenditure.depositMonth.toString() == "10" ||
                    data.fixedExpenditure.depositMonth.toString() == "12"
                ) {
                    $('#editFixedExpenditureDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

                    $("#editFixedExpenditureDepositDay option:selected").removeAttr("selected");

                    $('#editFixedExpenditureDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureDepositDay option[value=30]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureDepositDay option[value=31]').prop('disabled', false).prop('hidden', false);

                    formEditFixedExpenditure.find('select[id="editFixedExpenditureDepositDay"]').val(data.fixedExpenditure.depositDay.toString()).change();
                }
                else if (data.fixedExpenditure.depositMonth.toString() == "2") {
                    $('#editfixedExpenditureDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
                    $('#editfixedExpenditureDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
                    $('#editfixedExpenditureDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

                    $("#editfixedExpenditureDepositDay option:selected").removeAttr("selected");

                    $('#editfixedExpenditureDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);

                    formEditFixedExpenditure.find('select[id="editFixedExpenditureDepositDay"]').val(data.fixedExpenditure.depositDay.toString()).change();
                }
                else if (data.fixedExpenditure.depositMonth.toString() == "4" ||
                    data.fixedExpenditure.depositMonth.toString() == "6" ||
                    data.fixedExpenditure.depositMonth.toString() == "9" ||
                    data.fixedExpenditure.depositMonth.toString() == "11"
                ) {
                    $('#editFixedExpenditureDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedExpenditureDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

                    $("#editFixedExpenditureDepositDay option:selected").removeAttr("selected");

                    $('#editFixedExpenditureDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedExpenditureDepositDay option[value=30]').prop('disabled', false).prop('hidden', false);

                    formEditFixedExpenditure.find('select[id="editFixedExpenditureDepositDay"]').val(data.fixedExpenditure.depositDay.toString()).change();
                }

                formEditFixedExpenditure.find('input[id="editFixedExpenditureMaturityDate"]').val(data.fixedExpenditure.maturityDate);
                formEditFixedExpenditure.find('input[id="editFixedExpenditureNote"]').val(data.fixedExpenditure.note);
                formEditFixedExpenditure.find('select[id="editFixedExpenditurePaymentMethod"]').val(data.fixedExpenditure.paymentMethod).change();

                if (data.fixedExpenditure.myDepositAsset) {
                    formEditFixedExpenditure.find('select[id="editFixedExpenditureMyDepositAsset"]').val(data.fixedExpenditure.myDepositAsset).change();
                }
                else {
                    formEditFixedExpenditure.find('select[id="editFixedExpenditureMyDepositAsset"]').val(data.fixedExpenditure.paymentMethod).change();
                }

                if (data.fixedExpenditure.subClass == "Deposit" ||
                    data.fixedExpenditure.subClass == "MyAssetTransfer" ||
                    data.fixedExpenditure.subClass == "Investment" ||
                    data.fixedExpenditure.subClass == "PublicPension" ||
                    data.fixedExpenditure.subClass == "DebtRepayment") {
                    $("#divEditFixedExpenditureMyDepositAsset").show();
                }
                else {
                    $("#divEditFixedExpenditureMyDepositAsset").hide();
                }

                $('#editFixedExpenditureDialogModal').modal({
                    keyboard: false,
                    backdrop: "static"
                });

                $('#editFixedExpenditureDialogModal').modal('toggle');
                $('#editFixedExpenditureDialogModal').modal('show');
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

function UpdateFixedExpenditure() {

    if (!$('#formEditFixedExpenditure').valid()) {
        return false;
    }

    var createForm = $('#formEditFixedExpenditure');

    var id = createForm.find('input[id="editFixedExpenditureId"]').val();
    var mainClass = createForm.find('select[id="editFixedExpenditureMainClass"]').val();
    var subClass = createForm.find('select[id="editFixedExpenditureSubClass"]').val();
    var contents = createForm.find('input[id="editFixedExpenditureContents"]').val();
    var amount = createForm.find('input[id="editFixedExpenditureAmount"]').val();
    var depositMonth = createForm.find('select[id="editFixedExpenditureDepositMonth"]').val();
    var depositDay = createForm.find('select[id="editFixedExpenditureDepositDay"]').val();
    var maturityDate = createForm.find('input[id="editFixedExpenditureMaturityDate"]').val();
    var note = createForm.find('input[id="editFixedExpenditureNote"]').val();
    var paymentMethod = createForm.find('select[id="editFixedExpenditurePaymentMethod"]').val();
    var myDepositAsset = createForm.find('select[id="editFixedExpenditureMyDepositAsset"]').val();

    var paramValue = JSON.stringify({
        Id: id,
        MainClass: mainClass,
        SubClass: subClass,
        Contents: contents,
        Amount: amount,
        DepositMonth: depositMonth,
        DepositDay: depositDay,
        MaturityDate: maturityDate,
        Note: note,
        PaymentMethod: paymentMethod,
        MyDepositAsset: myDepositAsset
    });

    $.ajax({
        url: '/Notice/UpdateFixedExpenditure',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        //async: false,
        //cache: false,
        success: function (data) {
            if (data.result) {
                $('#editFixedExpenditureDialogModal').modal('hide'); // hide Modal

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

function ConfirmDeleteFixedExpenditure(errorMessageSelectGridRow) {

    var selectedRowId = "";

    $('#fixedExpenditureGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) { // Get SelectedRow by Color
            selectedRowId = $(this).attr("data-id");
        }
    }); // Search Selected Row

    if (selectedRowId === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    } // check whether selected row exist or not

    $('#confirmDeleteFixedExpenditureDialogModal').modal({
        keyboard: false,
        backdrop: "static"
    });

    $('#confirmDeleteFixedExpenditureDialogModal').modal('toggle');
    $('#confirmDeleteFixedExpenditureDialogModal').modal('show');
}

function DeleteFixedExpenditure(errorMessageSelectGridRow) {

    var selectedRowId = "";

    $('#fixedExpenditureGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) { // Get SelectedRow by Color
            selectedRowId = $(this).attr("data-id");
        }
    }); // Search Selected Row

    if (selectedRowId === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    } // check whether selected row exist or not

    $.ajax({
        url: '/Notice/IsFixedExpenditureExists' + '?id=' + selectedRowId,
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
                    Id: data.fixedExpenditure.id
                });

                $.ajax({
                    url: '/Notice/DeleteFixedExpenditure',
                    type: 'POST',
                    headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                    dataType: 'json',
                    data: paramValue,
                    contentType: 'application/json; charset=utf-8',
                    //async: false,
                    //cache: false,
                    success: function (data) {
                        if (data.result) {
                            $('#confirmDeleteFixedExpenditureDialogModal').modal('hide'); // hide Modal

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

function ExportExcelFixedExpenditure() {
    var form = document.createElement("form");
    var element1 = document.createElement("input");
    var element2 = document.createElement("input");

    form.method = "POST";
    form.action = "/Notice/ExportExcelFixedExpenditure";

    element1.name = "__RequestVerificationToken";
    element1.value = $('input[name="__RequestVerificationToken"]').val();
    form.appendChild(element1);

    element2.name = "fileName";
    element2.value = "FixedExpenditure";
    form.appendChild(element2);

    document.body.appendChild(form);

    form.submit();
}
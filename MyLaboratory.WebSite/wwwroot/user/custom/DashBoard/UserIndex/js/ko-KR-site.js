$(function () {
    var regularIncomeLaborIncome = parseInt($("#regularIncomeLaborIncome").val());
    var regularIncomeBusinessIncome = parseInt($("#regularIncomeBusinessIncome").val());
    var regularIncomePensionIncome = parseInt($("#regularIncomePensionIncome").val());
    var regularIncomeFinancialIncome = parseInt($("#regularIncomeFinancialIncome").val());
    var regularIncomeRentalIncome = parseInt($("#regularIncomeRentalIncome").val());
    var regularIncomeOtherIncome = parseInt($("#regularIncomeOtherIncome").val());

    var donutChartCanvasRegularIncome = $('#regularIncome').get(0).getContext('2d')
    var donutDataRegularIncome = {
        labels: [
            '근로수입',
            '사업수입',
            '연금수입',
            '금융소득',
            '임대수입',
            '기타수입',
        ],
        datasets: [
            {
                data: [regularIncomeLaborIncome, regularIncomeBusinessIncome, regularIncomePensionIncome, regularIncomeFinancialIncome, regularIncomeRentalIncome, regularIncomeOtherIncome],
                backgroundColor: ['#f56954', '#00a65a', '#f39c12', '#00c0ef', '#3c8dbc', '#d2d6de'],
            }
        ]
    }
    var donutOptionsRegularIncome = {
        maintainAspectRatio: false,
        responsive: true,
    }

    new Chart(donutChartCanvasRegularIncome, {
        type: 'doughnut',
        data: donutDataRegularIncome,
        options: donutOptionsRegularIncome
    })



    var irregularIncomeLaborIncome = parseInt($("#irregularIncomeLaborIncome").val());
    var irregularIncomeOtherIncome = parseInt($("#irregularIncomeOtherIncome").val());

    var donutChartCanvasIrregularIncome = $('#irregularIncome').get(0).getContext('2d')
    var donutDataIrregularIncome = {
        labels: [
            '근로수입',
            '기타수입',
        ],
        datasets: [
            {
                data: [irregularIncomeLaborIncome, irregularIncomeOtherIncome],
                backgroundColor: ['#f56954', '#00a65a'],
            }
        ]
    }
    var donutOptionsIrregularIncome = {
        maintainAspectRatio: false,
        responsive: true,
    }

    new Chart(donutChartCanvasIrregularIncome, {
        type: 'doughnut',
        data: donutDataIrregularIncome,
        options: donutOptionsIrregularIncome
    })



    var regularSavingsDeposit = parseInt($("#regularSavingsDeposit").val());
    var regularSavingsInvestment = parseInt($("#regularSavingsInvestment").val());

    var donutChartCanvasRegularSavings = $('#regularSavings').get(0).getContext('2d')
    var donutDataRegularSavings = {
        labels: [
            '예적금',
            '투자',
        ],
        datasets: [
            {
                data: [regularSavingsDeposit, regularSavingsInvestment],
                backgroundColor: ['#f56954', '#00a65a'],
            }
        ]
    }
    var donutOptionsRegularSavings = {
        maintainAspectRatio: false,
        responsive: true,
    }

    new Chart(donutChartCanvasRegularSavings, {
        type: 'doughnut',
        data: donutDataRegularSavings,
        options: donutOptionsRegularSavings
    })



    var nonConsumerSpendingPublicPension = parseInt($("#nonConsumerSpendingPublicPension").val());
    var nonConsumerSpendingDebtRepayment = parseInt($("#nonConsumerSpendingDebtRepayment").val());
    var nonConsumerSpendingTax = parseInt($("#nonConsumerSpendingTax").val());
    var nonConsumerSpendingSocialInsurance = parseInt($("#nonConsumerSpendingSocialInsurance").val());
    var nonConsumerSpendingInterHouseholdTranserExpenses = parseInt($("#nonConsumerSpendingInterHouseholdTranserExpenses").val());
    var nonConsumerSpendingNonProfitOrganizationTransfer = parseInt($("#nonConsumerSpendingNonProfitOrganizationTransfer").val());

    var donutChartCanvasNonConsumerSpending = $('#nonConsumerSpending').get(0).getContext('2d')
    var donutDataNonConsumerSpending = {
        labels: [
            '공적연금',
            '부채상환',
            '세금',
            '사회보험',
            '가구간 이전지출',
            '비영리단체 이전',
        ],
        datasets: [
            {
                data: [nonConsumerSpendingPublicPension, nonConsumerSpendingDebtRepayment, nonConsumerSpendingTax, nonConsumerSpendingSocialInsurance, nonConsumerSpendingInterHouseholdTranserExpenses, nonConsumerSpendingNonProfitOrganizationTransfer],
                backgroundColor: ['#f56954', '#00a65a', '#f39c12', '#00c0ef', '#3c8dbc', '#d2d6de'],
            }
        ]
    }
    var donutOptionsNonConsumerSpending = {
        maintainAspectRatio: false,
        responsive: true,
    }

    new Chart(donutChartCanvasNonConsumerSpending, {
        type: 'doughnut',
        data: donutDataNonConsumerSpending,
        options: donutOptionsNonConsumerSpending
    })



    var consumerSpendingMealOrEatOutExpenses = parseInt($("#consumerSpendingMealOrEatOutExpenses").val());
    var consumerSpendingHousingOrSuppliesCost = parseInt($("#consumerSpendingHousingOrSuppliesCost").val());
    var consumerSpendingEducationExpenses = parseInt($("#consumerSpendingEducationExpenses").val());
    var consumerSpendingMedicalExpenses = parseInt($("#consumerSpendingMedicalExpenses").val());
    var consumerSpendingTransportationCost = parseInt($("#consumerSpendingTransportationCost").val());
    var consumerSpendingCommunicationCost = parseInt($("#consumerSpendingCommunicationCost").val());
    var consumerSpendingLeisureOrCulture = parseInt($("#consumerSpendingLeisureOrCulture").val());
    var consumerSpendingClothingOrShoes = parseInt($("#consumerSpendingClothingOrShoes").val());
    var consumerSpendingPinMoney = parseInt($("#consumerSpendingPinMoney").val());
    var consumerSpendingProtectionTypeInsurance = parseInt($("#consumerSpendingProtectionTypeInsurance").val());
    var consumerSpendingOtherExpenses = parseInt($("#consumerSpendingOtherExpenses").val());
    var consumerSpendingUnknownExpenditure = parseInt($("#consumerSpendingUnknownExpenditure").val());

    var donutChartCanvasConsumerSpending = $('#consumerSpending').get(0).getContext('2d')
    var donutDataConsumerSpending = {
        labels: [
            '식비/외식비',
            '주거/용품비',
            '교육비',
            '의료비',
            '교통비',
            '통신비',
            '여가/문화',
            '의류/신발',
            '용돈',
            '보장성보험',
            '기타지출',
            '미파악지출',
        ],
        datasets: [
            {
                data: [consumerSpendingMealOrEatOutExpenses, consumerSpendingHousingOrSuppliesCost, consumerSpendingEducationExpenses, consumerSpendingMedicalExpenses, consumerSpendingTransportationCost, consumerSpendingCommunicationCost, consumerSpendingLeisureOrCulture, consumerSpendingClothingOrShoes, consumerSpendingPinMoney, consumerSpendingProtectionTypeInsurance, consumerSpendingOtherExpenses, consumerSpendingUnknownExpenditure],
                backgroundColor: ['#FF0000', '#FFA200', '#FBFF00', '#70FF00', '#00FFE0', '#003EFF', '#D500FF', '#813F3F', '#7D813F', '#3F815A', '#3F8180', '#3F4B81'],
            }
        ]
    }
    var donutOptionsConsumerSpending = {
        maintainAspectRatio: false,
        responsive: true,
    }

    new Chart(donutChartCanvasConsumerSpending, {
        type: 'doughnut',
        data: donutDataConsumerSpending,
        options: donutOptionsConsumerSpending
    })
})
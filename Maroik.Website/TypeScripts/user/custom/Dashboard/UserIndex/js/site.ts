/**
 * Script for the user **Dashboard** (`Views/Dashboard/UserIndex.cshtml`).
 *
 * It does two things:
 *
 *   1. Period / currency pickers — changing the year, month, or monetary-unit
 *      select reloads the dashboard for that period (the monetary-unit change
 *      first POSTs the new default so the server remembers it).
 *   2. Draws **10 Chart.js doughnut charts** — five spending/income categories,
 *      each shown for the whole year and for the selected month:
 *
 *        regular income · irregular income · regular savings ·
 *        non-consumer spending · consumer spending          × { Year, YearMonth }
 *
 * All the numbers are computed on the server and rendered into hidden inputs
 * (`#<seriesName>Year` for the slice value, `#percentageOf<seriesName>Year` for
 * its share). This script only reads those inputs and plots them — no maths
 * beyond `Number(...)` and `toLocaleString()` for display.
 *
 * Every chart block has the identical shape (see the first one, which is fully
 * commented); the rest carry only a one-line marker. The data / options objects
 * are typed with Chart.js 2's `Chart.ChartData` / `Chart.ChartOptions`.
 * IIFE-wrapped, no `import` / `export`.
 */
(function() {
    // The runtime-check helpers the _Layout script defines (see TypeScripts/global.d.ts).
    const { check, onReply, fieldValue } = window;

    // The replies this page reads, mirroring the controllers' Json(...) results (see window.replies).
    const resultReply = check.object({ result: check.boolean });
    // Localized category names, read from hidden inputs the view rendered from
    // the resource files. Used as the chart slice labels below.
    const localizer = {
        LaborIncome: $("#localizerLaborIncome").val(),
        BusinessIncome: $("#localizerBusinessIncome").val(),
        PensionIncome: $("#localizerPensionIncome").val(),
        FinancialIncome: $("#localizerFinancialIncome").val(),
        RentalIncome: $("#localizerRentalIncome").val(),
        OtherIncome: $("#localizerOtherIncome").val(),
        Deposit: $("#localizerDeposit").val(),
        Investment: $("#localizerInvestment").val(),
        PublicPension: $("#localizerPublicPension").val(),
        DebtRepayment: $("#localizerDebtRepayment").val(),
        Tax: $("#localizerTax").val(),
        SocialInsurance: $("#localizerSocialInsurance").val(),
        InterHouseholdTransferExpenses: $("#localizerInterHouseholdTransferExpenses").val(),
        NonProfitOrganizationTransfer: $("#localizerNonProfitOrganizationTransfer").val(),
        MealOrEatOutExpenses: $("#localizerMealOrEatOutExpenses").val(),
        HousingOrSuppliesCost: $("#localizerHousingOrSuppliesCost").val(),
        EducationExpenses: $("#localizerEducationExpenses").val(),
        MedicalExpenses: $("#localizerMedicalExpenses").val(),
        TransportationCost: $("#localizerTransportationCost").val(),
        CommunicationCost: $("#localizerCommunicationCost").val(),
        LeisureOrCulture: $("#localizerLeisureOrCulture").val(),
        ClothingOrShoes: $("#localizerClothingOrShoes").val(),
        PinMoney: $("#localizerPinMoney").val(),
        ProtectionTypeInsurance: $("#localizerProtectionTypeInsurance").val(),
        OtherExpenses: $("#localizerOtherExpenses").val(),
        UnknownExpenditure: $("#localizerUnknownExpenditure").val()
    } as Record<string, string>;

    // --- Period / currency pickers -------------------------------------
    // Year / month changes just reload the dashboard for the new period.
    $("#year").off("change").on("change", function() {
        window.location.href = "/Dashboard/UserIndex?year=" + $("#year").find(":selected").val() + "&month=" + $("#month").find(":selected").val();
    });

    $("#month").off("change").on("change", function() {
        window.location.href = "/Dashboard/UserIndex?year=" + $("#year").find(":selected").val() + "&month=" + $("#month").find(":selected").val();
    });

    // Monetary-unit change first persists the choice, then reloads. Both `if`
    // branches reload, so a failed save just re-renders in the old currency.
    $("#monetaryUnit").off("change").on("change", function() {
        $.ajax({
            url: "/Dashboard/UserUpdateDefaultMonetary",
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($("input[name=\"__RequestVerificationToken\"]")) },
            dataType: "json",
            data: JSON.stringify({
                DefaultMonetaryUnit: $("#monetaryUnit").find(":selected").val()
            }),
            contentType: "application/json; charset=utf-8",
            success: onReply(resultReply, function(data) {
                if (data.result) {
                    window.location.href = "/Dashboard/UserIndex?year=" + $("#year").find(":selected").val() + "&month=" + $("#month").find(":selected").val();
                } else {
                    window.location.href = "/Dashboard/UserIndex?year=" + $("#year").find(":selected").val() + "&month=" + $("#month").find(":selected").val();
                }
            })
        });
    });

    // --- Charts (drawn once the DOM is ready) -------------------------
    $(function() {
        // ===== Chart 1/10: regular income, whole year =====
        // 1. Read each slice's value from its hidden input.
        let regularIncomeLaborIncomeYear = Number($("#regularIncomeLaborIncomeYear").val());
        let regularIncomeBusinessIncomeYear = Number($("#regularIncomeBusinessIncomeYear").val());
        let regularIncomePensionIncomeYear = Number($("#regularIncomePensionIncomeYear").val());
        let regularIncomeFinancialIncomeYear = Number($("#regularIncomeFinancialIncomeYear").val());
        let regularIncomeRentalIncomeYear = Number($("#regularIncomeRentalIncomeYear").val());
        let regularIncomeOtherIncomeYear = Number($("#regularIncomeOtherIncomeYear").val());

        // 2. Grab the <canvas> 2D context and build the Chart.js data object
        //    (localized labels + one dataset of the values, with fixed colors).
        let donutChartCanvasRegularIncomeYear = ($("#regularIncomeYear").get(0) as HTMLCanvasElement).getContext("2d")!;
        let donutDataRegularIncomeYear: Chart.ChartData = {
            labels: [
                localizer.LaborIncome,
                localizer.BusinessIncome,
                localizer.PensionIncome,
                localizer.FinancialIncome,
                localizer.RentalIncome,
                localizer.OtherIncome,
            ],
            datasets: [
                {
                    data: [regularIncomeLaborIncomeYear, regularIncomeBusinessIncomeYear, regularIncomePensionIncomeYear, regularIncomeFinancialIncomeYear, regularIncomeRentalIncomeYear, regularIncomeOtherIncomeYear],
                    backgroundColor: ["#f56954", "#00a65a", "#f39c12", "#00c0ef", "#3c8dbc", "#d2d6de"],
                }
            ]
        };

        // 3. Options: white legend text; the tooltip `label` callback formats
        //    each slice as "Label: 1,234 (12.34%)" using the server-supplied
        //    percentage for that slice index.
        let donutOptionsRegularIncomeYear: Chart.ChartOptions = {
            maintainAspectRatio: false,
            responsive: true,
            legend: {
                labels: {
                    fontColor: "#fff",
                    fontSize: 15
                }
            },
            tooltips: {
                callbacks: {
                    label: function(tooltipItem: Chart.ChartTooltipItem, data: Chart.ChartData) {
                        const index = tooltipItem.index!;
                        const label = data.labels![index];
                        const value = Number(data.datasets![0].data![index]);

                        const percentages = [
                            Number($("#percentageOfRegularIncomeLaborIncomeYear").val()),
                            Number($("#percentageOfRegularIncomeBusinessIncomeYear").val()),
                            Number($("#percentageOfRegularIncomePensionIncomeYear").val()),
                            Number($("#percentageOfRegularIncomeFinancialIncomeYear").val()),
                            Number($("#percentageOfRegularIncomeRentalIncomeYear").val()),
                            Number($("#percentageOfRegularIncomeOtherIncomeYear").val())
                        ];

                        return `${label}: ${value.toLocaleString()} (${percentages[index].toFixed(2)}%)`;
                    }
                }
            }
        };

        // 4. Draw it.
        new Chart(donutChartCanvasRegularIncomeYear, {
            type: "doughnut",
            data: donutDataRegularIncomeYear,
            options: donutOptionsRegularIncomeYear
        });

        // ===== Chart 2/10: irregular income, whole year (same shape as chart 1) =====
        let irregularIncomeLaborIncomeYear = Number($("#irregularIncomeLaborIncomeYear").val());
        let irregularIncomeOtherIncomeYear = Number($("#irregularIncomeOtherIncomeYear").val());

        let donutChartCanvasIrregularIncomeYear = ($("#irregularIncomeYear").get(0) as HTMLCanvasElement).getContext("2d")!;
        let donutDataIrregularIncomeYear: Chart.ChartData = {
            labels: [
                localizer.LaborIncome,
                localizer.OtherIncome,
            ],
            datasets: [
                {
                    data: [irregularIncomeLaborIncomeYear, irregularIncomeOtherIncomeYear],
                    backgroundColor: ["#f56954", "#00a65a"],
                }
            ]
        };

        let donutOptionsIrregularIncomeYear: Chart.ChartOptions = {
            maintainAspectRatio: false,
            responsive: true,
            legend: {
                labels: {
                    fontColor: "#fff",
                    fontSize: 15
                }
            },
            tooltips: {
                callbacks: {
                    label: function(tooltipItem: Chart.ChartTooltipItem, data: Chart.ChartData) {
                        const index = tooltipItem.index!;
                        const label = data.labels![index];
                        const value = Number(data.datasets![0].data![index]);

                        const percentages = [
                            Number($("#percentageOfIrregularIncomeLaborIncomeYear").val()),
                            Number($("#percentageOfIrregularIncomeOtherIncomeYear").val())
                        ];

                        return `${label}: ${value.toLocaleString()} (${percentages[index].toFixed(2)}%)`;
                    }
                }
            }
        };

        new Chart(donutChartCanvasIrregularIncomeYear, {
            type: "doughnut",
            data: donutDataIrregularIncomeYear,
            options: donutOptionsIrregularIncomeYear
        });

        // ===== Chart 3/10: regular savings, whole year (same shape as chart 1) =====
        let regularSavingsDepositYear = Number($("#regularSavingsDepositYear").val());
        let regularSavingsInvestmentYear = Number($("#regularSavingsInvestmentYear").val());

        let donutChartCanvasRegularSavingsYear = ($("#regularSavingsYear").get(0) as HTMLCanvasElement).getContext("2d")!;
        let donutDataRegularSavingsYear: Chart.ChartData = {
            labels: [
                localizer.Deposit,
                localizer.Investment,
            ],
            datasets: [
                {
                    data: [regularSavingsDepositYear, regularSavingsInvestmentYear],
                    backgroundColor: ["#f56954", "#00a65a"],
                }
            ]
        };
        let donutOptionsRegularSavingsYear: Chart.ChartOptions = {
            maintainAspectRatio: false,
            responsive: true,
            legend: {
                labels: {
                    fontColor: "#fff",
                    fontSize: 15
                }
            },
            tooltips: {
                callbacks: {
                    label: function(tooltipItem: Chart.ChartTooltipItem, data: Chart.ChartData) {
                        const index = tooltipItem.index!;
                        const label = data.labels![index];
                        const value = Number(data.datasets![0].data![index]);

                        const percentages = [
                            Number($("#percentageOfRegularSavingsDepositYear").val()),
                            Number($("#percentageOfRegularSavingsInvestmentYear").val())
                        ];

                        return `${label}: ${value.toLocaleString()} (${percentages[index].toFixed(2)}%)`;
                    }
                }
            }
        };

        new Chart(donutChartCanvasRegularSavingsYear, {
            type: "doughnut",
            data: donutDataRegularSavingsYear,
            options: donutOptionsRegularSavingsYear
        });

        // ===== Chart 4/10: non-consumer spending, whole year (same shape as chart 1) =====
        let nonConsumerSpendingPublicPensionYear = Number($("#nonConsumerSpendingPublicPensionYear").val());
        let nonConsumerSpendingDebtRepaymentYear = Number($("#nonConsumerSpendingDebtRepaymentYear").val());
        let nonConsumerSpendingTaxYear = Number($("#nonConsumerSpendingTaxYear").val());
        let nonConsumerSpendingSocialInsuranceYear = Number($("#nonConsumerSpendingSocialInsuranceYear").val());
        let nonConsumerSpendingInterHouseholdTransferExpensesYear = Number($("#nonConsumerSpendingInterHouseholdTransferExpensesYear").val());
        let nonConsumerSpendingNonProfitOrganizationTransferYear = Number($("#nonConsumerSpendingNonProfitOrganizationTransferYear").val());

        let donutChartCanvasNonConsumerSpendingYear = ($("#nonConsumerSpendingYear").get(0) as HTMLCanvasElement).getContext("2d")!;
        let donutDataNonConsumerSpendingYear: Chart.ChartData = {
            labels: [
                localizer.PublicPension,
                localizer.DebtRepayment,
                localizer.Tax,
                localizer.SocialInsurance,
                localizer.InterHouseholdTransferExpenses,
                localizer.NonProfitOrganizationTransfer,
            ],
            datasets: [
                {
                    data: [nonConsumerSpendingPublicPensionYear, nonConsumerSpendingDebtRepaymentYear, nonConsumerSpendingTaxYear, nonConsumerSpendingSocialInsuranceYear, nonConsumerSpendingInterHouseholdTransferExpensesYear, nonConsumerSpendingNonProfitOrganizationTransferYear],
                    backgroundColor: ["#f56954", "#00a65a", "#f39c12", "#00c0ef", "#3c8dbc", "#d2d6de"],
                }
            ]
        };
        let donutOptionsNonConsumerSpendingYear: Chart.ChartOptions = {
            maintainAspectRatio: false,
            responsive: true,
            legend: {
                labels: {
                    fontColor: "#fff",
                    fontSize: 15
                }
            },
            tooltips: {
                callbacks: {
                    label: function(tooltipItem: Chart.ChartTooltipItem, data: Chart.ChartData) {
                        const index = tooltipItem.index!;
                        const label = data.labels![index];
                        const value = Number(data.datasets![0].data![index]);

                        const percentages = [
                            Number($("#percentageOfNonConsumerSpendingPublicPensionYear").val()),
                            Number($("#percentageOfNonConsumerSpendingDebtRepaymentYear").val()),
                            Number($("#percentageOfNonConsumerSpendingTaxYear").val()),
                            Number($("#percentageOfNonConsumerSpendingSocialInsuranceYear").val()),
                            Number($("#percentageOfNonConsumerSpendingInterHouseholdTransferExpensesYear").val()),
                            Number($("#percentageOfNonConsumerSpendingNonProfitOrganizationTransferYear").val())
                        ];

                        return `${label}: ${value.toLocaleString()} (${percentages[index].toFixed(2)}%)`;
                    }
                }
            }
        };

        new Chart(donutChartCanvasNonConsumerSpendingYear, {
            type: "doughnut",
            data: donutDataNonConsumerSpendingYear,
            options: donutOptionsNonConsumerSpendingYear
        });

        // ===== Chart 5/10: consumer spending, whole year (same shape as chart 1) =====
        let consumerSpendingMealOrEatOutExpensesYear = Number($("#consumerSpendingMealOrEatOutExpensesYear").val());
        let consumerSpendingHousingOrSuppliesCostYear = Number($("#consumerSpendingHousingOrSuppliesCostYear").val());
        let consumerSpendingEducationExpensesYear = Number($("#consumerSpendingEducationExpensesYear").val());
        let consumerSpendingMedicalExpensesYear = Number($("#consumerSpendingMedicalExpensesYear").val());
        let consumerSpendingTransportationCostYear = Number($("#consumerSpendingTransportationCostYear").val());
        let consumerSpendingCommunicationCostYear = Number($("#consumerSpendingCommunicationCostYear").val());
        let consumerSpendingLeisureOrCultureYear = Number($("#consumerSpendingLeisureOrCultureYear").val());
        let consumerSpendingClothingOrShoesYear = Number($("#consumerSpendingClothingOrShoesYear").val());
        let consumerSpendingPinMoneyYear = Number($("#consumerSpendingPinMoneyYear").val());
        let consumerSpendingProtectionTypeInsuranceYear = Number($("#consumerSpendingProtectionTypeInsuranceYear").val());
        let consumerSpendingOtherExpensesYear = Number($("#consumerSpendingOtherExpensesYear").val());
        let consumerSpendingUnknownExpenditureYear = Number($("#consumerSpendingUnknownExpenditureYear").val());

        let donutChartCanvasConsumerSpendingYear = ($("#consumerSpendingYear").get(0) as HTMLCanvasElement).getContext("2d")!;
        let donutDataConsumerSpendingYear: Chart.ChartData = {
            labels: [
                localizer.MealOrEatOutExpenses,
                localizer.HousingOrSuppliesCost,
                localizer.EducationExpenses,
                localizer.MedicalExpenses,
                localizer.TransportationCost,
                localizer.CommunicationCost,
                localizer.LeisureOrCulture,
                localizer.ClothingOrShoes,
                localizer.PinMoney,
                localizer.ProtectionTypeInsurance,
                localizer.OtherExpenses,
                localizer.UnknownExpenditure,
            ],
            datasets: [
                {
                    data: [consumerSpendingMealOrEatOutExpensesYear, consumerSpendingHousingOrSuppliesCostYear, consumerSpendingEducationExpensesYear, consumerSpendingMedicalExpensesYear, consumerSpendingTransportationCostYear, consumerSpendingCommunicationCostYear, consumerSpendingLeisureOrCultureYear, consumerSpendingClothingOrShoesYear, consumerSpendingPinMoneyYear, consumerSpendingProtectionTypeInsuranceYear, consumerSpendingOtherExpensesYear, consumerSpendingUnknownExpenditureYear],
                    backgroundColor: ["#FF0000", "#FFA200", "#FBFF00", "#70FF00", "#00FFE0", "#003EFF", "#D500FF", "#813F3F", "#7D813F", "#3F815A", "#3F8180", "#3F4B81"],
                }
            ]
        };
        let donutOptionsConsumerSpendingYear: Chart.ChartOptions = {
            maintainAspectRatio: false,
            responsive: true,
            legend: {
                labels: {
                    fontColor: "#fff",
                    fontSize: 15
                }
            },
            tooltips: {
                callbacks: {
                    label: function(tooltipItem: Chart.ChartTooltipItem, data: Chart.ChartData) {
                        const index = tooltipItem.index!;
                        const label = data.labels![index];
                        const value = Number(data.datasets![0].data![index]);

                        const percentages = [
                            Number($("#percentageOfConsumerSpendingMealOrEatOutExpensesYear").val()),
                            Number($("#percentageOfConsumerSpendingHousingOrSuppliesCostYear").val()),
                            Number($("#percentageOfConsumerSpendingEducationExpensesYear").val()),
                            Number($("#percentageOfConsumerSpendingMedicalExpensesYear").val()),
                            Number($("#percentageOfConsumerSpendingTransportationCostYear").val()),
                            Number($("#percentageOfConsumerSpendingCommunicationCostYear").val()),
                            Number($("#percentageOfConsumerSpendingLeisureOrCultureYear").val()),
                            Number($("#percentageOfConsumerSpendingClothingOrShoesYear").val()),
                            Number($("#percentageOfConsumerSpendingPinMoneyYear").val()),
                            Number($("#percentageOfConsumerSpendingProtectionTypeInsuranceYear").val()),
                            Number($("#percentageOfConsumerSpendingOtherExpensesYear").val()),
                            Number($("#percentageOfConsumerSpendingUnknownExpenditureYear").val())
                        ];

                        return `${label}: ${value.toLocaleString()} (${percentages[index].toFixed(2)}%)`;
                    }
                }
            }
        };

        new Chart(donutChartCanvasConsumerSpendingYear, {
            type: "doughnut",
            data: donutDataConsumerSpendingYear,
            options: donutOptionsConsumerSpendingYear
        });

        // ===== Chart 6/10: regular income, selected month (same shape as chart 1) =====
        let regularIncomeLaborIncomeYearMonth = Number($("#regularIncomeLaborIncomeYearMonth").val());
        let regularIncomeBusinessIncomeYearMonth = Number($("#regularIncomeBusinessIncomeYearMonth").val());
        let regularIncomePensionIncomeYearMonth = Number($("#regularIncomePensionIncomeYearMonth").val());
        let regularIncomeFinancialIncomeYearMonth = Number($("#regularIncomeFinancialIncomeYearMonth").val());
        let regularIncomeRentalIncomeYearMonth = Number($("#regularIncomeRentalIncomeYearMonth").val());
        let regularIncomeOtherIncomeYearMonth = Number($("#regularIncomeOtherIncomeYearMonth").val());

        let donutChartCanvasRegularIncomeYearMonth = ($("#regularIncomeYearMonth").get(0) as HTMLCanvasElement).getContext("2d")!;
        let donutDataRegularIncomeYearMonth: Chart.ChartData = {
            labels: [
                localizer.LaborIncome,
                localizer.BusinessIncome,
                localizer.PensionIncome,
                localizer.FinancialIncome,
                localizer.RentalIncome,
                localizer.OtherIncome,
            ],
            datasets: [
                {
                    data: [regularIncomeLaborIncomeYearMonth, regularIncomeBusinessIncomeYearMonth, regularIncomePensionIncomeYearMonth, regularIncomeFinancialIncomeYearMonth, regularIncomeRentalIncomeYearMonth, regularIncomeOtherIncomeYearMonth],
                    backgroundColor: ["#f56954", "#00a65a", "#f39c12", "#00c0ef", "#3c8dbc", "#d2d6de"],
                }
            ]
        };

        let donutOptionsRegularIncomeYearMonth: Chart.ChartOptions = {
            maintainAspectRatio: false,
            responsive: true,
            legend: {
                labels: {
                    fontColor: "#fff",
                    fontSize: 15
                }
            },
            tooltips: {
                callbacks: {
                    label: function(tooltipItem: Chart.ChartTooltipItem, data: Chart.ChartData) {
                        const index = tooltipItem.index!;
                        const label = data.labels![index];
                        const value = Number(data.datasets![0].data![index]);

                        const percentages = [
                            Number($("#percentageOfRegularIncomeLaborIncomeYearMonth").val()),
                            Number($("#percentageOfRegularIncomeBusinessIncomeYearMonth").val()),
                            Number($("#percentageOfRegularIncomePensionIncomeYearMonth").val()),
                            Number($("#percentageOfRegularIncomeFinancialIncomeYearMonth").val()),
                            Number($("#percentageOfRegularIncomeRentalIncomeYearMonth").val()),
                            Number($("#percentageOfRegularIncomeOtherIncomeYearMonth").val())
                        ];

                        return `${label}: ${value.toLocaleString()} (${percentages[index].toFixed(2)}%)`;
                    }
                }
            }
        };

        new Chart(donutChartCanvasRegularIncomeYearMonth, {
            type: "doughnut",
            data: donutDataRegularIncomeYearMonth,
            options: donutOptionsRegularIncomeYearMonth
        });

        // ===== Chart 7/10: irregular income, selected month (same shape as chart 1) =====
        let irregularIncomeLaborIncomeYearMonth = Number($("#irregularIncomeLaborIncomeYearMonth").val());
        let irregularIncomeOtherIncomeYearMonth = Number($("#irregularIncomeOtherIncomeYearMonth").val());

        let donutChartCanvasIrregularIncomeYearMonth = ($("#irregularIncomeYearMonth").get(0) as HTMLCanvasElement).getContext("2d")!;
        let donutDataIrregularIncomeYearMonth: Chart.ChartData = {
            labels: [
                localizer.LaborIncome,
                localizer.OtherIncome,
            ],
            datasets: [
                {
                    data: [irregularIncomeLaborIncomeYearMonth, irregularIncomeOtherIncomeYearMonth],
                    backgroundColor: ["#f56954", "#00a65a"],
                }
            ]
        };

        let donutOptionsIrregularIncomeYearMonth: Chart.ChartOptions = {
            maintainAspectRatio: false,
            responsive: true,
            legend: {
                labels: {
                    fontColor: "#fff",
                    fontSize: 15
                }
            },
            tooltips: {
                callbacks: {
                    label: function(tooltipItem: Chart.ChartTooltipItem, data: Chart.ChartData) {
                        const index = tooltipItem.index!;
                        const label = data.labels![index];
                        const value = Number(data.datasets![0].data![index]);

                        const percentages = [
                            Number($("#percentageOfIrregularIncomeLaborIncomeYearMonth").val()),
                            Number($("#percentageOfIrregularIncomeOtherIncomeYearMonth").val())
                        ];

                        return `${label}: ${value.toLocaleString()} (${percentages[index].toFixed(2)}%)`;
                    }
                }
            }
        };

        new Chart(donutChartCanvasIrregularIncomeYearMonth, {
            type: "doughnut",
            data: donutDataIrregularIncomeYearMonth,
            options: donutOptionsIrregularIncomeYearMonth
        });

        // ===== Chart 8/10: regular savings, selected month (same shape as chart 1) =====
        let regularSavingsDepositYearMonth = Number($("#regularSavingsDepositYearMonth").val());
        let regularSavingsInvestmentYearMonth = Number($("#regularSavingsInvestmentYearMonth").val());

        let donutChartCanvasRegularSavingsYearMonth = ($("#regularSavingsYearMonth").get(0) as HTMLCanvasElement).getContext("2d")!;
        let donutDataRegularSavingsYearMonth: Chart.ChartData = {
            labels: [
                localizer.Deposit,
                localizer.Investment,
            ],
            datasets: [
                {
                    data: [regularSavingsDepositYearMonth, regularSavingsInvestmentYearMonth],
                    backgroundColor: ["#f56954", "#00a65a"],
                }
            ]
        };
        let donutOptionsRegularSavingsYearMonth: Chart.ChartOptions = {
            maintainAspectRatio: false,
            responsive: true,
            legend: {
                labels: {
                    fontColor: "#fff",
                    fontSize: 15
                }
            },
            tooltips: {
                callbacks: {
                    label: function(tooltipItem: Chart.ChartTooltipItem, data: Chart.ChartData) {
                        const index = tooltipItem.index!;
                        const label = data.labels![index];
                        const value = Number(data.datasets![0].data![index]);

                        const percentages = [
                            Number($("#percentageOfRegularSavingsDepositYearMonth").val()),
                            Number($("#percentageOfRegularSavingsInvestmentYearMonth").val())
                        ];

                        return `${label}: ${value.toLocaleString()} (${percentages[index].toFixed(2)}%)`;
                    }
                }
            }
        };

        new Chart(donutChartCanvasRegularSavingsYearMonth, {
            type: "doughnut",
            data: donutDataRegularSavingsYearMonth,
            options: donutOptionsRegularSavingsYearMonth
        });

        // ===== Chart 9/10: non-consumer spending, selected month (same shape as chart 1) =====
        let nonConsumerSpendingPublicPensionYearMonth = Number($("#nonConsumerSpendingPublicPensionYearMonth").val());
        let nonConsumerSpendingDebtRepaymentYearMonth = Number($("#nonConsumerSpendingDebtRepaymentYearMonth").val());
        let nonConsumerSpendingTaxYearMonth = Number($("#nonConsumerSpendingTaxYearMonth").val());
        let nonConsumerSpendingSocialInsuranceYearMonth = Number($("#nonConsumerSpendingSocialInsuranceYearMonth").val());
        let nonConsumerSpendingInterHouseholdTransferExpensesYearMonth = Number($("#nonConsumerSpendingInterHouseholdTransferExpensesYearMonth").val());
        let nonConsumerSpendingNonProfitOrganizationTransferYearMonth = Number($("#nonConsumerSpendingNonProfitOrganizationTransferYearMonth").val());

        let donutChartCanvasNonConsumerSpendingYearMonth = ($("#nonConsumerSpendingYearMonth").get(0) as HTMLCanvasElement).getContext("2d")!;
        let donutDataNonConsumerSpendingYearMonth: Chart.ChartData = {
            labels: [
                localizer.PublicPension,
                localizer.DebtRepayment,
                localizer.Tax,
                localizer.SocialInsurance,
                localizer.InterHouseholdTransferExpenses,
                localizer.NonProfitOrganizationTransfer,
            ],
            datasets: [
                {
                    data: [nonConsumerSpendingPublicPensionYearMonth, nonConsumerSpendingDebtRepaymentYearMonth, nonConsumerSpendingTaxYearMonth, nonConsumerSpendingSocialInsuranceYearMonth, nonConsumerSpendingInterHouseholdTransferExpensesYearMonth, nonConsumerSpendingNonProfitOrganizationTransferYearMonth],
                    backgroundColor: ["#f56954", "#00a65a", "#f39c12", "#00c0ef", "#3c8dbc", "#d2d6de"],
                }
            ]
        };
        let donutOptionsNonConsumerSpendingYearMonth: Chart.ChartOptions = {
            maintainAspectRatio: false,
            responsive: true,
            legend: {
                labels: {
                    fontColor: "#fff",
                    fontSize: 15
                }
            },
            tooltips: {
                callbacks: {
                    label: function(tooltipItem: Chart.ChartTooltipItem, data: Chart.ChartData) {
                        const index = tooltipItem.index!;
                        const label = data.labels![index];
                        const value = Number(data.datasets![0].data![index]);

                        const percentages = [
                            Number($("#percentageOfNonConsumerSpendingPublicPensionYearMonth").val()),
                            Number($("#percentageOfNonConsumerSpendingDebtRepaymentYearMonth").val()),
                            Number($("#percentageOfNonConsumerSpendingTaxYearMonth").val()),
                            Number($("#percentageOfNonConsumerSpendingSocialInsuranceYearMonth").val()),
                            Number($("#percentageOfNonConsumerSpendingInterHouseholdTransferExpensesYearMonth").val()),
                            Number($("#percentageOfNonConsumerSpendingNonProfitOrganizationTransferYearMonth").val())
                        ];

                        return `${label}: ${value.toLocaleString()} (${percentages[index].toFixed(2)}%)`;
                    }
                }
            }
        };

        new Chart(donutChartCanvasNonConsumerSpendingYearMonth, {
            type: "doughnut",
            data: donutDataNonConsumerSpendingYearMonth,
            options: donutOptionsNonConsumerSpendingYearMonth
        });

        // ===== Chart 10/10: consumer spending, selected month (same shape as chart 1) =====
        let consumerSpendingMealOrEatOutExpensesYearMonth = Number($("#consumerSpendingMealOrEatOutExpensesYearMonth").val());
        let consumerSpendingHousingOrSuppliesCostYearMonth = Number($("#consumerSpendingHousingOrSuppliesCostYearMonth").val());
        let consumerSpendingEducationExpensesYearMonth = Number($("#consumerSpendingEducationExpensesYearMonth").val());
        let consumerSpendingMedicalExpensesYearMonth = Number($("#consumerSpendingMedicalExpensesYearMonth").val());
        let consumerSpendingTransportationCostYearMonth = Number($("#consumerSpendingTransportationCostYearMonth").val());
        let consumerSpendingCommunicationCostYearMonth = Number($("#consumerSpendingCommunicationCostYearMonth").val());
        let consumerSpendingLeisureOrCultureYearMonth = Number($("#consumerSpendingLeisureOrCultureYearMonth").val());
        let consumerSpendingClothingOrShoesYearMonth = Number($("#consumerSpendingClothingOrShoesYearMonth").val());
        let consumerSpendingPinMoneyYearMonth = Number($("#consumerSpendingPinMoneyYearMonth").val());
        let consumerSpendingProtectionTypeInsuranceYearMonth = Number($("#consumerSpendingProtectionTypeInsuranceYearMonth").val());
        let consumerSpendingOtherExpensesYearMonth = Number($("#consumerSpendingOtherExpensesYearMonth").val());
        let consumerSpendingUnknownExpenditureYearMonth = Number($("#consumerSpendingUnknownExpenditureYearMonth").val());

        let donutChartCanvasConsumerSpendingYearMonth = ($("#consumerSpendingYearMonth").get(0) as HTMLCanvasElement).getContext("2d")!;
        let donutDataConsumerSpendingYearMonth: Chart.ChartData = {
            labels: [
                localizer.MealOrEatOutExpenses,
                localizer.HousingOrSuppliesCost,
                localizer.EducationExpenses,
                localizer.MedicalExpenses,
                localizer.TransportationCost,
                localizer.CommunicationCost,
                localizer.LeisureOrCulture,
                localizer.ClothingOrShoes,
                localizer.PinMoney,
                localizer.ProtectionTypeInsurance,
                localizer.OtherExpenses,
                localizer.UnknownExpenditure,
            ],
            datasets: [
                {
                    data: [consumerSpendingMealOrEatOutExpensesYearMonth, consumerSpendingHousingOrSuppliesCostYearMonth, consumerSpendingEducationExpensesYearMonth, consumerSpendingMedicalExpensesYearMonth, consumerSpendingTransportationCostYearMonth, consumerSpendingCommunicationCostYearMonth, consumerSpendingLeisureOrCultureYearMonth, consumerSpendingClothingOrShoesYearMonth, consumerSpendingPinMoneyYearMonth, consumerSpendingProtectionTypeInsuranceYearMonth, consumerSpendingOtherExpensesYearMonth, consumerSpendingUnknownExpenditureYearMonth],
                    backgroundColor: ["#FF0000", "#FFA200", "#FBFF00", "#70FF00", "#00FFE0", "#003EFF", "#D500FF", "#813F3F", "#7D813F", "#3F815A", "#3F8180", "#3F4B81"],
                }
            ]
        };
        let donutOptionsConsumerSpendingYearMonth: Chart.ChartOptions = {
            maintainAspectRatio: false,
            responsive: true,
            legend: {
                labels: {
                    fontColor: "#fff",
                    fontSize: 15
                }
            },
            tooltips: {
                callbacks: {
                    label: function(tooltipItem: Chart.ChartTooltipItem, data: Chart.ChartData) {
                        const index = tooltipItem.index!;
                        const label = data.labels![index];
                        const value = Number(data.datasets![0].data![index]);

                        const percentages = [
                            Number($("#percentageOfConsumerSpendingMealOrEatOutExpensesYearMonth").val()),
                            Number($("#percentageOfConsumerSpendingHousingOrSuppliesCostYearMonth").val()),
                            Number($("#percentageOfConsumerSpendingEducationExpensesYearMonth").val()),
                            Number($("#percentageOfConsumerSpendingMedicalExpensesYearMonth").val()),
                            Number($("#percentageOfConsumerSpendingTransportationCostYearMonth").val()),
                            Number($("#percentageOfConsumerSpendingCommunicationCostYearMonth").val()),
                            Number($("#percentageOfConsumerSpendingLeisureOrCultureYearMonth").val()),
                            Number($("#percentageOfConsumerSpendingClothingOrShoesYearMonth").val()),
                            Number($("#percentageOfConsumerSpendingPinMoneyYearMonth").val()),
                            Number($("#percentageOfConsumerSpendingProtectionTypeInsuranceYearMonth").val()),
                            Number($("#percentageOfConsumerSpendingOtherExpensesYearMonth").val()),
                            Number($("#percentageOfConsumerSpendingUnknownExpenditureYearMonth").val())
                        ];

                        return `${label}: ${value.toLocaleString()} (${percentages[index].toFixed(2)}%)`;
                    }
                }
            }
        };

        new Chart(donutChartCanvasConsumerSpendingYearMonth, {
            type: "doughnut",
            data: donutDataConsumerSpendingYearMonth,
            options: donutOptionsConsumerSpendingYearMonth
        });
    });
})();

using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Time;
using Maroik.Website.Models.ViewModels.Dashboard;

namespace Maroik.Website.Mappings;

/// <summary>
/// Converts a <see cref="DashboardDto"/> returned by <c>IDashboardService</c> into the
/// <see cref="UserIndexOutputViewModel"/> expected by the Dashboard view.
/// Handles response-to-ViewModel mapping only (timezone conversion, currency lookup, and copying
/// the per-category sums/percentages <c>DashboardService</c> already computed via
/// <see cref="Maroik.Core.Domain.Finance.FinanceBreakdownPolicy"/> onto their named view-model
/// properties) — no summing or percentage math happens here.
/// </summary>
public static class DashboardViewModelMapper
{
    extension(DashboardDto summary)
    {
        /// <summary>Maps the summary to a fully-populated <see cref="UserIndexOutputViewModel"/>.</summary>
        public UserIndexOutputViewModel ToViewModel(string timeZoneId)
        {
            var vm = new UserIndexOutputViewModel
            {
                DefaultMonetaryUnit = summary.DefaultMonetaryUnit,
                MonetaryUnits = summary.MonetaryUnits.Where(x => x != null).Select(x => x!),
                StartYear = summary.StartYear,
                EndYear = summary.EndYear,
                SelectedYear = summary.SelectedYear,
                SelectedMonth = summary.SelectedMonth
            };

            if (summary.DefaultMonetaryUnit == null) return vm;

            // All assets, deleted ones included (summary.Assets omits them): a transaction tied to a
            // since-deleted asset is counted in the totals, so its row must still show its currency.
            IReadOnlyDictionary<string, string?> currencyByProduct = summary.CurrencyByProduct
                .ToDictionary(kv => kv.Key, kv => (string?)kv.Value);

            vm.IncomeYearOutputViewModels = MapIncomes(summary.YearIncomes, currencyByProduct, timeZoneId);
            vm.ExpenditureYearOutputViewModels = MapExpenditures(summary.YearExpenditures, currencyByProduct, timeZoneId);
            vm.IncomeYearMonthOutputViewModels = MapIncomes(summary.YearMonthIncomes, currencyByProduct, timeZoneId);
            vm.ExpenditureYearMonthOutputViewModels = MapExpenditures(summary.YearMonthExpenditures, currencyByProduct, timeZoneId);

            AssignIncomeBreakdown(vm, summary.YearIncomeBreakdown, summary.YearMonthIncomeBreakdown);
            AssignExpenditureBreakdown(vm, summary.YearExpenditureBreakdown, summary.YearMonthExpenditureBreakdown);

            return vm;
        }
    }

    // ── Mapping helpers ──────────────────────────────────────────────────────

    /// <summary>Maps income DTOs to the dashboard's income rows: currency looked up by deposit asset, timestamps converted to <paramref name="timeZoneId"/>.</summary>
    private static List<IncomeOutputViewModel> MapIncomes(
        IEnumerable<IncomeResponse> items,
        IReadOnlyDictionary<string, string?> currencyByProduct,
        string timeZoneId)
        =>
        [
            .. items.Select(item => new IncomeOutputViewModel
            {
                Id = item.Id,
                MainClass = item.MainClass,
                SubClass = item.SubClass,
                Content = item.Content,
                Amount = item.Amount,
                MonetaryUnit = currencyByProduct.GetValueOrDefault(item.DepositMyAssetProductName ?? ""),
                DepositMyAssetProductName = item.DepositMyAssetProductName,
                Created = item.Created.ConvertTimeByTimeZoneIanaId(timeZoneId),
                Updated = item.Updated.ConvertTimeByTimeZoneIanaId(timeZoneId),
                Note = item.Note
            })
        ];

    /// <summary>Maps expenditure DTOs to the dashboard's expenditure rows: currency looked up by payment asset, timestamps converted to <paramref name="timeZoneId"/>.</summary>
    private static List<ExpenditureOutputViewModel> MapExpenditures(
        IEnumerable<ExpenditureResponse> items,
        IReadOnlyDictionary<string, string?> currencyByProduct,
        string timeZoneId)
        =>
        [
            .. items.Select(item => new ExpenditureOutputViewModel
            {
                Id = item.Id,
                MainClass = item.MainClass,
                SubClass = item.SubClass,
                Content = item.Content,
                Amount = item.Amount,
                MonetaryUnit = currencyByProduct.GetValueOrDefault(item.PaymentMethod ?? ""),
                PaymentMethod = item.PaymentMethod,
                Note = item.Note,
                MyDepositAsset = item.MyDepositAsset,
                Created = item.Created.ConvertTimeByTimeZoneIanaId(timeZoneId),
                Updated = item.Updated.ConvertTimeByTimeZoneIanaId(timeZoneId)
            })
        ];

    // ── Breakdown assignment (lookups only — DashboardService already computed every number) ────

    /// <summary>The pre-computed amount of <paramref name="subClass"/> under <paramref name="mainClass"/>, or 0 when absent.</summary>
    private static decimal Amount(IReadOnlyDictionary<string, FinanceBreakdownDto> breakdown, string mainClass, string subClass) =>
        breakdown.TryGetValue(mainClass, out var b) ? b.AmountBySubClass.GetValueOrDefault(subClass) : 0m;

    /// <summary>The pre-computed percentage share of <paramref name="subClass"/> under <paramref name="mainClass"/>, or 0 when absent.</summary>
    private static double Pct(IReadOnlyDictionary<string, FinanceBreakdownDto> breakdown, string mainClass, string subClass) =>
        breakdown.TryGetValue(mainClass, out var b) ? b.PercentageBySubClass.GetValueOrDefault(subClass) : 0.0;

    /// <summary>
    /// Assigns every "Year" and "YearMonth" income-breakdown view-model property from
    /// <paramref name="yearBreakdown"/> / <paramref name="yearMonthBreakdown"/> respectively.
    /// One call sets both variants of a given main-class/subClass pair via tuple deconstruction, so
    /// the amount/percentage pairs for the two periods can no longer drift out of sync the way two
    /// near-identical if/else blocks could.
    /// </summary>
    private static void AssignIncomeBreakdown(
        UserIndexOutputViewModel vm,
        IReadOnlyDictionary<string, FinanceBreakdownDto> yearBreakdown,
        IReadOnlyDictionary<string, FinanceBreakdownDto> yearMonthBreakdown)
    {
        (decimal Year, decimal YearMonth, double PctYear, double PctYearMonth) Values(string mainClass, string subClass) =>
            (Amount(yearBreakdown, mainClass, subClass), Amount(yearMonthBreakdown, mainClass, subClass),
             Pct(yearBreakdown, mainClass, subClass), Pct(yearMonthBreakdown, mainClass, subClass));

        (vm.RegularIncomeLaborIncomeYear, vm.RegularIncomeLaborIncomeYearMonth,
         vm.PercentageOfRegularIncomeLaborIncomeYear, vm.PercentageOfRegularIncomeLaborIncomeYearMonth) = Values("RegularIncome", "LaborIncome");
        (vm.RegularIncomeBusinessIncomeYear, vm.RegularIncomeBusinessIncomeYearMonth,
         vm.PercentageOfRegularIncomeBusinessIncomeYear, vm.PercentageOfRegularIncomeBusinessIncomeYearMonth) = Values("RegularIncome", "BusinessIncome");
        (vm.RegularIncomePensionIncomeYear, vm.RegularIncomePensionIncomeYearMonth,
         vm.PercentageOfRegularIncomePensionIncomeYear, vm.PercentageOfRegularIncomePensionIncomeYearMonth) = Values("RegularIncome", "PensionIncome");
        (vm.RegularIncomeFinancialIncomeYear, vm.RegularIncomeFinancialIncomeYearMonth,
         vm.PercentageOfRegularIncomeFinancialIncomeYear, vm.PercentageOfRegularIncomeFinancialIncomeYearMonth) = Values("RegularIncome", "FinancialIncome");
        (vm.RegularIncomeRentalIncomeYear, vm.RegularIncomeRentalIncomeYearMonth,
         vm.PercentageOfRegularIncomeRentalIncomeYear, vm.PercentageOfRegularIncomeRentalIncomeYearMonth) = Values("RegularIncome", "RentalIncome");
        (vm.RegularIncomeOtherIncomeYear, vm.RegularIncomeOtherIncomeYearMonth,
         vm.PercentageOfRegularIncomeOtherIncomeYear, vm.PercentageOfRegularIncomeOtherIncomeYearMonth) = Values("RegularIncome", "OtherIncome");

        (vm.IrregularIncomeLaborIncomeYear, vm.IrregularIncomeLaborIncomeYearMonth,
         vm.PercentageOfIrregularIncomeLaborIncomeYear, vm.PercentageOfIrregularIncomeLaborIncomeYearMonth) = Values("IrregularIncome", "LaborIncome");
        (vm.IrregularIncomeOtherIncomeYear, vm.IrregularIncomeOtherIncomeYearMonth,
         vm.PercentageOfIrregularIncomeOtherIncomeYear, vm.PercentageOfIrregularIncomeOtherIncomeYearMonth) = Values("IrregularIncome", "OtherIncome");
    }

    /// <summary>
    /// Assigns every "Year" and "YearMonth" expenditure-breakdown view-model property from
    /// <paramref name="yearBreakdown"/> / <paramref name="yearMonthBreakdown"/> respectively. See
    /// <see cref="AssignIncomeBreakdown"/> for why this sets both periods per call instead of
    /// branching on an <c>isYearMonth</c> flag.
    /// </summary>
    private static void AssignExpenditureBreakdown(
        UserIndexOutputViewModel vm,
        IReadOnlyDictionary<string, FinanceBreakdownDto> yearBreakdown,
        IReadOnlyDictionary<string, FinanceBreakdownDto> yearMonthBreakdown)
    {

        (vm.RegularSavingsDepositYear, vm.RegularSavingsDepositYearMonth,
         vm.PercentageOfRegularSavingsDepositYear, vm.PercentageOfRegularSavingsDepositYearMonth) = Values("RegularSavings", "Deposit");
        (vm.RegularSavingsInvestmentYear, vm.RegularSavingsInvestmentYearMonth,
         vm.PercentageOfRegularSavingsInvestmentYear, vm.PercentageOfRegularSavingsInvestmentYearMonth) = Values("RegularSavings", "Investment");

        (vm.NonConsumerSpendingPublicPensionYear, vm.NonConsumerSpendingPublicPensionYearMonth,
         vm.PercentageOfNonConsumerSpendingPublicPensionYear, vm.PercentageOfNonConsumerSpendingPublicPensionYearMonth) = Values("NonConsumerSpending", "PublicPension");
        (vm.NonConsumerSpendingDebtRepaymentYear, vm.NonConsumerSpendingDebtRepaymentYearMonth,
         vm.PercentageOfNonConsumerSpendingDebtRepaymentYear, vm.PercentageOfNonConsumerSpendingDebtRepaymentYearMonth) = Values("NonConsumerSpending", "DebtRepayment");
        (vm.NonConsumerSpendingTaxYear, vm.NonConsumerSpendingTaxYearMonth,
         vm.PercentageOfNonConsumerSpendingTaxYear, vm.PercentageOfNonConsumerSpendingTaxYearMonth) = Values("NonConsumerSpending", "Tax");
        (vm.NonConsumerSpendingSocialInsuranceYear, vm.NonConsumerSpendingSocialInsuranceYearMonth,
         vm.PercentageOfNonConsumerSpendingSocialInsuranceYear, vm.PercentageOfNonConsumerSpendingSocialInsuranceYearMonth) = Values("NonConsumerSpending", "SocialInsurance");
        (vm.NonConsumerSpendingInterHouseholdTransferExpensesYear, vm.NonConsumerSpendingInterHouseholdTransferExpensesYearMonth,
         vm.PercentageOfNonConsumerSpendingInterHouseholdTransferExpensesYear, vm.PercentageOfNonConsumerSpendingInterHouseholdTransferExpensesYearMonth) = Values("NonConsumerSpending", "InterHouseholdTransferExpenses");
        (vm.NonConsumerSpendingNonProfitOrganizationTransferYear, vm.NonConsumerSpendingNonProfitOrganizationTransferYearMonth,
         vm.PercentageOfNonConsumerSpendingNonProfitOrganizationTransferYear, vm.PercentageOfNonConsumerSpendingNonProfitOrganizationTransferYearMonth) = Values("NonConsumerSpending", "NonProfitOrganizationTransfer");

        (vm.ConsumerSpendingMealOrEatOutExpensesYear, vm.ConsumerSpendingMealOrEatOutExpensesYearMonth,
         vm.PercentageOfConsumerSpendingMealOrEatOutExpensesYear, vm.PercentageOfConsumerSpendingMealOrEatOutExpensesYearMonth) = Values("ConsumerSpending", "MealOrEatOutExpenses");
        (vm.ConsumerSpendingHousingOrSuppliesCostYear, vm.ConsumerSpendingHousingOrSuppliesCostYearMonth,
         vm.PercentageOfConsumerSpendingHousingOrSuppliesCostYear, vm.PercentageOfConsumerSpendingHousingOrSuppliesCostYearMonth) = Values("ConsumerSpending", "HousingOrSuppliesCost");
        (vm.ConsumerSpendingEducationExpensesYear, vm.ConsumerSpendingEducationExpensesYearMonth,
         vm.PercentageOfConsumerSpendingEducationExpensesYear, vm.PercentageOfConsumerSpendingEducationExpensesYearMonth) = Values("ConsumerSpending", "EducationExpenses");
        (vm.ConsumerSpendingMedicalExpensesYear, vm.ConsumerSpendingMedicalExpensesYearMonth,
         vm.PercentageOfConsumerSpendingMedicalExpensesYear, vm.PercentageOfConsumerSpendingMedicalExpensesYearMonth) = Values("ConsumerSpending", "MedicalExpenses");
        (vm.ConsumerSpendingTransportationCostYear, vm.ConsumerSpendingTransportationCostYearMonth,
         vm.PercentageOfConsumerSpendingTransportationCostYear, vm.PercentageOfConsumerSpendingTransportationCostYearMonth) = Values("ConsumerSpending", "TransportationCost");
        (vm.ConsumerSpendingCommunicationCostYear, vm.ConsumerSpendingCommunicationCostYearMonth,
         vm.PercentageOfConsumerSpendingCommunicationCostYear, vm.PercentageOfConsumerSpendingCommunicationCostYearMonth) = Values("ConsumerSpending", "CommunicationCost");
        (vm.ConsumerSpendingLeisureOrCultureYear, vm.ConsumerSpendingLeisureOrCultureYearMonth,
         vm.PercentageOfConsumerSpendingLeisureOrCultureYear, vm.PercentageOfConsumerSpendingLeisureOrCultureYearMonth) = Values("ConsumerSpending", "LeisureOrCulture");
        (vm.ConsumerSpendingClothingOrShoesYear, vm.ConsumerSpendingClothingOrShoesYearMonth,
         vm.PercentageOfConsumerSpendingClothingOrShoesYear, vm.PercentageOfConsumerSpendingClothingOrShoesYearMonth) = Values("ConsumerSpending", "ClothingOrShoes");
        (vm.ConsumerSpendingPinMoneyYear, vm.ConsumerSpendingPinMoneyYearMonth,
         vm.PercentageOfConsumerSpendingPinMoneyYear, vm.PercentageOfConsumerSpendingPinMoneyYearMonth) = Values("ConsumerSpending", "PinMoney");
        (vm.ConsumerSpendingProtectionTypeInsuranceYear, vm.ConsumerSpendingProtectionTypeInsuranceYearMonth,
         vm.PercentageOfConsumerSpendingProtectionTypeInsuranceYear, vm.PercentageOfConsumerSpendingProtectionTypeInsuranceYearMonth) = Values("ConsumerSpending", "ProtectionTypeInsurance");
        (vm.ConsumerSpendingOtherExpensesYear, vm.ConsumerSpendingOtherExpensesYearMonth,
         vm.PercentageOfConsumerSpendingOtherExpensesYear, vm.PercentageOfConsumerSpendingOtherExpensesYearMonth) = Values("ConsumerSpending", "OtherExpenses");
        (vm.ConsumerSpendingUnknownExpenditureYear, vm.ConsumerSpendingUnknownExpenditureYearMonth,
         vm.PercentageOfConsumerSpendingUnknownExpenditureYear, vm.PercentageOfConsumerSpendingUnknownExpenditureYearMonth) = Values("ConsumerSpending", "UnknownExpenditure");
        return;

        (decimal Year, decimal YearMonth, double PctYear, double PctYearMonth) Values(string mainClass, string subClass) =>
            (Amount(yearBreakdown, mainClass, subClass), Amount(yearMonthBreakdown, mainClass, subClass),
                Pct(yearBreakdown, mainClass, subClass), Pct(yearMonthBreakdown, mainClass, subClass));
    }
}

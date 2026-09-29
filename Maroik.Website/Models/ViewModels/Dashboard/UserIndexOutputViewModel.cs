// ReSharper disable PropertyCanBeMadeInitOnly.Global
namespace Maroik.Website.Models.ViewModels.Dashboard;

/// <summary>
/// Full output model for the Dashboard index page.
/// Aggregates per-category income and expenditure totals for both the selected year
/// and the selected year-month, together with percentage breakdowns for chart rendering.
/// Property names follow the pattern: {MainClass}{SubClass}{Year|YearMonth}
/// and PercentageOf{MainClass}{SubClass}{Year|YearMonth}.
/// </summary>
public class UserIndexOutputViewModel
{
    /// <summary>All income records for the selected year (used in the yearly breakdown table).</summary>
    public List<IncomeOutputViewModel> IncomeYearOutputViewModels { get; set; } = [];

    /// <summary>All expenditure records for the selected year.</summary>
    public List<ExpenditureOutputViewModel> ExpenditureYearOutputViewModels { get; set; } = [];

    /// <summary>All income records for the selected year-month (used in the monthly breakdown table).</summary>
    public List<IncomeOutputViewModel> IncomeYearMonthOutputViewModels { get; set; } = [];

    /// <summary>All expenditure records for the selected year-month.</summary>
    public List<ExpenditureOutputViewModel> ExpenditureYearMonthOutputViewModels { get; set; } = [];

    /// <summary>Distinct currency codes present in the user's assets, for the currency-selector dropdown.</summary>
    public IEnumerable<string> MonetaryUnits { get; set; } = [];

    /// <summary>The currency code currently selected by the user for dashboard display.</summary>
    public string? DefaultMonetaryUnit { get; set; }

    // Year/Month selectors

    /// <summary>Earliest year for which the user has any income or expenditure data (lower bound of the year selector).</summary>
    public int StartYear { get; set; }

    /// <summary>Latest year with data (upper bound of the year selector).</summary>
    public int EndYear { get; set; }

    /// <summary>The year currently selected for the yearly and monthly breakdown views.</summary>
    public int SelectedYear { get; set; }

    /// <summary>The month (1–12) currently selected for the monthly breakdown view.</summary>
    public int SelectedMonth { get; set; }

    // Year Income - RegularIncome

    /// <summary>Total labor income for the selected year.</summary>
    public decimal RegularIncomeLaborIncomeYear { get; set; }

    /// <summary>Total business income for the selected year.</summary>
    public decimal RegularIncomeBusinessIncomeYear { get; set; }

    /// <summary>Total pension income for the selected year.</summary>
    public decimal RegularIncomePensionIncomeYear { get; set; }

    /// <summary>Total financial income (interest, dividends) for the selected year.</summary>
    public decimal RegularIncomeFinancialIncomeYear { get; set; }

    /// <summary>Total rental income for the selected year.</summary>
    public decimal RegularIncomeRentalIncomeYear { get; set; }

    /// <summary>Total other regular income for the selected year.</summary>
    public decimal RegularIncomeOtherIncomeYear { get; set; }

    /// <summary>Labor income as a percentage of total regular income for the selected year.</summary>
    public double PercentageOfRegularIncomeLaborIncomeYear { get; set; }

    /// <summary>Business income as a percentage of total regular income for the selected year.</summary>
    public double PercentageOfRegularIncomeBusinessIncomeYear { get; set; }

    /// <summary>Pension income as a percentage of total regular income for the selected year.</summary>
    public double PercentageOfRegularIncomePensionIncomeYear { get; set; }

    /// <summary>Financial income as a percentage of total regular income for the selected year.</summary>
    public double PercentageOfRegularIncomeFinancialIncomeYear { get; set; }

    /// <summary>Rental income as a percentage of total regular income for the selected year.</summary>
    public double PercentageOfRegularIncomeRentalIncomeYear { get; set; }

    /// <summary>Other regular income as a percentage of total regular income for the selected year.</summary>
    public double PercentageOfRegularIncomeOtherIncomeYear { get; set; }

    // Year Income - IrregularIncome

    /// <summary>Total irregular labor income (e.g. bonuses) for the selected year.</summary>
    public decimal IrregularIncomeLaborIncomeYear { get; set; }

    /// <summary>Total other irregular income for the selected year.</summary>
    public decimal IrregularIncomeOtherIncomeYear { get; set; }

    /// <summary>Irregular labor income as a percentage of total irregular income for the selected year.</summary>
    public double PercentageOfIrregularIncomeLaborIncomeYear { get; set; }

    /// <summary>Other irregular income as a percentage of total irregular income for the selected year.</summary>
    public double PercentageOfIrregularIncomeOtherIncomeYear { get; set; }

    // Year Expenditure - RegularSavings

    /// <summary>Total deposit/savings contributions for the selected year.</summary>
    public decimal RegularSavingsDepositYear { get; set; }

    /// <summary>Total investment contributions for the selected year.</summary>
    public decimal RegularSavingsInvestmentYear { get; set; }

    /// <summary>Deposit savings as a percentage of total regular savings for the selected year.</summary>
    public double PercentageOfRegularSavingsDepositYear { get; set; }

    /// <summary>Investment contributions as a percentage of total regular savings for the selected year.</summary>
    public double PercentageOfRegularSavingsInvestmentYear { get; set; }

    // Year Expenditure - NonConsumerSpending

    /// <summary>Total public pension contributions for the selected year.</summary>
    public decimal NonConsumerSpendingPublicPensionYear { get; set; }

    /// <summary>Total debt repayment amounts for the selected year.</summary>
    public decimal NonConsumerSpendingDebtRepaymentYear { get; set; }

    /// <summary>Total taxes paid for the selected year.</summary>
    public decimal NonConsumerSpendingTaxYear { get; set; }

    /// <summary>Total social insurance premiums for the selected year.</summary>
    public decimal NonConsumerSpendingSocialInsuranceYear { get; set; }

    /// <summary>Total inter-household transfer expenses (e.g. remittances) for the selected year.</summary>
    public decimal NonConsumerSpendingInterHouseholdTransferExpensesYear { get; set; }

    /// <summary>Total donations to non-profit organizations for the selected year.</summary>
    public decimal NonConsumerSpendingNonProfitOrganizationTransferYear { get; set; }

    /// <summary>Public pension as a percentage of total non-consumer spending for the selected year.</summary>
    public double PercentageOfNonConsumerSpendingPublicPensionYear { get; set; }

    /// <summary>Debt repayment as a percentage of total non-consumer spending for the selected year.</summary>
    public double PercentageOfNonConsumerSpendingDebtRepaymentYear { get; set; }

    /// <summary>Tax as a percentage of total non-consumer spending for the selected year.</summary>
    public double PercentageOfNonConsumerSpendingTaxYear { get; set; }

    /// <summary>Social insurance as a percentage of total non-consumer spending for the selected year.</summary>
    public double PercentageOfNonConsumerSpendingSocialInsuranceYear { get; set; }

    /// <summary>Inter-household transfers as a percentage of total non-consumer spending for the selected year.</summary>
    public double PercentageOfNonConsumerSpendingInterHouseholdTransferExpensesYear { get; set; }

    /// <summary>Non-profit transfers as a percentage of total non-consumer spending for the selected year.</summary>
    public double PercentageOfNonConsumerSpendingNonProfitOrganizationTransferYear { get; set; }

    // Year Expenditure - ConsumerSpending

    /// <summary>Total meal / dining-out expenses for the selected year.</summary>
    public decimal ConsumerSpendingMealOrEatOutExpensesYear { get; set; }

    /// <summary>Total housing and household supplies costs for the selected year.</summary>
    public decimal ConsumerSpendingHousingOrSuppliesCostYear { get; set; }

    /// <summary>Total education expenses for the selected year.</summary>
    public decimal ConsumerSpendingEducationExpensesYear { get; set; }

    /// <summary>Total medical expenses for the selected year.</summary>
    public decimal ConsumerSpendingMedicalExpensesYear { get; set; }

    /// <summary>Total transportation costs for the selected year.</summary>
    public decimal ConsumerSpendingTransportationCostYear { get; set; }

    /// <summary>Total communication costs (phone, internet) for the selected year.</summary>
    public decimal ConsumerSpendingCommunicationCostYear { get; set; }

    /// <summary>Total leisure and culture spending for the selected year.</summary>
    public decimal ConsumerSpendingLeisureOrCultureYear { get; set; }

    /// <summary>Total clothing and footwear spending for the selected year.</summary>
    public decimal ConsumerSpendingClothingOrShoesYear { get; set; }

    /// <summary>Total pocket-money / allowance outflows for the selected year.</summary>
    public decimal ConsumerSpendingPinMoneyYear { get; set; }

    /// <summary>Total protection-type insurance premiums for the selected year.</summary>
    public decimal ConsumerSpendingProtectionTypeInsuranceYear { get; set; }

    /// <summary>Total other consumer spending for the selected year.</summary>
    public decimal ConsumerSpendingOtherExpensesYear { get; set; }

    /// <summary>Total expenditure entries with unknown classification for the selected year.</summary>
    public decimal ConsumerSpendingUnknownExpenditureYear { get; set; }

    /// <summary>Meal/dining-out as a percentage of total consumer spending for the selected year.</summary>
    public double PercentageOfConsumerSpendingMealOrEatOutExpensesYear { get; set; }

    /// <summary>Housing/supplies as a percentage of total consumer spending for the selected year.</summary>
    public double PercentageOfConsumerSpendingHousingOrSuppliesCostYear { get; set; }

    /// <summary>Education expenses as a percentage of total consumer spending for the selected year.</summary>
    public double PercentageOfConsumerSpendingEducationExpensesYear { get; set; }

    /// <summary>Medical expenses as a percentage of total consumer spending for the selected year.</summary>
    public double PercentageOfConsumerSpendingMedicalExpensesYear { get; set; }

    /// <summary>Transportation costs as a percentage of total consumer spending for the selected year.</summary>
    public double PercentageOfConsumerSpendingTransportationCostYear { get; set; }

    /// <summary>Communication costs as a percentage of total consumer spending for the selected year.</summary>
    public double PercentageOfConsumerSpendingCommunicationCostYear { get; set; }

    /// <summary>Leisure/culture as a percentage of total consumer spending for the selected year.</summary>
    public double PercentageOfConsumerSpendingLeisureOrCultureYear { get; set; }

    /// <summary>Clothing/footwear as a percentage of total consumer spending for the selected year.</summary>
    public double PercentageOfConsumerSpendingClothingOrShoesYear { get; set; }

    /// <summary>Pocket-money as a percentage of total consumer spending for the selected year.</summary>
    public double PercentageOfConsumerSpendingPinMoneyYear { get; set; }

    /// <summary>Protection insurance as a percentage of total consumer spending for the selected year.</summary>
    public double PercentageOfConsumerSpendingProtectionTypeInsuranceYear { get; set; }

    /// <summary>Other expenses as a percentage of total consumer spending for the selected year.</summary>
    public double PercentageOfConsumerSpendingOtherExpensesYear { get; set; }

    /// <summary>Unknown expenditure as a percentage of total consumer spending for the selected year.</summary>
    public double PercentageOfConsumerSpendingUnknownExpenditureYear { get; set; }

    // YearMonth Income - RegularIncome

    /// <summary>Total labor income for the selected year-month.</summary>
    public decimal RegularIncomeLaborIncomeYearMonth { get; set; }

    /// <summary>Total business income for the selected year-month.</summary>
    public decimal RegularIncomeBusinessIncomeYearMonth { get; set; }

    /// <summary>Total pension income for the selected year-month.</summary>
    public decimal RegularIncomePensionIncomeYearMonth { get; set; }

    /// <summary>Total financial income for the selected year-month.</summary>
    public decimal RegularIncomeFinancialIncomeYearMonth { get; set; }

    /// <summary>Total rental income for the selected year-month.</summary>
    public decimal RegularIncomeRentalIncomeYearMonth { get; set; }

    /// <summary>Total other regular income for the selected year-month.</summary>
    public decimal RegularIncomeOtherIncomeYearMonth { get; set; }

    /// <summary>Labor income as a percentage of total regular income for the selected year-month.</summary>
    public double PercentageOfRegularIncomeLaborIncomeYearMonth { get; set; }

    /// <summary>Business income as a percentage of total regular income for the selected year-month.</summary>
    public double PercentageOfRegularIncomeBusinessIncomeYearMonth { get; set; }

    /// <summary>Pension income as a percentage of total regular income for the selected year-month.</summary>
    public double PercentageOfRegularIncomePensionIncomeYearMonth { get; set; }

    /// <summary>Financial income as a percentage of total regular income for the selected year-month.</summary>
    public double PercentageOfRegularIncomeFinancialIncomeYearMonth { get; set; }

    /// <summary>Rental income as a percentage of total regular income for the selected year-month.</summary>
    public double PercentageOfRegularIncomeRentalIncomeYearMonth { get; set; }

    /// <summary>Other regular income as a percentage of total regular income for the selected year-month.</summary>
    public double PercentageOfRegularIncomeOtherIncomeYearMonth { get; set; }

    // YearMonth Income - IrregularIncome

    /// <summary>Total irregular labor income for the selected year-month.</summary>
    public decimal IrregularIncomeLaborIncomeYearMonth { get; set; }

    /// <summary>Total other irregular income for the selected year-month.</summary>
    public decimal IrregularIncomeOtherIncomeYearMonth { get; set; }

    /// <summary>Irregular labor income as a percentage of total irregular income for the selected year-month.</summary>
    public double PercentageOfIrregularIncomeLaborIncomeYearMonth { get; set; }

    /// <summary>Other irregular income as a percentage of total irregular income for the selected year-month.</summary>
    public double PercentageOfIrregularIncomeOtherIncomeYearMonth { get; set; }

    // YearMonth Expenditure - RegularSavings

    /// <summary>Total deposit/savings contributions for the selected year-month.</summary>
    public decimal RegularSavingsDepositYearMonth { get; set; }

    /// <summary>Total investment contributions for the selected year-month.</summary>
    public decimal RegularSavingsInvestmentYearMonth { get; set; }

    /// <summary>Deposit savings as a percentage of total regular savings for the selected year-month.</summary>
    public double PercentageOfRegularSavingsDepositYearMonth { get; set; }

    /// <summary>Investment contributions as a percentage of total regular savings for the selected year-month.</summary>
    public double PercentageOfRegularSavingsInvestmentYearMonth { get; set; }

    // YearMonth Expenditure - NonConsumerSpending

    /// <summary>Total public pension contributions for the selected year-month.</summary>
    public decimal NonConsumerSpendingPublicPensionYearMonth { get; set; }

    /// <summary>Total debt repayment amounts for the selected year-month.</summary>
    public decimal NonConsumerSpendingDebtRepaymentYearMonth { get; set; }

    /// <summary>Total taxes paid for the selected year-month.</summary>
    public decimal NonConsumerSpendingTaxYearMonth { get; set; }

    /// <summary>Total social insurance premiums for the selected year-month.</summary>
    public decimal NonConsumerSpendingSocialInsuranceYearMonth { get; set; }

    /// <summary>Total inter-household transfer expenses for the selected year-month.</summary>
    public decimal NonConsumerSpendingInterHouseholdTransferExpensesYearMonth { get; set; }

    /// <summary>Total donations to non-profit organizations for the selected year-month.</summary>
    public decimal NonConsumerSpendingNonProfitOrganizationTransferYearMonth { get; set; }

    /// <summary>Public pension as a percentage of total non-consumer spending for the selected year-month.</summary>
    public double PercentageOfNonConsumerSpendingPublicPensionYearMonth { get; set; }

    /// <summary>Debt repayment as a percentage of total non-consumer spending for the selected year-month.</summary>
    public double PercentageOfNonConsumerSpendingDebtRepaymentYearMonth { get; set; }

    /// <summary>Tax as a percentage of total non-consumer spending for the selected year-month.</summary>
    public double PercentageOfNonConsumerSpendingTaxYearMonth { get; set; }

    /// <summary>Social insurance as a percentage of total non-consumer spending for the selected year-month.</summary>
    public double PercentageOfNonConsumerSpendingSocialInsuranceYearMonth { get; set; }

    /// <summary>Inter-household transfers as a percentage of total non-consumer spending for the selected year-month.</summary>
    public double PercentageOfNonConsumerSpendingInterHouseholdTransferExpensesYearMonth { get; set; }

    /// <summary>Non-profit transfers as a percentage of total non-consumer spending for the selected year-month.</summary>
    public double PercentageOfNonConsumerSpendingNonProfitOrganizationTransferYearMonth { get; set; }

    // YearMonth Expenditure - ConsumerSpending

    /// <summary>Total meal / dining-out expenses for the selected year-month.</summary>
    public decimal ConsumerSpendingMealOrEatOutExpensesYearMonth { get; set; }

    /// <summary>Total housing and household supplies costs for the selected year-month.</summary>
    public decimal ConsumerSpendingHousingOrSuppliesCostYearMonth { get; set; }

    /// <summary>Total education expenses for the selected year-month.</summary>
    public decimal ConsumerSpendingEducationExpensesYearMonth { get; set; }

    /// <summary>Total medical expenses for the selected year-month.</summary>
    public decimal ConsumerSpendingMedicalExpensesYearMonth { get; set; }

    /// <summary>Total transportation costs for the selected year-month.</summary>
    public decimal ConsumerSpendingTransportationCostYearMonth { get; set; }

    /// <summary>Total communication costs for the selected year-month.</summary>
    public decimal ConsumerSpendingCommunicationCostYearMonth { get; set; }

    /// <summary>Total leisure and culture spending for the selected year-month.</summary>
    public decimal ConsumerSpendingLeisureOrCultureYearMonth { get; set; }

    /// <summary>Total clothing and footwear spending for the selected year-month.</summary>
    public decimal ConsumerSpendingClothingOrShoesYearMonth { get; set; }

    /// <summary>Total pocket-money / allowance outflows for the selected year-month.</summary>
    public decimal ConsumerSpendingPinMoneyYearMonth { get; set; }

    /// <summary>Total protection-type insurance premiums for the selected year-month.</summary>
    public decimal ConsumerSpendingProtectionTypeInsuranceYearMonth { get; set; }

    /// <summary>Total other consumer spending for the selected year-month.</summary>
    public decimal ConsumerSpendingOtherExpensesYearMonth { get; set; }

    /// <summary>Total expenditure entries with unknown classification for the selected year-month.</summary>
    public decimal ConsumerSpendingUnknownExpenditureYearMonth { get; set; }

    /// <summary>Meal/dining-out as a percentage of total consumer spending for the selected year-month.</summary>
    public double PercentageOfConsumerSpendingMealOrEatOutExpensesYearMonth { get; set; }

    /// <summary>Housing/supplies as a percentage of total consumer spending for the selected year-month.</summary>
    public double PercentageOfConsumerSpendingHousingOrSuppliesCostYearMonth { get; set; }

    /// <summary>Education expenses as a percentage of total consumer spending for the selected year-month.</summary>
    public double PercentageOfConsumerSpendingEducationExpensesYearMonth { get; set; }

    /// <summary>Medical expenses as a percentage of total consumer spending for the selected year-month.</summary>
    public double PercentageOfConsumerSpendingMedicalExpensesYearMonth { get; set; }

    /// <summary>Transportation costs as a percentage of total consumer spending for the selected year-month.</summary>
    public double PercentageOfConsumerSpendingTransportationCostYearMonth { get; set; }

    /// <summary>Communication costs as a percentage of total consumer spending for the selected year-month.</summary>
    public double PercentageOfConsumerSpendingCommunicationCostYearMonth { get; set; }

    /// <summary>Leisure/culture as a percentage of total consumer spending for the selected year-month.</summary>
    public double PercentageOfConsumerSpendingLeisureOrCultureYearMonth { get; set; }

    /// <summary>Clothing/footwear as a percentage of total consumer spending for the selected year-month.</summary>
    public double PercentageOfConsumerSpendingClothingOrShoesYearMonth { get; set; }

    /// <summary>Pocket-money as a percentage of total consumer spending for the selected year-month.</summary>
    public double PercentageOfConsumerSpendingPinMoneyYearMonth { get; set; }

    /// <summary>Protection insurance as a percentage of total consumer spending for the selected year-month.</summary>
    public double PercentageOfConsumerSpendingProtectionTypeInsuranceYearMonth { get; set; }

    /// <summary>Other expenses as a percentage of total consumer spending for the selected year-month.</summary>
    public double PercentageOfConsumerSpendingOtherExpensesYearMonth { get; set; }

    /// <summary>Unknown expenditure as a percentage of total consumer spending for the selected year-month.</summary>
    public double PercentageOfConsumerSpendingUnknownExpenditureYearMonth { get; set; }
}

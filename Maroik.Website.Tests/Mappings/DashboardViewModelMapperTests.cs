using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Localization;
using Maroik.Website.Mappings;

namespace Maroik.Website.Tests.Mappings;

/// <summary>
/// Unit tests for <see cref="DashboardViewModelMapper"/>.
/// The per-category sum/percentage arithmetic itself is computed by <c>DashboardService</c> via
/// <see cref="Maroik.Core.Domain.Finance.FinanceBreakdownPolicy"/> (covered by
/// <c>FinanceBreakdownPolicyTests</c> and <c>DashboardServiceTests</c>) -- these tests only verify
/// that the mapper copies each pre-computed <see cref="FinanceBreakdownDto"/> value onto the right
/// named <c>UserIndexOutputViewModel</c> property, and never computes a sum or percentage itself.
/// </summary>
public class DashboardViewModelMapperTests
{
    private const string Utc = "UTC";

    private static IncomeResponse Income(string mainClass, string subClass, decimal amount, string? asset = "Bank") => new()
    {
        Id = 1,
        MainClass = mainClass,
        SubClass = subClass,
        Amount = amount,
        DepositMyAssetProductName = asset,
        Created = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Updated = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc)
    };

    private static ExpenditureResponse Expenditure(string mainClass, string subClass, decimal amount, string? paymentMethod = "Card") => new()
    {
        Id = 1,
        MainClass = mainClass,
        SubClass = subClass,
        Amount = amount,
        PaymentMethod = paymentMethod,
        Created = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Updated = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc)
    };

    private static FinanceBreakdownDto Breakdown(decimal total, params (string SubClass, decimal Amount, double Percentage)[] entries) => new()
    {
        Total = total,
        AmountBySubClass = entries.ToDictionary(e => e.SubClass, e => e.Amount),
        PercentageBySubClass = entries.ToDictionary(e => e.SubClass, e => e.Percentage)
    };

    private static DashboardDto BaseDto() => new()
    {
        DefaultMonetaryUnit = "KRW",
        Assets = [],
        MonetaryUnits = ["KRW", null, "USD"],
        StartYear = 2020,
        EndYear = 2025,
        SelectedYear = 2025,
        SelectedMonth = 6
    };

    // -- Early-exit / basic field mapping -------------------------------------

    /// <summary>To view model default monetary unit null returns basic fields only with no aggregation.</summary>
    [Fact]
    public void ToViewModel_DefaultMonetaryUnitNull_ReturnsBasicFieldsOnlyWithNoAggregation()
    {
        var dto = BaseDto();
        dto.DefaultMonetaryUnit = null;
        dto.YearIncomeBreakdown = new Dictionary<string, FinanceBreakdownDto> { ["RegularIncome"] = Breakdown(100m, ("LaborIncome", 100m, 100.0)) };

        var vm = dto.ToViewModel(Utc);

        Assert.Empty(vm.IncomeYearOutputViewModels);
        Assert.Equal(0m, vm.RegularIncomeLaborIncomeYear);
    }

    /// <summary>To view model copies year selectors and default monetary unit.</summary>
    [Fact]
    public void ToViewModel_CopiesYearSelectorsAndDefaultMonetaryUnit()
    {
        var dto = BaseDto();

        var vm = dto.ToViewModel(Utc);

        Assert.Equal(2020, vm.StartYear);
        Assert.Equal(2025, vm.EndYear);
        Assert.Equal(2025, vm.SelectedYear);
        Assert.Equal(6, vm.SelectedMonth);
        Assert.Equal("KRW", vm.DefaultMonetaryUnit);
    }

    /// <summary>To view model filters null monetary units.</summary>
    [Fact]
    public void ToViewModel_FiltersNullMonetaryUnits()
    {
        var dto = BaseDto();

        var vm = dto.ToViewModel(Utc);

        Assert.Equal(["KRW", "USD"], vm.MonetaryUnits);
    }

    // -- Currency lookup + timezone conversion --------------------------------

    /// <summary>To view model maps income monetary unit from asset lookup by deposit product name.</summary>
    [Fact]
    public void ToViewModel_MapsIncomeMonetaryUnit_FromAssetLookupByDepositProductName()
    {
        var dto = BaseDto();
        dto.CurrencyByProduct = new Dictionary<string, string> { ["Bank"] = "USD" };
        dto.YearIncomes = [Income("RegularIncome", "LaborIncome", 100)];

        var vm = dto.ToViewModel(Utc);

        Assert.Equal("USD", vm.IncomeYearOutputViewModels[0].MonetaryUnit);
    }

    /// <summary>
    /// Regression: a row tied to a since-deleted asset (absent from <c>Assets</c>, present in
    /// <c>CurrencyByProduct</c>) still shows its currency — the totals count it, so the row must not go blank.
    /// </summary>
    [Fact]
    public void ToViewModel_KeepsTheCurrencyOfARowTiedToADeletedAsset()
    {
        var dto = BaseDto();
        dto.Assets = [];
        dto.CurrencyByProduct = new Dictionary<string, string> { ["OldBank"] = "KRW" };
        dto.YearIncomes = [Income("RegularIncome", "LaborIncome", 100, "OldBank")];

        var vm = dto.ToViewModel(Utc);

        Assert.Equal("KRW", vm.IncomeYearOutputViewModels[0].MonetaryUnit);
    }

    /// <summary>To view model maps expenditure monetary unit from asset lookup by payment method.</summary>
    [Fact]
    public void ToViewModel_MapsExpenditureMonetaryUnit_FromAssetLookupByPaymentMethod()
    {
        var dto = BaseDto();
        dto.CurrencyByProduct = new Dictionary<string, string> { ["Card"] = "USD" };
        dto.YearExpenditures = [Expenditure("ConsumerSpending", "MealOrEatOutExpenses", 50)];

        var vm = dto.ToViewModel(Utc);

        Assert.Equal("USD", vm.ExpenditureYearOutputViewModels[0].MonetaryUnit);
    }

    /// <summary>To view model converts income created and updated to target time zone.</summary>
    [Fact]
    public void ToViewModel_ConvertsIncomeCreatedAndUpdated_ToTargetTimeZone()
    {
        var dto = BaseDto();
        var income = Income("RegularIncome", "LaborIncome", 100);
        dto.YearIncomes = [income];

        var vm = dto.ToViewModel("Asia/Seoul");

        Assert.Equal(income.Created.ConvertTimeByTimeZoneIanaId("Asia/Seoul"), vm.IncomeYearOutputViewModels[0].Created);
        Assert.Equal(income.Updated.ConvertTimeByTimeZoneIanaId("Asia/Seoul"), vm.IncomeYearOutputViewModels[0].Updated);
    }

    // -- Income breakdown copy: RegularIncome / IrregularIncome ----------------

    /// <summary>To view model copies regular income subclass amounts and percentages for the year onto their named properties.</summary>
    [Fact]
    public void ToViewModel_CopiesRegularIncomeBreakdown_Year()
    {
        var dto = BaseDto();
        dto.YearIncomeBreakdown = new Dictionary<string, FinanceBreakdownDto>
        {
            ["RegularIncome"] = Breakdown(1000m,
                ("LaborIncome", 600m, 60.0), ("BusinessIncome", 300m, 30.0), ("PensionIncome", 100m, 10.0))
        };

        var vm = dto.ToViewModel(Utc);

        Assert.Equal(600m, vm.RegularIncomeLaborIncomeYear);
        Assert.Equal(300m, vm.RegularIncomeBusinessIncomeYear);
        Assert.Equal(100m, vm.RegularIncomePensionIncomeYear);
        Assert.Equal(0m, vm.RegularIncomeFinancialIncomeYear); // subclass absent from the breakdown -> default
        Assert.Equal(60.0, vm.PercentageOfRegularIncomeLaborIncomeYear);
        Assert.Equal(30.0, vm.PercentageOfRegularIncomeBusinessIncomeYear);
        Assert.Equal(10.0, vm.PercentageOfRegularIncomePensionIncomeYear);
    }

    /// <summary>To view model copies irregular income breakdown for the year onto it's named properties, independently of RegularIncome.</summary>
    [Fact]
    public void ToViewModel_CopiesIrregularIncomeBreakdown_Year()
    {
        var dto = BaseDto();
        dto.YearIncomeBreakdown = new Dictionary<string, FinanceBreakdownDto>
        {
            ["RegularIncome"] = Breakdown(999m, ("LaborIncome", 999m, 100.0)), // must not bleed into IrregularIncome
            ["IrregularIncome"] = Breakdown(1000m, ("LaborIncome", 750m, 75.0), ("OtherIncome", 250m, 25.0))
        };

        var vm = dto.ToViewModel(Utc);

        Assert.Equal(750m, vm.IrregularIncomeLaborIncomeYear);
        Assert.Equal(250m, vm.IrregularIncomeOtherIncomeYear);
        Assert.Equal(75.0, vm.PercentageOfIrregularIncomeLaborIncomeYear);
        Assert.Equal(25.0, vm.PercentageOfIrregularIncomeOtherIncomeYear);
    }

    /// <summary>To view model defaults regular income amounts and percentages to zero when no breakdown entry exists for that main class.</summary>
    [Fact]
    public void ToViewModel_RegularIncomeMainClassAbsent_AmountsAndPercentagesDefaultToZero()
    {
        var dto = BaseDto(); // YearIncomeBreakdown left empty

        var vm = dto.ToViewModel(Utc);

        Assert.Equal(0m, vm.RegularIncomeLaborIncomeYear);
        Assert.Equal(0.0, vm.PercentageOfRegularIncomeLaborIncomeYear);
        Assert.Equal(0.0, vm.PercentageOfRegularIncomeBusinessIncomeYear);
    }

    /// <summary>To view model copies the Year and YearMonth income breakdowns independently.</summary>
    [Fact]
    public void ToViewModel_CopiesRegularIncomeBreakdown_ForYearAndYearMonth_Independently()
    {
        var dto = BaseDto();
        dto.YearIncomeBreakdown = new Dictionary<string, FinanceBreakdownDto> { ["RegularIncome"] = Breakdown(1000m, ("LaborIncome", 1000m, 100.0)) };
        dto.YearMonthIncomeBreakdown = new Dictionary<string, FinanceBreakdownDto>
        {
            ["RegularIncome"] = Breakdown(500m, ("LaborIncome", 400m, 80.0), ("BusinessIncome", 100m, 20.0))
        };

        var vm = dto.ToViewModel(Utc);

        Assert.Equal(1000m, vm.RegularIncomeLaborIncomeYear);
        Assert.Equal(400m, vm.RegularIncomeLaborIncomeYearMonth);
        Assert.Equal(100m, vm.RegularIncomeBusinessIncomeYearMonth);
        Assert.Equal(80.0, vm.PercentageOfRegularIncomeLaborIncomeYearMonth);
    }

    // -- Expenditure breakdown copy: RegularSavings ----------------------------

    /// <summary>To view model copies regular savings breakdown for the year onto it's named properties.</summary>
    [Fact]
    public void ToViewModel_CopiesRegularSavingsBreakdown_Year()
    {
        var dto = BaseDto();
        dto.YearExpenditureBreakdown = new Dictionary<string, FinanceBreakdownDto>
        {
            ["RegularSavings"] = Breakdown(1000m, ("Deposit", 700m, 70.0), ("Investment", 300m, 30.0))
        };

        var vm = dto.ToViewModel(Utc);

        Assert.Equal(700m, vm.RegularSavingsDepositYear);
        Assert.Equal(300m, vm.RegularSavingsInvestmentYear);
        Assert.Equal(70.0, vm.PercentageOfRegularSavingsDepositYear);
        Assert.Equal(30.0, vm.PercentageOfRegularSavingsInvestmentYear);
    }

    // -- Expenditure breakdown copy: NonConsumerSpending -----------------------

    /// <summary>To view model copies every non-consumer-spending subclass for the year onto it's named property.</summary>
    [Fact]
    public void ToViewModel_CopiesNonConsumerSpendingBreakdown_Year()
    {
        var dto = BaseDto();
        dto.YearExpenditureBreakdown = new Dictionary<string, FinanceBreakdownDto>
        {
            ["NonConsumerSpending"] = Breakdown(1000m,
                ("PublicPension", 200m, 20.0), ("DebtRepayment", 200m, 20.0), ("Tax", 200m, 20.0),
                ("SocialInsurance", 200m, 20.0), ("InterHouseholdTransferExpenses", 100m, 10.0),
                ("NonProfitOrganizationTransfer", 100m, 10.0))
        };

        var vm = dto.ToViewModel(Utc);

        Assert.Equal(200m, vm.NonConsumerSpendingPublicPensionYear);
        Assert.Equal(200m, vm.NonConsumerSpendingDebtRepaymentYear);
        Assert.Equal(200m, vm.NonConsumerSpendingTaxYear);
        Assert.Equal(200m, vm.NonConsumerSpendingSocialInsuranceYear);
        Assert.Equal(100m, vm.NonConsumerSpendingInterHouseholdTransferExpensesYear);
        Assert.Equal(100m, vm.NonConsumerSpendingNonProfitOrganizationTransferYear);
        Assert.Equal(20.0, vm.PercentageOfNonConsumerSpendingPublicPensionYear);
        Assert.Equal(10.0, vm.PercentageOfNonConsumerSpendingInterHouseholdTransferExpensesYear);
    }

    // -- Expenditure breakdown copy: ConsumerSpending (all 12 sub-classes) -----

    /// <summary>To view model copies every consumer-spending subclass for the year onto it's named property.</summary>
    [Fact]
    public void ToViewModel_CopiesConsumerSpendingBreakdown_Year()
    {
        var dto = BaseDto();
        const double eachPct = 100.0 / 12.0;
        dto.YearExpenditureBreakdown = new Dictionary<string, FinanceBreakdownDto>
        {
            ["ConsumerSpending"] = Breakdown(1200m,
                ("MealOrEatOutExpenses", 100m, eachPct), ("HousingOrSuppliesCost", 100m, eachPct),
                ("EducationExpenses", 100m, eachPct), ("MedicalExpenses", 100m, eachPct),
                ("TransportationCost", 100m, eachPct), ("CommunicationCost", 100m, eachPct),
                ("LeisureOrCulture", 100m, eachPct), ("ClothingOrShoes", 100m, eachPct),
                ("PinMoney", 100m, eachPct), ("ProtectionTypeInsurance", 100m, eachPct),
                ("OtherExpenses", 100m, eachPct), ("UnknownExpenditure", 100m, eachPct))
        };

        var vm = dto.ToViewModel(Utc);

        Assert.Equal(100m, vm.ConsumerSpendingMealOrEatOutExpensesYear);
        Assert.Equal(100m, vm.ConsumerSpendingUnknownExpenditureYear);
        Assert.Equal(eachPct, vm.PercentageOfConsumerSpendingMealOrEatOutExpensesYear, precision: 10);
        Assert.Equal(eachPct, vm.PercentageOfConsumerSpendingCommunicationCostYear, precision: 10);
        Assert.Equal(eachPct, vm.PercentageOfConsumerSpendingUnknownExpenditureYear, precision: 10);
    }

    /// <summary>To view model copies every consumer-spending subclass for the year-month period onto it's named property.</summary>
    [Fact]
    public void ToViewModel_CopiesConsumerSpendingBreakdown_YearMonth()
    {
        var dto = BaseDto();
        const double eachPct = 100.0 / 12.0;
        dto.YearMonthExpenditureBreakdown = new Dictionary<string, FinanceBreakdownDto>
        {
            ["ConsumerSpending"] = Breakdown(1200m,
                ("MealOrEatOutExpenses", 100m, eachPct), ("CommunicationCost", 100m, eachPct),
                ("UnknownExpenditure", 100m, eachPct))
        };

        var vm = dto.ToViewModel(Utc);

        Assert.Equal(100m, vm.ConsumerSpendingMealOrEatOutExpensesYearMonth);
        Assert.Equal(100m, vm.ConsumerSpendingCommunicationCostYearMonth);
        Assert.Equal(100m, vm.ConsumerSpendingUnknownExpenditureYearMonth);
        Assert.Equal(eachPct, vm.PercentageOfConsumerSpendingMealOrEatOutExpensesYearMonth, precision: 10);
        Assert.Equal(eachPct, vm.PercentageOfConsumerSpendingCommunicationCostYearMonth, precision: 10);
        Assert.Equal(eachPct, vm.PercentageOfConsumerSpendingUnknownExpenditureYearMonth, precision: 10);
    }

    /// <summary>To view model defaults expenditure percentages to zero when no breakdown entry exists for that main class.</summary>
    [Fact]
    public void ToViewModel_ExpenditureMainClassAbsent_PercentagesDefaultToZero()
    {
        var dto = BaseDto(); // YearExpenditureBreakdown left empty

        var vm = dto.ToViewModel(Utc);

        Assert.Equal(0.0, vm.PercentageOfRegularSavingsDepositYear);
        Assert.Equal(0.0, vm.PercentageOfConsumerSpendingMealOrEatOutExpensesYear);
    }
}

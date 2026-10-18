using ErrorOr;
using Maroik.Core.Domain.Finances;
using Maroik.Core.Domain.Tests.Fixtures;
using Maroik.Core.Domain.ValueObjects;
using DomainIncome = Maroik.Core.Domain.Finances.Income;

namespace Maroik.Core.Domain.Tests.Finances;

/// <summary>
/// Boundaries and round trips of the finance rules: fixed schedules on the first / last month and a past maturity,
/// reconstituted incomes and expenditures, the default-currency fallback, a zero-total breakdown and an asset note.
/// </summary>
public class FinanceBoundaryTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime _now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Owner e-mail used by every aggregate built in these tests.</summary>
    private const string Email = "user@example.com";

    /// <summary>Result of registering a fixed income on the given deposit month/day (no maturity unless given).</summary>
    private static ErrorOr<FixedIncome> Income(short month, short day = 1, DateTime? maturity = null) =>
        FixedIncome.Register(Email, "RegularIncome", "LaborIncome", "c", 1000m, "KRW", "asset", month, day, maturity ?? FixedSchedulePolicy.NoMaturityDate, _now);

    /// <summary>Result of registering a fixed expenditure on the given deposit month/day (no maturity unless given).</summary>
    private static ErrorOr<FixedExpenditure> Expense(short month, short day = 1, DateTime? maturity = null) =>
        FixedExpenditure.Register(Email, "ConsumerSpending", "MealOrEatOutExpenses", "c", 1000m, "KRW", "asset", null, month, day, maturity ?? FixedSchedulePolicy.NoMaturityDate, _now);

    /// <summary>Verifies that months 1 and 12 are accepted as the deposit month.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public void FixedIncomeAndExpenditure_AcceptTheFirstAndLastMonth(short month)
    {
        Assert.False(Income(month).IsError);
        Assert.False(Expense(month).IsError);
    }

    /// <summary>Verifies that months 0 and 13 are refused as the deposit month.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void FixedIncomeAndExpenditure_RefuseMonthsOutsideOneToTwelve(short month)
    {
        ErrorAssert.Validation(Income(month), "FixedIncome.InvalidDepositMonth", "Deposit month must be between 1 and 12.");
        ErrorAssert.Validation(Expense(month), "FixedExpenditure.InvalidDepositMonth", "Deposit month must be between 1 and 12.");
    }

    /// <summary>Verifies the invalid-day and empty-field errors of fixed income and fixed expenditure registration.</summary>
    [Fact]
    public void FixedIncomeAndExpenditure_ErrorContract()
    {
        ErrorAssert.Validation(Income(2, 30), "FixedIncome.InvalidDepositDay", "Deposit day must be between 1 and {0} for month {1}.", 29, (short)2);
        ErrorAssert.Validation(Expense(2, 30), "FixedExpenditure.InvalidDepositDay", "Deposit day must be between 1 and {0} for month {1}.", 29, (short)2);

        ErrorAssert.Validation(FixedIncome.Register(Email, " ", "LaborIncome", "c", 1m, "KRW", "a", 1, 1, _now.AddDays(1), _now), "FixedIncome.MainClassEmpty", "Main income category cannot be empty.");
        ErrorAssert.Validation(FixedIncome.Register(Email, "RegularIncome", " ", "c", 1m, "KRW", "a", 1, 1, _now.AddDays(1), _now), "FixedIncome.SubClassEmpty", "Sub income category cannot be empty.");
        ErrorAssert.Validation(FixedIncome.Register(Email, "RegularIncome", "LaborIncome", "c", 1m, "KRW", " ", 1, 1, _now.AddDays(1), _now), "FixedIncome.DepositAssetEmpty", "Deposit asset product name cannot be empty.");
        ErrorAssert.Validation(FixedExpenditure.Register(Email, " ", "MealOrEatOutExpenses", "c", 1m, "KRW", "a", null, 1, 1, _now.AddDays(1), _now), "FixedExpenditure.MainClassEmpty", "Main expense category cannot be empty.");
        ErrorAssert.Validation(FixedExpenditure.Register(Email, "ConsumerSpending", " ", "c", 1m, "KRW", "a", null, 1, 1, _now.AddDays(1), _now), "FixedExpenditure.SubClassEmpty", "Sub expense category cannot be empty.");
        ErrorAssert.Validation(FixedExpenditure.Register(Email, "ConsumerSpending", "MealOrEatOutExpenses", "c", 1m, "KRW", " ", null, 1, 1, _now.AddDays(1), _now), "FixedExpenditure.PaymentMethodEmpty", "Payment method cannot be empty.");
    }

    /// <summary>Verifies that a maturity date in the past is refused both when registering and when updating.</summary>
    [Fact]
    public void FixedIncomeAndExpenditure_MaturityInThePast_IsRefusedOnRegisterAndOnUpdate()
    {
        DateTime past = _now.AddDays(-30);
        const string template = "The maturity date cannot be earlier than the current date.";

        ErrorAssert.Validation(Income(1, 1, past), "FixedIncome.MaturityDateInPast", template);
        ErrorAssert.Validation(Expense(1, 1, past), "FixedExpenditure.MaturityDateInPast", template);
        ErrorAssert.Validation(Income(1).Value.Update("RegularIncome", "LaborIncome", "c", 1m, "KRW", "asset", 1, 1, past, null, _now), "FixedIncome.MaturityDateInPast", template);
        ErrorAssert.Validation(Expense(1).Value.Update("ConsumerSpending", "MealOrEatOutExpenses", "c", 1m, "KRW", "asset", null, 1, 1, past, null, _now), "FixedExpenditure.MaturityDateInPast", template);
    }

    /// <summary>Verifies the edges of the notice window and the one-day grace for a maturity date.</summary>
    [Fact]
    public void FixedSchedulePolicy_Boundaries()
    {
        DateTime today = new(2026, 6, 15, 13, 45, 0);

        Assert.True(FixedSchedulePolicy.IsNoticed(6, 15, today, 3, false));   // the deposit day itself is inside the window
        Assert.True(FixedSchedulePolicy.IsNoticed(6, 18, today, 3, false));   // exactly window days ahead
        Assert.False(FixedSchedulePolicy.IsNoticed(6, 19, today, 3, false));  // one day beyond
        Assert.False(FixedSchedulePolicy.IsNoticed(6, 14, today, 3, false));  // yesterday: this year's occurrence has passed

        Assert.True(FixedSchedulePolicy.IsAcceptableMaturityDate(today.AddDays(-1), today));  // one day of grace for timezones
        Assert.False(FixedSchedulePolicy.IsAcceptableMaturityDate(today.AddDays(-2), today));
        Assert.Equal("9999-12-31", FixedSchedulePolicy.NoMaturityDateIso);
    }

    /// <summary>Verifies that <c>Income</c> and <c>Expenditure</c> reconstitution restore every field.</summary>
    [Fact]
    public void IncomeAndExpenditure_Reconstitute_RestoreEveryFieldVerbatim()
    {
        DateTime created = new(2025, 1, 2), updated = new(2025, 3, 4);

        DomainIncome income = DomainIncome.Reconstitute(7, Email, "RegularIncome", "LaborIncome", "c", 123.45m, "KRW", "asset", "n", created, updated);
        Assert.Equal((7, Email, "RegularIncome", "LaborIncome", "c", "asset", "n", created, updated),
            (income.Id, income.AccountEmail.Value, income.MainClass, income.SubClass, income.Content, income.DepositMyAssetProductName, income.Note, income.Created, income.Updated));
        Assert.Equal(Money.Create(123.45m, "KRW").Value, income.Amount);

        Expenditure expenditure = Expenditure.Reconstitute(8, Email, "ConsumerSpending", "MealOrEatOutExpenses", "c", 5m, "KRW", "pay", "dep", "n", created, updated);
        Assert.Equal((8, Email, "ConsumerSpending", "MealOrEatOutExpenses", "c", "pay", "dep", "n", created, updated),
            (expenditure.Id, expenditure.AccountEmail.Value, expenditure.MainClass, expenditure.SubClass, expenditure.Content, expenditure.PaymentMethod, expenditure.MyDepositAsset, expenditure.Note, expenditure.Created, expenditure.Updated));
        Assert.Equal(Money.Create(5m, "KRW").Value, expenditure.Amount);
    }

    /// <summary>Verifies that a still-used unit is kept, an unused one falls back to the most common asset currency, and no assets yield <see langword="null"/>.</summary>
    [Fact]
    public void DefaultMonetaryUnit_FallsBackToTheMostCommonCurrency()
    {
        IReadOnlyCollection<Asset> assets = [A("a", "USD"), A("b", "KRW"), A("c", "KRW")];

        Assert.Equal("KRW", DefaultMonetaryUnitPolicy.Resolve(CurrencyCode.Create("EUR").Value, assets)?.Value);
        Assert.Equal("USD", DefaultMonetaryUnitPolicy.Resolve(CurrencyCode.Create("USD").Value, assets)?.Value); // a still-valid current unit wins over popularity
        Assert.Null(DefaultMonetaryUnitPolicy.Resolve(CurrencyCode.Create("KRW").Value, []));
        return;
        static Asset A(string name, string unit) => Asset.Create(name, Email, "FreeDepositAndWithdrawal", 1m, unit, _now).Value;
    }

    /// <summary>Verifies that a breakdown whose total is zero reports amounts but no percentages.</summary>
    [Fact]
    public void FinanceBreakdown_WithATotalOfZero_HasNoPercentages()
    {
        FinanceBreakdownPolicy.SubClassBreakdown result = FinanceBreakdownPolicy.Compute([("a", 5m), ("a", -5m)]);

        Assert.Equal(0m, result.Total);
        Assert.Empty(result.PercentageBySubClass);
        Assert.Equal(0m, result.AmountBySubClass["a"]);
    }

    /// <summary>Verifies that the note passed to <c>Asset.Create</c> is kept.</summary>
    [Fact]
    public void Asset_Create_KeepsTheNote()
    {
        Assert.Equal("a note", Asset.Create("a", Email, "FreeDepositAndWithdrawal", 1m, "KRW", _now, "a note").Value.Note);
    }
}

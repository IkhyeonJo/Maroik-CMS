using Maroik.Core.Domain.Finance;
namespace Maroik.Core.Domain.Tests.Finance;

/// <summary>Unit tests for <see cref="DefaultUnitScope"/>: which records the dashboard counts in the default unit.</summary>
public class DefaultUnitScopeTests
{
    /// <summary>The fixed "current time" the test rows are stamped with.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>An asset of the test account.</summary>
    private static Asset AssetOf(string name, string currency, bool deleted = false) =>
        Asset.Reconstitute(name, "user@example.com", "CashAsset", 1000m, currency, null, deleted, Now, Now);

    /// <summary>An income deposited into <paramref name="asset"/>.</summary>
    private static Income IncomeInto(string asset) =>
        Income.Reconstitute(1, "user@example.com", "RegularIncome", "LaborIncome", null, 10m, "KRW", asset, null, Now, Now);

    /// <summary>An expenditure paid from <paramref name="asset"/>.</summary>
    private static Expenditure ExpenditureFrom(string asset) =>
        Expenditure.Reconstitute(1, "user@example.com", "ConsumerSpending", "MealOrEatOutExpenses", null, 10m, "KRW", asset, null, null, Now, Now);

    /// <summary>The scope over a KRW bank, a USD broker and a deleted KRW wallet.</summary>
    private static DefaultUnitScope KrwScope() =>
        DefaultUnitScope.Of("KRW", [AssetOf("Bank", "KRW"), AssetOf("Broker", "USD"), AssetOf("OldWallet", "KRW", deleted: true)]);

    /// <summary>Every asset's currency is listed by product name, the deleted one included.</summary>
    [Fact]
    public void CurrencyByProduct_ListsEveryAsset_IncludingDeletedOnes()
        => Assert.Equal<IReadOnlyDictionary<string, string>>(
            new Dictionary<string, string> { ["Bank"] = "KRW", ["Broker"] = "USD", ["OldWallet"] = "KRW" },
            KrwScope().CurrencyByProduct);

    /// <summary>The scope hands out its lookup read-only: a caller that needs to change it must take a copy.</summary>
    [Fact]
    public void CurrencyByProduct_IsReadOnly()
        => Assert.IsNotType<Dictionary<string, string>>(KrwScope().CurrencyByProduct, exactMatch: false);

    /// <summary>An income counts when its deposit asset carries the default unit, a deleted asset included.</summary>
    [Theory]
    [InlineData("Bank", true)]
    [InlineData("OldWallet", true)]
    [InlineData("Broker", false)]
    [InlineData("Unknown", false)]
    public void Includes_AnIncome_ByItsDepositAssetsCurrency(string asset, bool counted)
        => Assert.Equal(counted, KrwScope().Includes(IncomeInto(asset)));

    /// <summary>An expenditure counts when its payment asset carries the default unit, a deleted asset included.</summary>
    [Theory]
    [InlineData("Bank", true)]
    [InlineData("OldWallet", true)]
    [InlineData("Broker", false)]
    [InlineData("Unknown", false)]
    public void Includes_AnExpenditure_ByItsPaymentAssetsCurrency(string asset, bool counted)
        => Assert.Equal(counted, KrwScope().Includes(ExpenditureFrom(asset)));
}

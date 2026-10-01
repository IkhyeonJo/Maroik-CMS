using Maroik.Core.Domain.Finance;
using Maroik.Core.Domain.ValueObjects;
namespace Maroik.Core.Domain.Tests.Finance;

/// <summary>
/// Unit tests for <see cref="Maroik.Core.Domain.Finance.Asset"/>.
/// Covers creation validation, deposit/withdraw operations, balance setting with currency-mismatch guard,
/// update logic, note management, and soft-delete.
/// </summary>
public class AssetTests
{
    /// <summary>Creates a valid asset; each argument can be overridden per test.</summary>
    private static Asset ValidAsset(
        string productName = "My Bank",
        string email = "user@example.com",
        string item = "FreeDepositAndWithdrawal",
        decimal amount = 1000m,
        string currency = "KRW")
        => Asset.Create(productName, email, item, amount, currency).Value;

    // -- Create ---------------------------------------------------------------

    /// <summary>Create returns asset, when valid.</summary>
    [Fact]
    public void Create_ReturnsAsset_WhenValid()
    {
        var result = Asset.Create("Savings", "user@example.com", "FreeDepositAndWithdrawal", 5000m, "KRW");

        Assert.False(result.IsError);
        Assert.Equal("Savings", result.Value.ProductName);
        Assert.Equal(5000m, result.Value.Balance.Amount);
    }

    /// <summary>Create returns error, when product name empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ReturnsError_WhenProductNameEmpty(string? name)
    {
        var result = Asset.Create(name, "user@example.com", "FreeDepositAndWithdrawal", 0m, "KRW");

        Assert.True(result.IsError);
        Assert.Equal("Asset.ProductNameEmpty", result.FirstError.Code);
    }

    /// <summary>Create returns error, when item empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ReturnsError_WhenItemEmpty(string? item)
    {
        var result = Asset.Create("Savings", "user@example.com", item, 0m, "KRW");

        Assert.True(result.IsError);
        Assert.Equal("Asset.ItemEmpty", result.FirstError.Code);
    }

    /// <summary>
    /// Regression: Create rejects an <c>Item</c> value outside <see cref="AssetItemType"/>'s known
    /// members — previously only <c>AssetService</c> checked this, so the aggregate itself could be
    /// constructed (and would then fail with a raw DB <c>Asset_Item_check</c> exception on save) with
    /// an unrecognized category.
    /// </summary>
    [Theory]
    [InlineData("Deposit")]
    [InlineData("Stock")]
    [InlineData("freeDepositAndWithdrawal")]
    public void Create_ReturnsError_WhenItemNotRecognised(string item)
    {
        var result = Asset.Create("Savings", "user@example.com", item, 0m, "KRW");

        Assert.True(result.IsError);
        Assert.Equal("Asset.ItemInvalid", result.FirstError.Code);
    }

    /// <summary>Create returns error, when email invalid.</summary>
    [Fact]
    public void Create_ReturnsError_WhenEmailInvalid()
    {
        var result = Asset.Create("Savings", "bademail", "FreeDepositAndWithdrawal", 0m, "KRW");

        Assert.True(result.IsError);
    }

    /// <summary>Create returns error, when currency empty.</summary>
    [Fact]
    public void Create_ReturnsError_WhenCurrencyEmpty()
    {
        var result = Asset.Create("Savings", "user@example.com", "FreeDepositAndWithdrawal", 0m, null);

        Assert.True(result.IsError);
        Assert.Equal("Money.CurrencyEmpty", result.FirstError.Code);
    }

    // -- Deposit / Withdraw ----------------------------------------------------

    /// <summary>Deposit increases balance.</summary>
    [Fact]
    public void Deposit_IncreasesBalance()
    {
        var asset = ValidAsset(amount: 1000m);

        asset.Deposit(Money.Create(500m, "KRW").Value);

        Assert.Equal(1500m, asset.Balance.Amount);
    }

    /// <summary>Withdraw decreases balance.</summary>
    [Fact]
    public void Withdraw_DecreasesBalance()
    {
        var asset = ValidAsset(amount: 1000m);

        asset.Withdraw(Money.Create(300m, "KRW").Value);

        Assert.Equal(700m, asset.Balance.Amount);
    }

    /// <summary>
    /// Regression: the balance lives in a numeric(20,4) column, so a deposit that would take it past
    /// <see cref="FinanceAmountPolicy.MaxAbsoluteAmount"/> is a clean validation error (and leaves the
    /// balance untouched) instead of a raw numeric-overflow at save.
    /// </summary>
    [Fact]
    public void Deposit_ReturnsError_AndLeavesBalanceUntouched_WhenItWouldExceedTheRange()
    {
        var asset = ValidAsset(amount: FinanceAmountPolicy.MaxAbsoluteAmount);

        var result = asset.Deposit(Money.Create(0.01m, "KRW").Value);

        Assert.True(result.IsError);
        Assert.Equal("Finance.AmountOutOfRange", result.FirstError.Code);
        Assert.Equal(FinanceAmountPolicy.MaxAbsoluteAmount, asset.Balance.Amount);
    }

    /// <summary>The same bound applies in the negative direction on a withdrawal.</summary>
    [Fact]
    public void Withdraw_ReturnsError_AndLeavesBalanceUntouched_WhenItWouldExceedTheNegativeRange()
    {
        var asset = ValidAsset(amount: -FinanceAmountPolicy.MaxAbsoluteAmount);

        var result = asset.Withdraw(Money.Create(0.01m, "KRW").Value);

        Assert.True(result.IsError);
        Assert.Equal("Finance.AmountOutOfRange", result.FirstError.Code);
        Assert.Equal(-FinanceAmountPolicy.MaxAbsoluteAmount, asset.Balance.Amount);
    }

    /// <summary>A change that lands exactly on the limit is still allowed.</summary>
    [Fact]
    public void Deposit_Succeeds_WhenItLandsExactlyOnTheLimit()
    {
        var asset = ValidAsset(amount: FinanceAmountPolicy.MaxAbsoluteAmount - 1m);

        var result = asset.Deposit(Money.Create(1m, "KRW").Value);

        Assert.False(result.IsError);
        Assert.Equal(FinanceAmountPolicy.MaxAbsoluteAmount, asset.Balance.Amount);
    }

    /// <summary>Deposit returns error, when currency mismatch.</summary>
    [Fact]
    public void Deposit_ReturnsError_WhenCurrencyMismatch()
    {
        var asset = ValidAsset(amount: 1000m, currency: "KRW");

        var result = asset.Deposit(Money.Create(500m, "USD").Value);

        Assert.True(result.IsError);
        Assert.Equal("Asset.CurrencyMismatch", result.FirstError.Code);
        Assert.Equal(1000m, asset.Balance.Amount);
    }

    /// <summary>Withdraw returns error, when currency mismatch.</summary>
    [Fact]
    public void Withdraw_ReturnsError_WhenCurrencyMismatch()
    {
        var asset = ValidAsset(amount: 1000m, currency: "KRW");

        var result = asset.Withdraw(Money.Create(300m, "USD").Value);

        Assert.True(result.IsError);
        Assert.Equal("Asset.CurrencyMismatch", result.FirstError.Code);
        Assert.Equal(1000m, asset.Balance.Amount);
    }

    // -- SetBalance ------------------------------------------------------------

    /// <summary>Set balance succeeds, when same currency.</summary>
    [Fact]
    public void SetBalance_Succeeds_WhenSameCurrency()
    {
        var asset = ValidAsset(amount: 1000m, currency: "KRW");
        var newBalance = Money.Create(9999m, "KRW").Value;

        var result = asset.SetBalance(newBalance);

        Assert.False(result.IsError);
        Assert.Equal(9999m, asset.Balance.Amount);
    }

    /// <summary>Set balance returns error, when currency mismatch.</summary>
    [Fact]
    public void SetBalance_ReturnsError_WhenCurrencyMismatch()
    {
        var asset = ValidAsset(currency: "KRW");
        var usdBalance = Money.Create(100m, "USD").Value;

        var result = asset.SetBalance(usdBalance);

        Assert.True(result.IsError);
        Assert.Equal("Asset.CurrencyMismatch", result.FirstError.Code);
    }

    // -- Update ----------------------------------------------------------------

    /// <summary>Update succeeds, when valid.</summary>
    [Fact]
    public void Update_Succeeds_WhenValid()
    {
        var asset = ValidAsset();

        var result = asset.Update("New Name", "InvestmentAsset", 2000m, "KRW", "memo", false);

        Assert.False(result.IsError);
        Assert.Equal("New Name", asset.ProductName);
        Assert.Equal("InvestmentAsset", asset.Item);
        Assert.Equal(2000m, asset.Balance.Amount);
    }

    /// <summary>Update returns error, when product name empty.</summary>
    [Fact]
    public void Update_ReturnsError_WhenProductNameEmpty()
    {
        var asset = ValidAsset();

        var result = asset.Update("", "FreeDepositAndWithdrawal", 0m, "KRW", null, false);

        Assert.True(result.IsError);
        Assert.Equal("Asset.ProductNameEmpty", result.FirstError.Code);
    }

    // -- Update (item validation) ----------------------------------------------

    /// <summary>Update returns error, when item empty.</summary>
    [Fact]
    public void Update_ReturnsError_WhenItemEmpty()
    {
        var asset = ValidAsset();

        var result = asset.Update("Name", "", 0m, "KRW", null, false);

        Assert.True(result.IsError);
        Assert.Equal("Asset.ItemEmpty", result.FirstError.Code);
    }

    /// <summary>Regression: Update rejects an <c>Item</c> value outside <see cref="AssetItemType"/>'s known members (see <see cref="Create_ReturnsError_WhenItemNotRecognised"/>).</summary>
    [Fact]
    public void Update_ReturnsError_WhenItemNotRecognised()
    {
        var asset = ValidAsset();

        var result = asset.Update("Name", "Stock", 1000m, "KRW", null, false);

        Assert.True(result.IsError);
        Assert.Equal("Asset.ItemInvalid", result.FirstError.Code);
    }

    /// <summary>Update returns error and leaves the balance unchanged, when currency is empty.</summary>
    [Fact]
    public void Update_ReturnsError_WhenCurrencyEmpty()
    {
        var asset = ValidAsset();
        string originalCurrency = asset.Balance.Currency;

        var result = asset.Update("Name", "FreeDepositAndWithdrawal", 1000m, "", null, false);

        Assert.True(result.IsError);
        Assert.Equal("Money.CurrencyEmpty", result.FirstError.Code);
        Assert.Equal(originalCurrency, asset.Balance.Currency);
    }

    // -- UpdateNote ------------------------------------------------------------

    /// <summary>Update note sets note.</summary>
    [Fact]
    public void UpdateNote_SetsNote()
    {
        var asset = ValidAsset();

        asset.UpdateNote("memo text");

        Assert.Equal("memo text", asset.Note);
    }

    /// <summary>Update note clears note, when null.</summary>
    [Fact]
    public void UpdateNote_ClearsNote_WhenNull()
    {
        var asset = ValidAsset();
        asset.UpdateNote("memo");

        asset.UpdateNote(null);

        Assert.Null(asset.Note);
    }

    // -- SoftDelete ------------------------------------------------------------

    /// <summary>Soft delete sets deleted true.</summary>
    [Fact]
    public void SoftDelete_SetsDeletedTrue()
    {
        var asset = ValidAsset();

        asset.SoftDelete();

        Assert.True(asset.Deleted);
    }
}

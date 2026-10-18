using Maroik.Core.Domain.Finances;
using Maroik.Core.Domain.ValueObjects;
namespace Maroik.Core.Domain.Tests.Finances;

/// <summary>
/// Unit tests for <see cref="BalanceMovement"/>, the movements an <see cref="Expenditure"/> / <see cref="Income"/>
/// makes, and <see cref="Asset.Apply"/>.
/// </summary>
public class BalanceMovementTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>An amount in the given currency.</summary>
    private static Money MoneyOf(decimal amount, string currency = "KRW") => Money.Create(amount, currency).Value;

    /// <summary>An expenditure of 300 KRW paid from "Card", into <paramref name="depositAsset"/> when it is a transfer.</summary>
    private static Expenditure ExpenditureOf(string? depositAsset)
        => Expenditure.Reconstitute(1, "user@example.com", "ConsumerSpending", "MealOrEatOutExpenses", null,
            300m, "KRW", "Card", depositAsset, null, Now, Now);

    // -- Record movements -------------------------------------------------------

    /// <summary>A non-transfer expenditure only withdraws from its payment asset.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Expenditure_WithdrawsFromThePaymentAsset_WhenItIsNotATransfer(string? depositAsset)
        => Assert.Equal([new BalanceMovement("Card", MoneyOf(300m), IsDeposit: false)],
            ExpenditureOf(depositAsset).BalanceMovements());

    /// <summary>A transfer withdraws from the payment asset, then deposits into the deposit asset.</summary>
    [Fact]
    public void Expenditure_WithdrawsThenDeposits_WhenItIsATransfer()
        => Assert.Equal(
            [new BalanceMovement("Card", MoneyOf(300m), IsDeposit: false), new BalanceMovement("Savings", MoneyOf(300m), IsDeposit: true)],
            ExpenditureOf("Savings").BalanceMovements());

    /// <summary>An income deposits into its deposit asset.</summary>
    [Fact]
    public void Income_DepositsIntoItsDepositAsset()
    {
        var income = Income.Reconstitute(1, "user@example.com", "RegularIncome", "LaborIncome", null,
            500m, "USD", "Bank", null, Now, Now);

        Assert.Equal([new BalanceMovement("Bank", MoneyOf(500m, "USD"), IsDeposit: true)], income.BalanceMovements());
    }

    // -- Reversing --------------------------------------------------------------

    /// <summary>Reversing flips the direction and keeps the asset and amount.</summary>
    [Fact]
    public void Reversed_FlipsTheDirection()
    {
        Assert.Equal(new BalanceMovement("Card", MoneyOf(1m), IsDeposit: true), new BalanceMovement("Card", MoneyOf(1m), IsDeposit: false).Reversed());
        Assert.Equal(new BalanceMovement("Card", MoneyOf(1m), IsDeposit: false), new BalanceMovement("Card", MoneyOf(1m), IsDeposit: true).Reversed());
    }

    /// <summary>Undoing a record reverses each of its movements, in order.</summary>
    [Fact]
    public void Undoing_ReversesEveryMovement_InOrder()
        => Assert.Equal(
            [new BalanceMovement("Card", MoneyOf(300m), IsDeposit: true), new BalanceMovement("Savings", MoneyOf(300m), IsDeposit: false)],
            BalanceMovement.Undoing(ExpenditureOf("Savings").BalanceMovements()));

    /// <summary>Replacing undoes the earlier movements first, then applies the new ones.</summary>
    [Fact]
    public void Replacing_UndoesTheEarlierMovements_ThenAppliesTheNewOnes()
        => Assert.Equal(
            [
                new BalanceMovement("Card", MoneyOf(300m), IsDeposit: true),
                new BalanceMovement("Bank", MoneyOf(50m), IsDeposit: false),
            ],
            BalanceMovement.Replacing(
                [new BalanceMovement("Card", MoneyOf(300m), IsDeposit: false)],
                [new BalanceMovement("Bank", MoneyOf(50m), IsDeposit: false)]));

    // -- Asset.Apply ------------------------------------------------------------

    /// <summary>A deposit movement adds its amount to the balance and stamps Updated.</summary>
    [Fact]
    public void Apply_Deposits_ForADepositMovement()
    {
        var asset = Asset.Reconstitute("Card", "user@example.com", "CashAsset", 1000m, "KRW", null, false, Now, Now);

        Assert.False(asset.Apply(new BalanceMovement("Card", MoneyOf(300m), IsDeposit: true), Now.AddDays(1)).IsError);

        Assert.Equal(1300m, asset.Balance.Amount);
        Assert.Equal(Now.AddDays(1), asset.Updated);
    }

    /// <summary>A withdrawal movement subtracts its amount from the balance.</summary>
    [Fact]
    public void Apply_Withdraws_ForAWithdrawalMovement()
    {
        var asset = Asset.Reconstitute("Card", "user@example.com", "CashAsset", 1000m, "KRW", null, false, Now, Now);

        Assert.False(asset.Apply(new BalanceMovement("Card", MoneyOf(300m), IsDeposit: false), Now).IsError);

        Assert.Equal(700m, asset.Balance.Amount);
    }

    /// <summary>A movement in another currency is refused with the asset's currency-mismatch error and leaves the balance.</summary>
    [Fact]
    public void Apply_ReturnsTheCurrencyMismatchError_ForAMovementInAnotherCurrency()
    {
        var asset = Asset.Reconstitute("Card", "user@example.com", "CashAsset", 1000m, "KRW", null, false, Now, Now);

        var result = asset.Apply(new BalanceMovement("Card", MoneyOf(300m, "USD"), IsDeposit: false), Now);

        Assert.Equal("Asset.CurrencyMismatch", result.FirstError.Code);
        Assert.Equal(1000m, asset.Balance.Amount);
    }
}

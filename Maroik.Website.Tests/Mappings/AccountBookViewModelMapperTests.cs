using Maroik.Core.Contract.Dtos;
using Maroik.Website.Mappings;
using Maroik.Website.Models.ViewModels.AccountBook;

namespace Maroik.Website.Tests.Mappings;

/// <summary>Unit tests for <see cref="AccountBookViewModelMapper"/>.</summary>
public class AccountBookViewModelMapperTests
{
    private static Func<string, string> Identity => key => key;
    /// <summary>A fake localizer that prefixes each key with <paramref name="p"/>.</summary>
    private static Func<string, string> Prefix(string p) => key => $"{p}{key}";

    // ── Asset ─────────────────────────────────────────────────────────────────

    /// <summary>Asset to display view model localizes item.</summary>
    [Fact]
    public void AssetToDisplayViewModel_LocalizesItem()
    {
        var response = new AssetResponse { Item = "Deposit" };
        var vm = response.ToDisplayViewModel(Prefix("L:"), "UTC");
        Assert.Equal("L:Deposit", vm.Item);
    }

    /// <summary>Asset to display view model copies scalar fields.</summary>
    [Fact]
    public void AssetToDisplayViewModel_CopiesScalarFields()
    {
        var response = new AssetResponse
        {
            ProductName = "Savings",
            Amount = 1234.5m,
            MonetaryUnit = "KRW",
            Note = "test note",
            Deleted = true
        };
        var vm = response.ToDisplayViewModel(Identity, "UTC");
        Assert.Equal("Savings", vm.ProductName);
        Assert.Equal(1234.5m, vm.Amount);
        Assert.Equal("KRW", vm.MonetaryUnit);
        Assert.Equal("test note", vm.Note);
        Assert.True(vm.Deleted);
    }

    /// <summary>Asset to display view model converts timestamp to seoul.</summary>
    [Fact]
    public void AssetToDisplayViewModel_ConvertsTimestamp_ToSeoul()
    {
        // 2025-01-01 00:00:00 UTC → 2025-01-01 09:00:00 KST (UTC+9)
        var utc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var vm = new AssetResponse { Created = utc, Updated = utc }
            .ToDisplayViewModel(Identity, "Asia/Seoul");
        Assert.Equal(9, vm.Created.Hour);
        Assert.Equal(9, vm.Updated.Hour);
    }

    /// <summary>Assets to display view models maps all.</summary>
    [Fact]
    public void AssetsToDisplayViewModels_MapsAll()
    {
        var responses = new[]
        {
            new AssetResponse { ProductName = "A" },
            new AssetResponse { ProductName = "B" }
        };
        var vms = responses.ToDisplayViewModels(Identity, "UTC");
        Assert.Equal(2, vms.Count);
    }

    // ── Income ────────────────────────────────────────────────────────────────

    /// <summary>Income to display view model localizes class fields.</summary>
    [Fact]
    public void IncomeToDisplayViewModel_LocalizesClassFields()
    {
        var response = new IncomeResponse { MainClass = "RegularIncome", SubClass = "LaborIncome" };
        var vm = response.ToDisplayViewModel(
            new Dictionary<string, string?> { ["prod"] = "KRW" },
            Prefix("L:"),
            "UTC");
        Assert.Equal("L:RegularIncome", vm.MainClass);
        Assert.Equal("L:LaborIncome", vm.SubClass);
    }

    /// <summary>Income to display view model populates monetary unit from asset.</summary>
    [Fact]
    public void IncomeToDisplayViewModel_PopulatesMonetaryUnitFromAsset()
    {
        var response = new IncomeResponse { DepositMyAssetProductName = "MyBank" };
        var currency = new Dictionary<string, string?> { ["MyBank"] = "USD" };
        var vm = response.ToDisplayViewModel(currency, Identity, "UTC");
        Assert.Equal("USD", vm.MonetaryUnit);
    }

    /// <summary>Income to display view model monetary unit null when asset not found.</summary>
    [Fact]
    public void IncomeToDisplayViewModel_MonetaryUnit_NullWhenAssetNotFound()
    {
        var response = new IncomeResponse { DepositMyAssetProductName = "Unknown" };
        var vm = response.ToDisplayViewModel(new Dictionary<string, string?>(), Identity, "UTC");
        Assert.Null(vm.MonetaryUnit);
    }

    // ── Expenditure ───────────────────────────────────────────────────────────

    /// <summary>Expenditure to display view model localizes class fields.</summary>
    [Fact]
    public void ExpenditureToDisplayViewModel_LocalizesClassFields()
    {
        var response = new ExpenditureResponse { MainClass = "ConsumerSpending", SubClass = "MealOrEatOutExpenses" };
        var vm = response.ToDisplayViewModel(
            new Dictionary<string, string?> { ["card"] = "KRW" },
            Prefix("L:"),
            "UTC");
        Assert.Equal("L:ConsumerSpending", vm.MainClass);
        Assert.Equal("L:MealOrEatOutExpenses", vm.SubClass);
    }

    /// <summary>Expenditure to display view model populates monetary unit from payment method.</summary>
    [Fact]
    public void ExpenditureToDisplayViewModel_PopulatesMonetaryUnitFromPaymentMethod()
    {
        var response = new ExpenditureResponse { PaymentMethod = "CreditCard" };
        var currency = new Dictionary<string, string?> { ["CreditCard"] = "KRW" };
        var vm = response.ToDisplayViewModel(currency, Identity, "UTC");
        Assert.Equal("KRW", vm.MonetaryUnit);
    }

    // ── IsValidLocalDateTime / ToCreatedUtc ──────────────────────────────────────
    // Created is the account's own local wall-clock time ("yyyy-MM-dd HH:mm:ss"), converted to
    // UTC using the account's IANA time zone (known server-side from the session) — never the
    // browser's time zone, and never computed/converted on the client.

    /// <summary>Is valid local date time accepts the well-formed shape.</summary>
    [Theory]
    [InlineData("2026-01-15 09:30:00")]
    [InlineData("2026-12-31 23:59:59")]
    public void IsValidLocalDateTime_Accepts_WellFormedShape(string value)
    {
        Assert.True(AccountBookViewModelMapper.IsValidLocalDateTime(value));
    }

    /// <summary>Is valid local date time rejects malformed or missing input.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-date")]
    [InlineData("2026-01-15")]
    [InlineData("2026-01-15T09:30:00")]
    [InlineData("2026-13-01 09:30:00")]
    public void IsValidLocalDateTime_Rejects_MalformedOrMissingInput(string? value)
    {
        Assert.False(AccountBookViewModelMapper.IsValidLocalDateTime(value));
    }

    /// <summary>Income input view model to created utc converts using the given account time zone,
    /// not the machine's local time zone.</summary>
    [Fact]
    public void IncomeInputViewModel_ToCreatedUtc_ConvertsUsingGivenTimeZone()
    {
        var vm = new IncomeInputViewModel { Created = "2026-01-15 09:30:00" };
        DateTime utc = vm.ToCreatedUtc("Asia/Seoul");
        // 09:30 KST (UTC+9) -> 00:30 UTC the same day.
        Assert.Equal(new DateTime(2026, 1, 15, 0, 30, 0, DateTimeKind.Utc), utc);
    }

    /// <summary>Expenditure input view model to created utc converts using the given account time zone.</summary>
    [Fact]
    public void ExpenditureInputViewModel_ToCreatedUtc_ConvertsUsingGivenTimeZone()
    {
        var vm = new ExpenditureInputViewModel { Created = "2026-01-15 09:30:00" };
        DateTime utc = vm.ToCreatedUtc("Asia/Seoul");
        Assert.Equal(new DateTime(2026, 1, 15, 0, 30, 0, DateTimeKind.Utc), utc);
    }
}

using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Options;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="DashboardService"/>.
/// All repository dependencies are replaced with Moq mocks.
/// Covers the summary aggregation (per-year / per-year-month income and expenditure
/// breakdowns) and the default monetary unit synchronization logic.
/// </summary>
public class DashboardServiceTests
{
    private readonly Mock<IAccountRepository> _accountRepo = new();
    private readonly Mock<IAssetRepository> _assetRepo = new();
    private readonly Mock<IIncomeRepository> _incomeRepo = new();
    private readonly Mock<IExpenditureRepository> _expenditureRepo = new();
    private readonly Mock<IFixedIncomeRepository> _fixedIncomeRepo = new();
    private readonly Mock<IFixedExpenditureRepository> _fixedExpenditureRepo = new();

    private DashboardService CreateSut(ServerSetting? settings = null) => new(
        _accountRepo.Object,
        _assetRepo.Object,
        _incomeRepo.Object,
        _expenditureRepo.Object,
        _fixedIncomeRepo.Object,
        _fixedExpenditureRepo.Object,
        Options.Create(settings ?? new ServerSetting()));

    // -- Helpers --------------------------------------------------------------

    private static Asset MakeAsset(string name, string unit = "KRW", bool deleted = false) =>
        Asset.Reconstitute(
            productName: name,
            accountEmail: "user@example.com",
            item: "Deposit",
            amount: 1000m,
            monetaryUnit: unit,
            note: null,
            deleted: deleted,
            created: DateTime.UtcNow,
            updated: DateTime.UtcNow);

    private static Account MakeAccount(string email = "user@example.com", string? unit = null) =>
        Account.Reconstitute(
            email: email,
            hashedPassword: "hash",
            nickname: "TestUser",
            avatarImagePath: null,
            role: Role.User,
            timeZoneIanaId: "UTC",
            defaultMonetaryUnit: unit,
            locked: false,
            loginAttempt: 0,
            emailConfirmed: true,
            agreedServiceTerms: true,
            registrationToken: null,
            resetPasswordToken: null,
            created: DateTime.UtcNow,
            updated: DateTime.UtcNow,
            message: null,
            deleted: false,
            securityStamp: "stamp",
            mustChangePassword: false);

    private void SetupEmptyFinancials()
    {
        _incomeRepo.Setup(r => r.GetByAccountEmailAndDateRangeAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _expenditureRepo.Setup(r => r.GetByAccountEmailAndDateRangeAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _incomeRepo.Setup(r => r.GetFirstByAccountEmailOrderedByCreatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Income?)null);
        _expenditureRepo.Setup(r => r.GetFirstByAccountEmailOrderedByCreatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expenditure?)null);
    }

    // -- GetSummaryAsync ------------------------------------------------------

    /// <summary>Verifies that <c>GetSummaryAsync</c> returns expected selected year and month.</summary>
    [Fact]
    public async Task GetSummaryAsync_ReturnsExpectedSelectedYearAndMonth()
    {
        const string email = "user@example.com";
        var account = MakeAccount(unit: null);
        var assets = new List<Asset>();

        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(assets);
        _incomeRepo.Setup(r => r.GetFirstByAccountEmailOrderedByCreatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Income?)null);
        _expenditureRepo.Setup(r => r.GetFirstByAccountEmailOrderedByCreatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Expenditure?)null);
        var sut = CreateSut();

        DashboardDto summary = await sut.GetSummaryAsync(email, "2025", "3", "UTC", TestContext.Current.CancellationToken);

        Assert.Equal(2025, summary.SelectedYear);
        Assert.Equal(3, summary.SelectedMonth);
    }

    /// <summary>
    /// Verifies that the configured demo account always sees the configured fixed period,
    /// regardless of the year/month it was actually requested with.
    /// </summary>
    [Fact]
    public async Task GetSummaryAsync_OverridesYearAndMonth_ForConfiguredDemoAccount()
    {
        const string email = "demo@maroik.com";
        var account = MakeAccount(unit: null);
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _incomeRepo.Setup(r => r.GetFirstByAccountEmailOrderedByCreatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Income?)null);
        _expenditureRepo.Setup(r => r.GetFirstByAccountEmailOrderedByCreatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Expenditure?)null);
        var sut = CreateSut(new ServerSetting
        {
            DemoAccountEmail = email,
            DemoDashboardYear = "2025",
            DemoDashboardMonth = "6"
        });

        DashboardDto summary = await sut.GetSummaryAsync(email, "1999", "1", "UTC", TestContext.Current.CancellationToken);

        Assert.Equal(2025, summary.SelectedYear);
        Assert.Equal(6, summary.SelectedMonth);
    }

    /// <summary>Verifies that a non-demo account is unaffected by a configured demo account override.</summary>
    [Fact]
    public async Task GetSummaryAsync_DoesNotOverrideYearAndMonth_ForNonDemoAccount()
    {
        const string email = "user@example.com";
        var account = MakeAccount(unit: null);
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _incomeRepo.Setup(r => r.GetFirstByAccountEmailOrderedByCreatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Income?)null);
        _expenditureRepo.Setup(r => r.GetFirstByAccountEmailOrderedByCreatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Expenditure?)null);
        var sut = CreateSut(new ServerSetting
        {
            DemoAccountEmail = "demo@maroik.com",
            DemoDashboardYear = "2025",
            DemoDashboardMonth = "6"
        });

        DashboardDto summary = await sut.GetSummaryAsync(email, "2020", "4", "UTC", TestContext.Current.CancellationToken);

        Assert.Equal(2020, summary.SelectedYear);
        Assert.Equal(4, summary.SelectedMonth);
    }

    /// <summary>Verifies that <c>GetSummaryAsync</c> uses current year and month when invalid input given.</summary>
    [Fact]
    public async Task GetSummaryAsync_UsesCurrentYearAndMonth_WhenInvalidInputGiven()
    {
        const string email = "user@example.com";
        var account = MakeAccount(unit: null);
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _incomeRepo.Setup(r => r.GetFirstByAccountEmailOrderedByCreatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Income?)null);
        _expenditureRepo.Setup(r => r.GetFirstByAccountEmailOrderedByCreatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Expenditure?)null);
        var sut = CreateSut();

        DashboardDto summary = await sut.GetSummaryAsync(email, "bad", "bad", "UTC", TestContext.Current.CancellationToken);

        Assert.Equal(DateTime.UtcNow.Year, summary.SelectedYear);
        Assert.Equal(DateTime.UtcNow.Month, summary.SelectedMonth);
    }

    /// <summary>
    /// Regression test: month=13 parses successfully as an int but is out of DateTime's valid
    /// range, so <c>new DateTime(yearInt, monthInt, 1)</c> would throw. An asset must be present
    /// (giving a non-null DefaultMonetaryUnit) so the code path that builds that DateTime actually
    /// runs. Verifies the out-of-range month is caught by falling back to the current month
    /// rather than crashing the summary endpoint.
    /// </summary>
    [Fact]
    public async Task GetSummaryAsync_FallsBackToCurrentMonth_WhenMonthIsOutOfRange()
    {
        const string email = "user@example.com";
        var account = MakeAccount(unit: "KRW");
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([MakeAsset("Bank")]);
        SetupEmptyFinancials();
        var sut = CreateSut();

        DashboardDto summary = await sut.GetSummaryAsync(email, "2025", "13", "UTC", TestContext.Current.CancellationToken);

        Assert.Equal(2025, summary.SelectedYear);
        Assert.Equal(DateTime.UtcNow.Month, summary.SelectedMonth);
    }

    /// <summary>Verifies that <c>GetSummaryAsync</c> sets default monetary unit when account has none but assets exist.</summary>
    [Fact]
    public async Task GetSummaryAsync_SetsDefaultMonetaryUnit_WhenAccountHasNoneButAssetsExist()
    {
        const string email = "user@example.com";
        var account = MakeAccount(unit: null);
        var assets = new List<Asset>
        {
            MakeAsset("Wallet"),
            MakeAsset("Checking"),
            MakeAsset("Savings", "USD")
        };

        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(assets);
        SetupEmptyFinancials();
        var sut = CreateSut();

        DashboardDto summary = await sut.GetSummaryAsync(email, "2025", "1", "UTC", TestContext.Current.CancellationToken);

        // Most-used unit is KRW (2 assets), and the self-correction is persisted back to the
        // account (matches main) so other screens reading Account.DefaultMonetaryUnit directly
        // stay in sync with what the dashboard displays.
        Assert.Equal("KRW", summary.DefaultMonetaryUnit);
        _accountRepo.Verify(r => r.UpdateDefaultMonetaryUnitAsync(email, "KRW", It.IsAny<CancellationToken>()), Times.Once);
        _accountRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that <c>GetSummaryAsync</c> clears default monetary unit when no assets exist.</summary>
    [Fact]
    public async Task GetSummaryAsync_ClearsDefaultMonetaryUnit_WhenNoAssetsExist()
    {
        const string email = "user@example.com";
        var account = MakeAccount(unit: "KRW");
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _incomeRepo.Setup(r => r.GetFirstByAccountEmailOrderedByCreatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Income?)null);
        _expenditureRepo.Setup(r => r.GetFirstByAccountEmailOrderedByCreatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Expenditure?)null);
        var sut = CreateSut();

        DashboardDto summary = await sut.GetSummaryAsync(email, "2025", "1", "UTC", TestContext.Current.CancellationToken);

        Assert.Null(summary.DefaultMonetaryUnit);
        _accountRepo.Verify(r => r.UpdateDefaultMonetaryUnitAsync(email, null, It.IsAny<CancellationToken>()), Times.Once);
        _accountRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that a soft-deleted asset is excluded from the dashboard's visible asset card
    /// list, but — matching main's <c>AssetRepository.GetAssetsAsync</c>, which never filters
    /// Deleted — still counts toward the MonetaryUnit options and effective-currency calculation.
    /// </summary>
    [Fact]
    public async Task GetSummaryAsync_ExcludesDeletedAssets_FromVisibleAssetsListOnly()
    {
        const string email = "user@example.com";
        var account = MakeAccount(unit: null);
        var assets = new List<Asset>
        {
            MakeAsset("Wallet"),
            MakeAsset("OldForeignAccount", "USD", deleted: true)
        };

        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(assets);
        SetupEmptyFinancials();
        var sut = CreateSut();

        DashboardDto summary = await sut.GetSummaryAsync(email, "2025", "1", "UTC", TestContext.Current.CancellationToken);

        // The visible asset card list excludes deleted assets (main has no equivalent list).
        Assert.Single(summary.Assets);

        // main's AssetRepository.GetAssetsAsync never filters Deleted, so the MonetaryUnit
        // options and effective-currency resolution must still see the deleted asset's currency —
        // otherwise an account whose only assets are deleted would resolve to no currency and its
        // whole income/expenditure section would silently disappear from the dashboard.
        Assert.Contains("USD", summary.MonetaryUnits);
        Assert.Equal("KRW", summary.DefaultMonetaryUnit);

        // Rows tied to the deleted asset still need its currency, so the lookup covers every asset.
        Assert.Equal("USD", summary.CurrencyByProduct["OldForeignAccount"]);
        Assert.Equal("KRW", summary.CurrencyByProduct["Wallet"]);
    }

    /// <summary>
    /// Verifies that when an account's only asset has been soft-deleted, the dashboard still
    /// resolves a DefaultMonetaryUnit (matching main) instead of going empty, since the currency
    /// resolution must consider deleted assets the same way main's unfiltered asset list does.
    /// </summary>
    [Fact]
    public async Task GetSummaryAsync_ResolvesDefaultMonetaryUnit_WhenOnlyAssetIsDeleted()
    {
        const string email = "user@example.com";
        var account = MakeAccount(unit: "KRW");
        var assets = new List<Asset> { MakeAsset("OldWallet", deleted: true) };

        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(assets);
        SetupEmptyFinancials();
        var sut = CreateSut();

        DashboardDto summary = await sut.GetSummaryAsync(email, "2025", "1", "UTC", TestContext.Current.CancellationToken);

        Assert.Empty(summary.Assets);
        Assert.Equal("KRW", summary.DefaultMonetaryUnit);
    }

    /// <summary>
    /// The demo account is browsed anonymously and its data is pre-seeded and fixed, so
    /// <c>GetSummaryAsync</c> must never issue the DefaultMonetaryUnit self-correction write for it
    /// (that would fire on ordinary anonymous page views).
    /// </summary>
    [Fact]
    public async Task GetSummaryAsync_DoesNotPersistSelfCorrection_ForDemoAccount()
    {
        const string email = "demo@maroik.com";
        var account = MakeAccount(email, unit: null);
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeAsset("Wallet")]);
        SetupEmptyFinancials();
        var sut = CreateSut(new ServerSetting
        {
            DemoAccountEmail = email,
            DemoDashboardYear = "2025",
            DemoDashboardMonth = "6"
        });

        DashboardDto summary = await sut.GetSummaryAsync(email, "2025", "6", "UTC", TestContext.Current.CancellationToken);

        // The value still resolves for display...
        Assert.Equal("KRW", summary.DefaultMonetaryUnit);
        // ...but is not written back (the service self-corrects through the column-scoped update).
        _accountRepo.Verify(r => r.UpdateDefaultMonetaryUnitAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _accountRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that the year-month totals are correctly derived by filtering the already-fetched
    /// year-range results in memory, and that only one date-range query per repository is issued —
    /// regression test for a fix that dropped two of four sequential DB round trips.
    /// </summary>
    [Fact]
    public async Task GetSummaryAsync_DerivesYearMonthTotals_FromSingleYearRangeQueryPerRepository()
    {
        const string email = "user@example.com";
        var account = MakeAccount(unit: "KRW");
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([MakeAsset("Wallet")]);

        var inMonth = Income.Reconstitute(1, email, "RegularIncome", "LaborIncome", null, 100m, "KRW", "Wallet", null,
            new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);
        var outOfMonth = Income.Reconstitute(2, email, "RegularIncome", "LaborIncome", null, 200m, "KRW", "Wallet", null,
            new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);

        _incomeRepo.Setup(r => r.GetByAccountEmailAndDateRangeAsync(email, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([inMonth, outOfMonth]);
        _expenditureRepo.Setup(r => r.GetByAccountEmailAndDateRangeAsync(email, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _incomeRepo.Setup(r => r.GetFirstByAccountEmailOrderedByCreatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Income?)null);
        _expenditureRepo.Setup(r => r.GetFirstByAccountEmailOrderedByCreatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Expenditure?)null);
        var sut = CreateSut();

        DashboardDto summary = await sut.GetSummaryAsync(email, "2025", "3", "UTC", TestContext.Current.CancellationToken);

        Assert.Equal(2, summary.YearIncomes.Count);
        Assert.Single(summary.YearMonthIncomes);
        _incomeRepo.Verify(r => r.GetByAccountEmailAndDateRangeAsync(email, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _expenditureRepo.Verify(r => r.GetByAccountEmailAndDateRangeAsync(email, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- GetSummaryAsync: income/expenditure breakdown -------------------------

    /// <summary>
    /// Verifies that <c>GetSummaryAsync</c> computes the per-main-class/subclass income breakdown
    /// (via <see cref="FinanceBreakdownPolicy"/>) itself, rather than leaving that arithmetic to the
    /// presentation layer -- <c>DashboardViewModelMapper</c> now only copies these pre-computed
    /// numbers onto the view model.
    /// </summary>
    [Fact]
    public async Task GetSummaryAsync_ComputesIncomeBreakdown_PerMainClassAndSubClass()
    {
        const string email = "user@example.com";
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeAccount(unit: "KRW"));
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([MakeAsset("Wallet")]);
        SetupEmptyFinancials();

        List<Income> yearIncomes =
        [
            Income.Reconstitute(1, email, "RegularIncome", "LaborIncome", null, 600m, "KRW", "Wallet", null, DateTime.UtcNow, DateTime.UtcNow),
            Income.Reconstitute(2, email, "RegularIncome", "BusinessIncome", null, 400m, "KRW", "Wallet", null, DateTime.UtcNow, DateTime.UtcNow),
            Income.Reconstitute(3, email, "IrregularIncome", "LaborIncome", null, 250m, "KRW", "Wallet", null, DateTime.UtcNow, DateTime.UtcNow),
        ];
        _incomeRepo.Setup(r => r.GetByAccountEmailAndDateRangeAsync(email, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(yearIncomes);
        var sut = CreateSut();

        DashboardDto summary = await sut.GetSummaryAsync(email, "2025", "1", "UTC", TestContext.Current.CancellationToken);

        FinanceBreakdownDto regular = summary.YearIncomeBreakdown["RegularIncome"];
        Assert.Equal(1000m, regular.Total);
        Assert.Equal(600m, regular.AmountBySubClass["LaborIncome"]);
        Assert.Equal(400m, regular.AmountBySubClass["BusinessIncome"]);
        Assert.Equal(60.0, regular.PercentageBySubClass["LaborIncome"]);
        Assert.Equal(40.0, regular.PercentageBySubClass["BusinessIncome"]);

        FinanceBreakdownDto irregular = summary.YearIncomeBreakdown["IrregularIncome"];
        Assert.Equal(250m, irregular.Total);
        Assert.Equal(250m, irregular.AmountBySubClass["LaborIncome"]);
        Assert.Equal(100.0, irregular.PercentageBySubClass["LaborIncome"]);
    }

    /// <summary>
    /// Verifies that <c>GetSummaryAsync</c> computes the per-main-class/subclass expenditure
    /// breakdown itself, covering all three expenditure main classes.
    /// </summary>
    [Fact]
    public async Task GetSummaryAsync_ComputesExpenditureBreakdown_PerMainClassAndSubClass()
    {
        const string email = "user@example.com";
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeAccount(unit: "KRW"));
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([MakeAsset("Card")]);
        SetupEmptyFinancials();

        List<Expenditure> yearExpenditures =
        [
            Expenditure.Reconstitute(1, email, "ConsumerSpending", "MealOrEatOutExpenses", null, 300m, "KRW", "Card", null, null, DateTime.UtcNow, DateTime.UtcNow),
            Expenditure.Reconstitute(2, email, "ConsumerSpending", "HousingOrSuppliesCost", null, 700m, "KRW", "Card", null, null, DateTime.UtcNow, DateTime.UtcNow),
            Expenditure.Reconstitute(3, email, "RegularSavings", "Deposit", null, 500m, "KRW", "Card", null, null, DateTime.UtcNow, DateTime.UtcNow),
        ];
        _expenditureRepo.Setup(r => r.GetByAccountEmailAndDateRangeAsync(email, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(yearExpenditures);
        var sut = CreateSut();

        DashboardDto summary = await sut.GetSummaryAsync(email, "2025", "1", "UTC", TestContext.Current.CancellationToken);

        FinanceBreakdownDto consumer = summary.YearExpenditureBreakdown["ConsumerSpending"];
        Assert.Equal(1000m, consumer.Total);
        Assert.Equal(30.0, consumer.PercentageBySubClass["MealOrEatOutExpenses"]);
        Assert.Equal(70.0, consumer.PercentageBySubClass["HousingOrSuppliesCost"]);

        FinanceBreakdownDto savings = summary.YearExpenditureBreakdown["RegularSavings"];
        Assert.Equal(500m, savings.Total);
        Assert.Equal(100.0, savings.PercentageBySubClass["Deposit"]);

        // NonConsumerSpending had no records this year -- still present, with a zero total and no
        // percentage entries (matches FinanceBreakdownPolicy's zero-total behavior).
        FinanceBreakdownDto nonConsumer = summary.YearExpenditureBreakdown["NonConsumerSpending"];
        Assert.Equal(0m, nonConsumer.Total);
        Assert.Empty(nonConsumer.PercentageBySubClass);
    }

    /// <summary>
    /// Regression: <c>BuildBreakdown</c> now groups items by main class via a single
    /// <see cref="Enumerable.ToLookup{TSource,TKey}(IEnumerable{TSource},Func{TSource,TKey})"/> pass
    /// instead of re-filtering the full list once per known main class. An item whose main class is
    /// not one of <c>IncomeClassPolicy.SubClassesByMainClass</c>'s keys must still be silently
    /// excluded from every bucket -- neither crashing the lookup nor leaking into an unrelated class.
    /// </summary>
    [Fact]
    public async Task GetSummaryAsync_IncomeWithUnrecognisedMainClass_IsExcludedFromEveryBucket()
    {
        const string email = "user@example.com";
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeAccount(unit: "KRW"));
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([MakeAsset("Wallet")]);
        SetupEmptyFinancials();

        List<Income> yearIncomes =
        [
            Income.Reconstitute(1, email, "RegularIncome", "LaborIncome", null, 600m, "KRW", "Wallet", null, DateTime.UtcNow, DateTime.UtcNow),
            Income.Reconstitute(2, email, "NotARealMainClass", "Whatever", null, 999m, "KRW", "Wallet", null, DateTime.UtcNow, DateTime.UtcNow),
        ];
        _incomeRepo.Setup(r => r.GetByAccountEmailAndDateRangeAsync(email, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(yearIncomes);
        var sut = CreateSut();

        DashboardDto summary = await sut.GetSummaryAsync(email, "2025", "1", "UTC", TestContext.Current.CancellationToken);

        Assert.DoesNotContain("NotARealMainClass", summary.YearIncomeBreakdown.Keys);
        FinanceBreakdownDto regular = summary.YearIncomeBreakdown["RegularIncome"];
        Assert.Equal(600m, regular.Total);
        Assert.Equal(100.0, regular.PercentageBySubClass["LaborIncome"]);
    }

    /// <summary>
    /// Verifies that the year and year-month breakdowns are computed independently of their
    /// respective (already year-month-filtered) lists, not from each other.
    /// </summary>
    [Fact]
    public async Task GetSummaryAsync_ComputesYearAndYearMonthBreakdowns_Independently()
    {
        const string email = "user@example.com";
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeAccount(unit: "KRW"));
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([MakeAsset("Wallet")]);
        SetupEmptyFinancials();

        var inMonth = Income.Reconstitute(1, email, "RegularIncome", "LaborIncome", null, 400m, "KRW", "Wallet", null,
            new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);
        var outOfMonth = Income.Reconstitute(2, email, "RegularIncome", "LaborIncome", null, 600m, "KRW", "Wallet", null,
            new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);
        _incomeRepo.Setup(r => r.GetByAccountEmailAndDateRangeAsync(email, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([inMonth, outOfMonth]);
        var sut = CreateSut();

        DashboardDto summary = await sut.GetSummaryAsync(email, "2025", "3", "UTC", TestContext.Current.CancellationToken);

        Assert.Equal(1000m, summary.YearIncomeBreakdown["RegularIncome"].Total);
        Assert.Equal(400m, summary.YearMonthIncomeBreakdown["RegularIncome"].Total);
        Assert.Equal(100.0, summary.YearMonthIncomeBreakdown["RegularIncome"].PercentageBySubClass["LaborIncome"]);
    }

    // -- GetNoticeCountsAsync -------------------------------------------------

    /// <summary>Verifies that <c>GetNoticeCountsAsync</c> returns zero counts when no fixed items.</summary>
    [Fact]
    public async Task GetNoticeCountsAsync_ReturnsZeroCounts_WhenNoFixedItems()
    {
        const string email = "user@example.com";
        _fixedIncomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _fixedExpenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();

        NotificationDto counts = await sut.GetNoticeCountsAsync(email, 7, "UTC", TestContext.Current.CancellationToken);

        Assert.Equal(0, counts.FixedIncomesNoticed);
        Assert.Equal(0, counts.FixedExpendituresNoticed);
        Assert.Equal(0, counts.FixedIncomesExpired);
        Assert.Equal(0, counts.FixedExpendituresExpired);
    }

    /// <summary>Verifies that <c>GetNoticeCountsAsync</c> counts expired items when maturity date is past.</summary>
    [Fact]
    public async Task GetNoticeCountsAsync_CountsExpiredItems_WhenMaturityDateIsPast()
    {
        const string email = "user@example.com";
        var pastDate = DateTime.UtcNow.Date.AddDays(-10);

        _fixedIncomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            FixedIncome.Reconstitute(
                id: 1,
                accountEmail: email,
                mainClass: "RegularIncome",
                subClass: "LaborIncome",
                content: null,
                amount: 100m,
                monetaryUnit: "KRW",
                depositMyAssetProductName: "Wallet",
                depositMonth: 1,
                depositDay: 1,
                maturityDate: pastDate,
                note: null,
                unpunctuality: false,
                created: DateTime.UtcNow,
                updated: DateTime.UtcNow)
        ]);
        _fixedExpenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            FixedExpenditure.Reconstitute(
                id: 1,
                accountEmail: email,
                mainClass: "RegularExpenditure",
                subClass: "LivingExpenditure",
                content: null,
                amount: 100m,
                monetaryUnit: "KRW",
                paymentMethod: "Wallet",
                myDepositAsset: null,
                depositMonth: 1,
                depositDay: 1,
                maturityDate: pastDate,
                note: null,
                unpunctuality: false,
                created: DateTime.UtcNow,
                updated: DateTime.UtcNow)
        ]);
        var sut = CreateSut();

        NotificationDto counts = await sut.GetNoticeCountsAsync(email, 7, "UTC", TestContext.Current.CancellationToken);

        Assert.Equal(1, counts.FixedIncomesExpired);
        Assert.Equal(1, counts.FixedExpendituresExpired);
    }

    /// <summary>Verifies that <c>GetNoticeCountsAsync</c> counts noticed when unpunctuality is true.</summary>
    [Fact]
    public async Task GetNoticeCountsAsync_CountsNoticed_WhenUnpunctualityIsTrue()
    {
        const string email = "user@example.com";
        var futureDate = DateTime.UtcNow.Date.AddYears(1);

        _fixedIncomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            FixedIncome.Reconstitute(
                id: 1,
                accountEmail: email,
                mainClass: "RegularIncome",
                subClass: "LaborIncome",
                content: null,
                amount: 100m,
                monetaryUnit: "KRW",
                depositMyAssetProductName: "Wallet",
                depositMonth: 1,
                depositDay: 1,
                maturityDate: futureDate,
                note: null,
                unpunctuality: true,
                created: DateTime.UtcNow,
                updated: DateTime.UtcNow)
        ]);
        _fixedExpenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();

        NotificationDto counts = await sut.GetNoticeCountsAsync(email, 7, "UTC", TestContext.Current.CancellationToken);

        Assert.Equal(1, counts.FixedIncomesNoticed);
    }

    // -- GetServerResourceSummary ---------------------------------------------

    /// <summary>Verifies that <c>GetServerResourceSummary</c> returns empty when files do not exist.</summary>
    [Fact]
    public void GetServerResourceSummary_ReturnsEmpty_WhenFilesDoNotExist()
    {
        var sut = CreateSut();

        ServerResourceDto summary = sut.GetServerResourceSummary(
            "nonexistent_host.txt",
            "nonexistent_docker.txt");

        Assert.Empty(summary.HostCpuInfo);
        Assert.Empty(summary.HostMemoryInfo);
        Assert.Empty(summary.HostDiskInfo);
        Assert.Equal(0.0, summary.HostCpuNumeric);
    }

    /// <summary>Verifies that <c>GetServerResourceSummary</c> parses host cpu numeric when from host file.</summary>
    [Fact]
    public void GetServerResourceSummary_ParsesHostCpuNumeric_FromHostFile()
    {
        string hostFile = Path.GetTempFileName();
        string dockerFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(hostFile,
                "Host CPU Information: 45.5%\nHost Memory Information: Memory: 4GB/8GB\nHost Disk Information: Disk: 100GB/500GB");
            File.WriteAllText(dockerFile, "Docker Resource Usage:\napp 12.3% 100MiB / 512MiB");

            var sut = CreateSut();
            ServerResourceDto summary = sut.GetServerResourceSummary(hostFile, dockerFile);

            Assert.Equal(45.5, summary.HostCpuNumeric, precision: 1);
        }
        finally
        {
            File.Delete(hostFile);
            File.Delete(dockerFile);
        }
    }

    /// <summary>Verifies that <c>GetServerResourceSummary</c> parses each docker container row into a typed <see cref="DockerContainerResourceDto"/>.</summary>
    [Fact]
    public void GetServerResourceSummary_ParsesDockerContainers_IntoTypedDtos()
    {
        string hostFile = Path.GetTempFileName();
        string dockerFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(hostFile, "Host CPU Information: 1%\nHost Memory Information: Memory: 1GB/2GB\nHost Disk Information: Disk: 1GB/2GB");
            File.WriteAllText(dockerFile,
                "Docker Resource Usage:\n" +
                "NAME CPU% MEM USED / LIMIT\n" +
                "web 12.3% 100MiB / 512MiB\n" +
                "db 4.5% 1GiB / 2GiB");

            var sut = CreateSut();
            ServerResourceDto summary = sut.GetServerResourceSummary(hostFile, dockerFile);

            Assert.Equal(2, summary.DockerContainerResult.Count);
            Assert.Equal("web", summary.DockerContainerResult[0].Name);
            Assert.Equal("12.3%", summary.DockerContainerResult[0].CpuPercent);
            Assert.Equal("100MiB / 512MiB", summary.DockerContainerResult[0].MemUsageDisplay);
            Assert.Equal("db", summary.DockerContainerResult[1].Name);
        }
        finally
        {
            File.Delete(hostFile);
            File.Delete(dockerFile);
        }
    }

    /// <summary>Verifies that <c>GetServerResourceSummary</c> returns an empty (not null) docker container list when the docker file is missing.</summary>
    [Fact]
    public void GetServerResourceSummary_ReturnsEmptyDockerContainerList_WhenDockerFileMissing()
    {
        var sut = CreateSut();

        ServerResourceDto summary = sut.GetServerResourceSummary("nonexistent_host.txt", "nonexistent_docker.txt");

        Assert.Empty(summary.DockerContainerResult);
    }

    /// <summary>
    /// Regression test: when the docker resource file's content doesn't contain the
    /// "Docker Resource Usage:" marker at all (e.g. the collection script's output format
    /// changed, or it just wrote an error message), <c>ExtractSection</c> must return an empty
    /// section instead of a garbage substring taken from near the start of the unrelated content —
    /// a missing start marker previously still produced a non-empty slice when no end marker was
    /// given, because the "not found" (-1) IndexOf result was silently added to the marker length
    /// instead of being treated as "not found".
    /// </summary>
    [Fact]
    public void GetServerResourceSummary_ReturnsEmptyDockerContainerList_WhenStartMarkerMissingFromDockerFile()
    {
        string hostFile = Path.GetTempFileName();
        string dockerFile = Path.GetTempFileName();
        try
        {
            // No "Docker Resource Usage:" marker anywhere in this content. The leading junk line is
            // longer than the marker text so that, under the pre-fix bug, the wrongly-computed start
            // index still lands inside it — leaving the real header/data lines below intact in the
            // (incorrectly non-empty) extracted section, which would otherwise get parsed into fake
            // container rows despite the marker never having been found.
            File.WriteAllText(dockerFile,
                new string('x', 50) + "\n" +
                "NAME CPU% MEM USED / LIMIT\n" +
                "web 12.3% 100MiB / 512MiB\n" +
                "db 4.5% 1GiB / 2GiB");

            var sut = CreateSut();
            ServerResourceDto summary = sut.GetServerResourceSummary(hostFile, dockerFile);

            Assert.Empty(summary.DockerContainerResult);
        }
        finally
        {
            File.Delete(hostFile);
            File.Delete(dockerFile);
        }
    }

    // -- GetSummaryAsync: currency filter and start year ------------------------------

    private void GivenSummaryInputs(IEnumerable<Asset> assets, List<Income> incomes, List<Expenditure> expenditures,
        Income? firstIncome = null, Expenditure? firstExpenditure = null, string? defaultUnit = "KRW")
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeAccount(unit: defaultUnit));
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([.. assets]);
        _incomeRepo.Setup(r => r.GetByAccountEmailAndDateRangeAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(incomes);
        _expenditureRepo.Setup(r => r.GetByAccountEmailAndDateRangeAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(expenditures);
        _incomeRepo.Setup(r => r.GetFirstByAccountEmailOrderedByCreatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(firstIncome);
        _expenditureRepo.Setup(r => r.GetFirstByAccountEmailOrderedByCreatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(firstExpenditure);
    }

    private static Income IncomeAt(long id, string asset, decimal amount, DateTime created) =>
        Income.Reconstitute(id, "user@example.com", "RegularIncome", "LaborIncome", null, amount, "KRW", asset, null, created, created);

    private static Expenditure ExpenditureAt(long id, string paymentMethod, decimal amount, DateTime created) =>
        Expenditure.Reconstitute(id, "user@example.com", "ConsumerSpending", "MealOrEatOutExpenses", null, amount, "KRW", paymentMethod, "", null, created, created);

    /// <summary>
    /// Only rows whose asset uses the account's default currency are totaled (mixing currencies would
    /// add unlike amounts), but the product→currency lookup still lists every asset for the view.
    /// </summary>
    [Fact]
    public async Task GetSummaryAsync_TotalsOnlyRowsInTheDefaultCurrency()
    {
        var march = new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        GivenSummaryInputs(
            [MakeAsset("Wallet"), MakeAsset("UsdWallet", "USD")],
            [IncomeAt(1, "Wallet", 100m, march), IncomeAt(2, "UsdWallet", 999m, march)],
            [ExpenditureAt(3, "Wallet", 10m, march), ExpenditureAt(4, "UsdWallet", 888m, march)]);

        DashboardDto summary = await CreateSut().GetSummaryAsync("user@example.com", "2025", "3", "UTC", TestContext.Current.CancellationToken);

        Assert.Equal([1L], summary.YearIncomes.Select(i => i.Id));
        Assert.Equal([1L], summary.YearMonthIncomes.Select(i => i.Id));
        Assert.Equal([3L], summary.YearExpenditures.Select(e => e.Id));
        Assert.Equal([3L], summary.YearMonthExpenditures.Select(e => e.Id));
        Assert.Equal("USD", summary.CurrencyByProduct["UsdWallet"]);
    }

    /// <summary>A transaction on a since-deleted asset still resolves its currency and stays in the totals.</summary>
    [Fact]
    public async Task GetSummaryAsync_KeepsTransactionsOnDeletedAssets_InTheTotals()
    {
        var march = new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        GivenSummaryInputs([MakeAsset("OldWallet", deleted: true)], [IncomeAt(1, "OldWallet", 100m, march)], []);

        DashboardDto summary = await CreateSut().GetSummaryAsync("user@example.com", "2025", "3", "UTC", TestContext.Current.CancellationToken);

        Assert.Equal([1L], summary.YearIncomes.Select(i => i.Id));
    }

    /// <summary>The start year is the earlier of the first income and the first expenditure.</summary>
    [Fact]
    public async Task GetSummaryAsync_StartYear_IsTheEarlierOfTheFirstIncomeAndTheFirstExpenditure()
    {
        GivenSummaryInputs([MakeAsset("Wallet")], [], [],
            firstIncome: IncomeAt(1, "Wallet", 1m, new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            firstExpenditure: ExpenditureAt(2, "Wallet", 1m, new DateTime(2021, 2, 1, 0, 0, 0, DateTimeKind.Utc)));

        DashboardDto summary = await CreateSut().GetSummaryAsync("user@example.com", "2025", "3", "UTC", TestContext.Current.CancellationToken);

        Assert.Equal(2021, summary.StartYear);
    }

    /// <summary>The start year is read in the user's own time zone: a UTC instant just before New Year is already next year in Seoul.</summary>
    [Fact]
    public async Task GetSummaryAsync_StartYear_IsConvertedToTheUsersTimeZone()
    {
        GivenSummaryInputs([MakeAsset("Wallet")], [], [],
            firstIncome: IncomeAt(1, "Wallet", 1m, new DateTime(2024, 12, 31, 20, 0, 0, DateTimeKind.Utc)));

        DashboardDto utc = await CreateSut().GetSummaryAsync("user@example.com", "2025", "3", "UTC", TestContext.Current.CancellationToken);
        DashboardDto seoul = await CreateSut().GetSummaryAsync("user@example.com", "2025", "3", "Asia/Seoul", TestContext.Current.CancellationToken);

        Assert.Equal(2024, utc.StartYear);
        Assert.Equal(2025, seoul.StartYear);
    }

    /// <summary>An account with no transactions yet starts "this year".</summary>
    [Fact]
    public async Task GetSummaryAsync_StartYear_IsTheCurrentYear_WhenThereAreNoTransactions()
    {
        GivenSummaryInputs([MakeAsset("Wallet")], [], []);

        DashboardDto summary = await CreateSut().GetSummaryAsync("user@example.com", "2025", "3", "UTC", TestContext.Current.CancellationToken);

        Assert.Equal(DateTime.UtcNow.Year, summary.StartYear);
    }

    // -- GetNoticeCountsAsync: fixed expenditures ------------------------------------

    private static FixedExpenditure FixedExpenditureDue(DateTime maturity, bool unpunctual = false) =>
        FixedExpenditure.Reconstitute(1, "user@example.com", "ConsumerSpending", "MealOrEatOutExpenses", null, 100m, "KRW",
            "Wallet", null, 1, 1, maturity, null, unpunctual, DateTime.UtcNow, DateTime.UtcNow);

    /// <summary>Fixed expenditures are counted like fixed incomes: "always notify" items are noticed, past-maturity ones are expired.</summary>
    [Fact]
    public async Task GetNoticeCountsAsync_CountsNoticedAndExpiredFixedExpenditures()
    {
        _fixedIncomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _fixedExpenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            FixedExpenditureDue(DateTime.UtcNow.Date.AddYears(1), unpunctual: true),
            FixedExpenditureDue(DateTime.UtcNow.Date.AddYears(-1)),
        ]);

        NotificationDto counts = await CreateSut().GetNoticeCountsAsync("user@example.com", 7, "UTC", TestContext.Current.CancellationToken);

        Assert.Equal(1, counts.FixedExpendituresNoticed);
        Assert.Equal(1, counts.FixedExpendituresExpired);
        Assert.Equal(0, counts.FixedIncomesNoticed);
        Assert.Equal(0, counts.FixedIncomesExpired);
    }

    // -- GetServerResourceSummary: memory / disk rendering ----------------------------

    /// <summary>
    /// The collector writes <c>Memory: total / usage</c> and <c>Disk: total / usage</c>; the summary
    /// shows them as <c>usage / total</c>, and reads the CPU percentage out of the "CPU Usage: n%" line.
    /// </summary>
    [Fact]
    public void GetServerResourceSummary_RendersMemoryAndDiskAsUsageOverTotal()
    {
        string hostFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(hostFile,
                "Host CPU Information:\nCPU Usage: 37.5%\nHost Memory Information:\nMemory: 8.0Gi / 3.2Gi\nHost Disk Information:\nDisk: 500GB / 120GB\n");

            ServerResourceDto summary = CreateSut().GetServerResourceSummary(hostFile, "nonexistent_docker.txt");

            Assert.Equal("3.2Gi / 8.0Gi", summary.MemUsageLimit);
            Assert.Equal("120GB / 500GB", summary.DiskUsageLimit);
            Assert.Equal(37.5, summary.HostCpuNumeric, precision: 1);
        }
        finally
        {
            File.Delete(hostFile);
        }
    }

    /// <summary>A memory/disk line without the "total / usage" separator renders nothing instead of throwing.</summary>
    [Fact]
    public void GetServerResourceSummary_RendersNothing_WhenTheMemoryAndDiskLinesHaveNoSeparator()
    {
        string hostFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(hostFile, "Host CPU Information: 1%\nHost Memory Information:\nMemory: unavailable\nHost Disk Information:\nDisk: unknown\n");

            ServerResourceDto summary = CreateSut().GetServerResourceSummary(hostFile, "nonexistent_docker.txt");

            Assert.Equal("", summary.MemUsageLimit);
            Assert.Equal("", summary.DiskUsageLimit);
        }
        finally
        {
            File.Delete(hostFile);
        }
    }
}

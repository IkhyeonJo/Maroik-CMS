using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for the Income and Expenditure action groups of
/// <c>AccountBookController</c> (all <c>[RequiredHttpPostAccess(Role = Role.User)]</c>-gated).
/// Runs against a real PostgreSQL instance (via <see cref="MaroikWebApplicationFactory"/>'s
/// Testcontainers setup, schema loaded from the real production init script), so
/// <c>IncomeService</c>/<c>ExpenditureService</c>'s <c>IUnitOfWork</c> transactions are
/// exercised for real, including their Asset-balance side effects.
/// </summary>
[Collection("Website Integration")]
public class AccountBookControllerIncomeExpenditureTests(MaroikWebApplicationFactory factory)
{
    private const string Email = "accountbook-incexp-user@test.com";
    private const string AssetProductName = "some-asset";

    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);
    
    private Task<AuthenticatedSession> LoginAsync() =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, Email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

    private static void SeedAssetIfMissing(ApplicationDbContext db, string productName = AssetProductName)
    {
        if (db.Assets.Any(a => a.AccountEmail == Email && a.ProductName == productName)) return;
        db.Assets.Add(new Asset
        {
            ProductName = productName,
            AccountEmail = Email,
            Item = "CashAsset",
            Amount = 1000,
            MonetaryUnit = "KRW",
            Deleted = false,
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private decimal GetAssetBalance(string productName = AssetProductName)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return db.Assets.Single(a => a.AccountEmail == Email && a.ProductName == productName).Amount;
    }

    /// <summary>A unique product name per call, so balance-delta assertions in a single test
    /// can't be perturbed by other tests in this class concurrently touching the shared asset.</summary>
    private static string UniqueAssetName([System.Runtime.CompilerServices.CallerMemberName] string testName = "") =>
        $"{testName}-{Guid.NewGuid():N}";

    private long SeedIncome(decimal amount = 1000, string productName = AssetProductName)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        SeedAssetIfMissing(db, productName);
        var income = new Income
        {
            AccountEmail = Email,
            MainClass = "RegularIncome",
            SubClass = "LaborIncome",
            Content = "Salary",
            Amount = amount,
            DepositMyAssetProductName = productName,
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow
        };
        db.Incomes.Add(income);
        db.SaveChanges();
        return income.Id;
    }

    private long SeedExpenditure(decimal amount = 50, string productName = AssetProductName)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        SeedAssetIfMissing(db, productName);
        var expenditure = new Expenditure
        {
            AccountEmail = Email,
            MainClass = "ConsumerSpending",
            SubClass = "MealOrEatOutExpenses",
            Content = "Lunch",
            Amount = amount,
            PaymentMethod = productName,
            // Left empty (not equal to PaymentMethod): MyDepositAsset only has meaning for
            // "transfer"-classified expenditures (moving money between two assets). Setting it
            // to the same value as PaymentMethod — which the real CreateExpenditure flow never
            // does for a non-transfer category like this one — would make
            // AdjustAssetBalancesAsync deposit and withdraw the *same* asset on revert, net-zero.
            MyDepositAsset = null,
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow
        };
        db.Expenditures.Add(expenditure);
        db.SaveChanges();
        return expenditure.Id;
    }

    // -- CreateIncome: role gating + ModelState validation ------------------------

    /// <summary>Create income admin session is forbidden.</summary>
    [Fact]
    public async Task CreateIncome_AdminSession_IsForbidden()
    {
        var session = await AuthenticatedSessionHelper.LoginAsync(factory, _client, "accountbook-incexp-admin@test.com", "AdminPassword1!", Role.Admin, TestContext.Current.CancellationToken);
        using var request = session.BuildJsonPostRequest("/AccountBook/CreateIncome", new { Id = 0, MainClass = "RegularIncome", SubClass = "LaborIncome", Content = "x", Amount = 1, DepositMyAssetProductName = "a", Note = "" });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Create income missing main class fails model state cleanly.</summary>
    [Fact]
    public async Task CreateIncome_MissingMainClass_FailsModelStateCleanly()
    {
        var session = await LoginAsync();
        var payload = new { Id = 0, MainClass = (string?)null, SubClass = "LaborIncome", Content = "x", Amount = 1, DepositMyAssetProductName = "a", Note = "" };
        using var request = session.BuildJsonPostRequest("/AccountBook/CreateIncome", payload);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"result\":false", json);
    }

    // -- CreateIncome: success path — verifies the Asset-balance side effect too -----

    /// <summary>Create income valid payload deposits into asset balance.</summary>
    [Fact]
    public async Task CreateIncome_ValidPayload_DepositsIntoAssetBalance()
    {
        string asset = UniqueAssetName();
        var session = await LoginAsync();
        using (var scope = factory.Services.CreateScope())
            SeedAssetIfMissing(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), asset);
        decimal before = GetAssetBalance(asset);

        var payload = new
        {
            Id = 0, MainClass = "RegularIncome", SubClass = "LaborIncome", Content = "Bonus",
            Amount = 300, DepositMyAssetProductName = asset, Note = "",
            Created = "2026-01-15 10:30:00"
        };
        using var request = session.BuildJsonPostRequest("/AccountBook/CreateIncome", payload);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
        Assert.Equal(before + 300, GetAssetBalance(asset));
    }

    // -- UpdateIncome / DeleteIncome: success path ------------------------------------

    /// <summary>Update income existing record returns success result.</summary>
    [Fact]
    public async Task UpdateIncome_ExistingRecord_ReturnsSuccessResult()
    {
        var session = await LoginAsync();
        long id = SeedIncome();

        using var request = session.BuildJsonPostRequest("/AccountBook/UpdateIncome", new
        {
            Id = id, MainClass = "RegularIncome", SubClass = "BusinessIncome", Content = "Updated",
            Amount = 1200, DepositMyAssetProductName = AssetProductName, Note = "updated",
            Created = "2026-01-15 10:30:00"
        });
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var income = db.Incomes.Single(i => i.Id == id);
        Assert.Equal("BusinessIncome", income.SubClass);
        Assert.Equal("updated", income.Note);
    }

    /// <summary>Delete income existing record removes it and refunds asset balance.</summary>
    [Fact]
    public async Task DeleteIncome_ExistingRecord_RemovesItAndRefundsAssetBalance()
    {
        string asset = UniqueAssetName();
        var session = await LoginAsync();
        long id = SeedIncome(amount: 500, productName: asset);
        decimal balanceAfterDeposit = GetAssetBalance(asset);

        using var request = session.BuildJsonPostRequest("/AccountBook/DeleteIncome", new { Id = id });
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(db.Incomes.Any(i => i.Id == id));
        Assert.Equal(balanceAfterDeposit - 500, GetAssetBalance(asset));
    }

    // -- IsIncomeExists -------------------------------------------------------------

    /// <summary>Is income exists seeded record returns success result.</summary>
    [Fact]
    public async Task IsIncomeExists_SeededRecord_ReturnsSuccessResult()
    {
        var session = await LoginAsync();
        long id = SeedIncome();

        using var request = session.BuildJsonPostRequest($"/AccountBook/IsIncomeExists?id={id}");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
    }

    /// <summary>Is income exists unknown id returns failure result.</summary>
    [Fact]
    public async Task IsIncomeExists_UnknownId_ReturnsFailureResult()
    {
        var session = await LoginAsync();
        using var request = session.BuildJsonPostRequest("/AccountBook/IsIncomeExists?id=999999");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    /// <summary>Get income amount label unknown product returns base label.</summary>
    [Fact]
    public async Task GetIncomeAmountLabel_UnknownProduct_ReturnsBaseLabel()
    {
        var session = await LoginAsync();
        using var request = session.BuildJsonPostRequest("/AccountBook/GetIncomeAmountLabel?productName=no-such-asset");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
    }

    // -- CreateExpenditure: role gating + ModelState validation --------------------

    /// <summary>Create expenditure missing payment method fails model state cleanly.</summary>
    [Fact]
    public async Task CreateExpenditure_MissingPaymentMethod_FailsModelStateCleanly()
    {
        var session = await LoginAsync();
        var payload = new { Id = 0, MainClass = "ConsumerSpending", SubClass = "MealOrEatOutExpenses", Content = "x", Amount = 1, PaymentMethod = (string?)null, MyDepositAsset = "a", Note = "" };
        using var request = session.BuildJsonPostRequest("/AccountBook/CreateExpenditure", payload);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"result\":false", json);
    }

    // -- CreateExpenditure: success path — verifies the Asset-balance side effect ----

    /// <summary>Create expenditure valid payload withdraws from asset balance.</summary>
    [Fact]
    public async Task CreateExpenditure_ValidPayload_WithdrawsFromAssetBalance()
    {
        string asset = UniqueAssetName();
        var session = await LoginAsync();
        using (var scope = factory.Services.CreateScope())
            SeedAssetIfMissing(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), asset);
        decimal before = GetAssetBalance(asset);

        var payload = new
        {
            Id = 0, MainClass = "ConsumerSpending", SubClass = "MealOrEatOutExpenses", Content = "Dinner",
            Amount = 40, PaymentMethod = asset, MyDepositAsset = "N/A", Note = "",
            Created = "2026-01-15 10:30:00"
        };
        using var request = session.BuildJsonPostRequest("/AccountBook/CreateExpenditure", payload);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
        Assert.Equal(before - 40, GetAssetBalance(asset));
    }

    // -- UpdateExpenditure / DeleteExpenditure: success path --------------------------

    /// <summary>Update expenditure existing record returns success result.</summary>
    [Fact]
    public async Task UpdateExpenditure_ExistingRecord_ReturnsSuccessResult()
    {
        var session = await LoginAsync();
        long id = SeedExpenditure();

        using var request = session.BuildJsonPostRequest("/AccountBook/UpdateExpenditure", new
        {
            Id = id, MainClass = "ConsumerSpending", SubClass = "TransportationCost", Content = "Updated",
            Amount = 60, PaymentMethod = AssetProductName, MyDepositAsset = "N/A", Note = "updated",
            Created = "2026-01-15 10:30:00"
        });
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expenditure = db.Expenditures.Single(e => e.Id == id);
        Assert.Equal("TransportationCost", expenditure.SubClass);
        Assert.Equal("updated", expenditure.Note);
    }

    /// <summary>Delete expenditure existing record removes it and refunds asset balance.</summary>
    [Fact]
    public async Task DeleteExpenditure_ExistingRecord_RemovesItAndRefundsAssetBalance()
    {
        string asset = UniqueAssetName();
        var session = await LoginAsync();
        long id = SeedExpenditure(amount: 75, productName: asset);
        decimal balanceAfterWithdrawal = GetAssetBalance(asset);

        using var request = session.BuildJsonPostRequest("/AccountBook/DeleteExpenditure", new { Id = id });
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(db.Expenditures.Any(e => e.Id == id));
        Assert.Equal(balanceAfterWithdrawal + 75, GetAssetBalance(asset));
    }

    // -- IsExpenditureExists ----------------------------------------------------------

    /// <summary>Is expenditure exists seeded record returns success result.</summary>
    [Fact]
    public async Task IsExpenditureExists_SeededRecord_ReturnsSuccessResult()
    {
        var session = await LoginAsync();
        long id = SeedExpenditure();

        using var request = session.BuildJsonPostRequest($"/AccountBook/IsExpenditureExists?id={id}");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
    }

    /// <summary>Is expenditure exists unknown id returns failure result.</summary>
    [Fact]
    public async Task IsExpenditureExists_UnknownId_ReturnsFailureResult()
    {
        var session = await LoginAsync();
        using var request = session.BuildJsonPostRequest("/AccountBook/IsExpenditureExists?id=999999");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    /// <summary>Get expenditure amount label unknown product returns base label.</summary>
    [Fact]
    public async Task GetExpenditureAmountLabel_UnknownProduct_ReturnsBaseLabel()
    {
        var session = await LoginAsync();
        using var request = session.BuildJsonPostRequest("/AccountBook/GetExpenditureAmountLabel?productName=no-such-asset");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
    }
}

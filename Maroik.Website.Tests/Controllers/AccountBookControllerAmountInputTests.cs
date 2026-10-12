using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// The account-book forms (Asset / Income / Expenditure) show the amount with a comma between every
/// three integer digits as it is typed, and post that text ("1,234,567.5"). The controller parses it with
/// <c>FinanceAmountPolicy.TryParseInput</c>: well-formed text is stored as the number it shows, and any
/// other text is refused as "Input is invalid" without touching the database.
/// </summary>
[Collection("Website Integration")]
public class AccountBookControllerAmountInputTests(MaroikWebApplicationFactory factory)
{
    /// <summary>The account every test in this class signs in as.</summary>
    private const string Email = "accountbook-amount-user@test.com";

    /// <summary>Malformed amount texts, each refused by every create / update action.</summary>
    public static TheoryData<string> MalformedAmounts => ["1,23", "12,34,567", "1234,567", "1.2.3", "1,000.5,0", "abc", "+5", ""];

    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    /// <summary>Seeds (if missing) the User account <see cref="Email"/> and signs in as it.</summary>
    private Task<AuthenticatedSession> LoginAsync() =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, Email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

    /// <summary>A product name no other test uses.</summary>
    private static string UniqueName([System.Runtime.CompilerServices.CallerMemberName] string testName = "") =>
        $"{testName}-{Guid.NewGuid():N}";

    /// <summary>Inserts an asset of <see cref="Email"/> with a balance of 1000.</summary>
    private void SeedAsset(string productName)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Assets.Add(new Asset
        {
            ProductName = productName, AccountEmail = Email, Item = "CashAsset", Amount = 1000, MonetaryUnit = "KRW",
            Deleted = false, Created = DateTime.UtcNow, Updated = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    /// <summary>Runs <paramref name="query"/> against a fresh <see cref="ApplicationDbContext"/>.</summary>
    private T Query<T>(Func<ApplicationDbContext, T> query)
    {
        using var scope = factory.Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    /// <summary>Posts <paramref name="payload"/> as JSON to <paramref name="url"/> and returns the reply body.</summary>
    private async Task<string> PostAsync(string url, object payload)
    {
        var session = await LoginAsync();
        using var request = session.BuildJsonPostRequest(url, payload);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Asserts <paramref name="json"/> is the "Input is invalid" refusal.</summary>
    private static void AssertInputIsInvalid(string json)
    {
        Assert.Contains("\"result\":false", json);
        Assert.Contains("Input is invalid", json);
    }

    /// <summary>An asset payload with <paramref name="amount"/> as its posted amount text.</summary>
    private static object AssetPayload(string productName, string amount, string? originalProductName = null) => new
    {
        ProductName = productName, Item = "CashAsset", Amount = amount, MonetaryUnit = "KRW", Note = "", Deleted = false,
        OriginalProductName = originalProductName
    };

    /// <summary>An income payload depositing <paramref name="amount"/> into <paramref name="asset"/>.</summary>
    private static object IncomePayload(string asset, string amount, long id = 0) => new
    {
        Id = id, MainClass = "RegularIncome", SubClass = "LaborIncome", Content = "Salary", Amount = amount,
        DepositMyAssetProductName = asset, Note = "", Created = "2026-01-15 10:30:00"
    };

    /// <summary>An expenditure payload paying <paramref name="amount"/> from <paramref name="asset"/>.</summary>
    private static object ExpenditurePayload(string asset, string amount, long id = 0) => new
    {
        Id = id, MainClass = "ConsumerSpending", SubClass = "MealOrEatOutExpenses", Content = "Lunch", Amount = amount,
        PaymentMethod = asset, MyDepositAsset = "N/A", Note = "", Created = "2026-01-15 10:30:00"
    };

    // -- Asset ------------------------------------------------------------------------

    /// <summary>A comma-grouped balance is stored as the number it shows.</summary>
    [Fact]
    public async Task CreateAsset_WithACommaGroupedAmount_StoresTheNumber()
    {
        string name = UniqueName();

        string json = await PostAsync("/AccountBook/CreateAsset", AssetPayload(name, "1,234,567.5"));

        Assert.Contains("\"result\":true", json);
        Assert.Equal(1234567.5m, Query(db => db.Assets.Single(a => a.AccountEmail == Email && a.ProductName == name).Amount));
    }

    /// <summary>Malformed amount text is refused and no asset is created.</summary>
    [Theory]
    [MemberData(nameof(MalformedAmounts))]
    public async Task CreateAsset_WithAMalformedAmount_IsRefused_AndStoresNothing(string amount)
    {
        string name = UniqueName();

        string json = await PostAsync("/AccountBook/CreateAsset", AssetPayload(name, amount));

        AssertInputIsInvalid(json);
        Assert.False(Query(db => db.Assets.Any(a => a.AccountEmail == Email && a.ProductName == name)));
    }

    /// <summary>An asset balance may be negative; a comma-grouped negative balance is stored as typed.</summary>
    [Fact]
    public async Task UpdateAsset_WithACommaGroupedNegativeAmount_StoresTheNumber()
    {
        string name = UniqueName();
        SeedAsset(name);

        string json = await PostAsync("/AccountBook/UpdateAsset", AssetPayload(name, "-2,000", name));

        Assert.Contains("\"result\":true", json);
        Assert.Equal(-2000m, Query(db => db.Assets.Single(a => a.AccountEmail == Email && a.ProductName == name).Amount));
    }

    /// <summary>Malformed amount text is refused and the balance is left as it was.</summary>
    [Theory]
    [MemberData(nameof(MalformedAmounts))]
    public async Task UpdateAsset_WithAMalformedAmount_IsRefused_AndLeavesTheBalance(string amount)
    {
        string name = UniqueName();
        SeedAsset(name);

        string json = await PostAsync("/AccountBook/UpdateAsset", AssetPayload(name, amount, name));

        AssertInputIsInvalid(json);
        Assert.Equal(1000m, Query(db => db.Assets.Single(a => a.AccountEmail == Email && a.ProductName == name).Amount));
    }

    // -- Income -----------------------------------------------------------------------

    /// <summary>A comma-grouped income is stored and deposited as the number it shows.</summary>
    [Fact]
    public async Task CreateIncome_WithACommaGroupedAmount_StoresTheNumber()
    {
        string asset = UniqueName();
        SeedAsset(asset);

        string json = await PostAsync("/AccountBook/CreateIncome", IncomePayload(asset, "12,345.25"));

        Assert.Contains("\"result\":true", json);
        Assert.Equal(12345.25m, Query(db => db.Incomes.Single(i => i.AccountEmail == Email && i.DepositMyAssetProductName == asset).Amount));
        Assert.Equal(13345.25m, Query(db => db.Assets.Single(a => a.AccountEmail == Email && a.ProductName == asset).Amount));
    }

    /// <summary>Malformed amount text is refused: no income, the balance untouched.</summary>
    [Theory]
    [MemberData(nameof(MalformedAmounts))]
    public async Task CreateIncome_WithAMalformedAmount_IsRefused_AndStoresNothing(string amount)
    {
        string asset = UniqueName();
        SeedAsset(asset);

        string json = await PostAsync("/AccountBook/CreateIncome", IncomePayload(asset, amount));

        AssertInputIsInvalid(json);
        Assert.False(Query(db => db.Incomes.Any(i => i.AccountEmail == Email && i.DepositMyAssetProductName == asset)));
        Assert.Equal(1000m, Query(db => db.Assets.Single(a => a.AccountEmail == Email && a.ProductName == asset).Amount));
    }

    /// <summary>An income edit takes the comma-grouped amount too.</summary>
    [Fact]
    public async Task UpdateIncome_WithACommaGroupedAmount_StoresTheNumber()
    {
        string asset = UniqueName();
        SeedAsset(asset);
        Assert.Contains("\"result\":true", await PostAsync("/AccountBook/CreateIncome", IncomePayload(asset, "100")));
        long id = Query(db => db.Incomes.Single(i => i.AccountEmail == Email && i.DepositMyAssetProductName == asset).Id);

        string json = await PostAsync("/AccountBook/UpdateIncome", IncomePayload(asset, "1,500", id));

        Assert.Contains("\"result\":true", json);
        Assert.Equal(1500m, Query(db => db.Incomes.Single(i => i.Id == id).Amount));
    }

    /// <summary>A malformed amount on an income edit is refused and the income keeps its amount.</summary>
    [Theory]
    [MemberData(nameof(MalformedAmounts))]
    public async Task UpdateIncome_WithAMalformedAmount_IsRefused(string amount)
    {
        string asset = UniqueName();
        SeedAsset(asset);
        Assert.Contains("\"result\":true", await PostAsync("/AccountBook/CreateIncome", IncomePayload(asset, "100")));
        long id = Query(db => db.Incomes.Single(i => i.AccountEmail == Email && i.DepositMyAssetProductName == asset).Id);

        string json = await PostAsync("/AccountBook/UpdateIncome", IncomePayload(asset, amount, id));

        AssertInputIsInvalid(json);
        Assert.Equal(100m, Query(db => db.Incomes.Single(i => i.Id == id).Amount));
    }

    // -- Expenditure ------------------------------------------------------------------

    /// <summary>A comma-grouped expenditure is stored and withdrawn as the number it shows.</summary>
    [Fact]
    public async Task CreateExpenditure_WithACommaGroupedAmount_StoresTheNumber()
    {
        string asset = UniqueName();
        SeedAsset(asset);

        string json = await PostAsync("/AccountBook/CreateExpenditure", ExpenditurePayload(asset, "1,200.5"));

        Assert.Contains("\"result\":true", json);
        Assert.Equal(1200.5m, Query(db => db.Expenditures.Single(e => e.AccountEmail == Email && e.PaymentMethod == asset).Amount));
        Assert.Equal(-200.5m, Query(db => db.Assets.Single(a => a.AccountEmail == Email && a.ProductName == asset).Amount));
    }

    /// <summary>Malformed amount text is refused: no expenditure, the balance untouched.</summary>
    [Theory]
    [MemberData(nameof(MalformedAmounts))]
    public async Task CreateExpenditure_WithAMalformedAmount_IsRefused_AndStoresNothing(string amount)
    {
        string asset = UniqueName();
        SeedAsset(asset);

        string json = await PostAsync("/AccountBook/CreateExpenditure", ExpenditurePayload(asset, amount));

        AssertInputIsInvalid(json);
        Assert.False(Query(db => db.Expenditures.Any(e => e.AccountEmail == Email && e.PaymentMethod == asset)));
        Assert.Equal(1000m, Query(db => db.Assets.Single(a => a.AccountEmail == Email && a.ProductName == asset).Amount));
    }

    /// <summary>An expenditure edit takes the comma-grouped amount too.</summary>
    [Fact]
    public async Task UpdateExpenditure_WithACommaGroupedAmount_StoresTheNumber()
    {
        string asset = UniqueName();
        SeedAsset(asset);
        Assert.Contains("\"result\":true", await PostAsync("/AccountBook/CreateExpenditure", ExpenditurePayload(asset, "100")));
        long id = Query(db => db.Expenditures.Single(e => e.AccountEmail == Email && e.PaymentMethod == asset).Id);

        string json = await PostAsync("/AccountBook/UpdateExpenditure", ExpenditurePayload(asset, "2,500", id));

        Assert.Contains("\"result\":true", json);
        Assert.Equal(2500m, Query(db => db.Expenditures.Single(e => e.Id == id).Amount));
    }

    /// <summary>A malformed amount on an expenditure edit is refused and the expenditure keeps its amount.</summary>
    [Theory]
    [MemberData(nameof(MalformedAmounts))]
    public async Task UpdateExpenditure_WithAMalformedAmount_IsRefused(string amount)
    {
        string asset = UniqueName();
        SeedAsset(asset);
        Assert.Contains("\"result\":true", await PostAsync("/AccountBook/CreateExpenditure", ExpenditurePayload(asset, "100")));
        long id = Query(db => db.Expenditures.Single(e => e.AccountEmail == Email && e.PaymentMethod == asset).Id);

        string json = await PostAsync("/AccountBook/UpdateExpenditure", ExpenditurePayload(asset, amount, id));

        AssertInputIsInvalid(json);
        Assert.Equal(100m, Query(db => db.Expenditures.Single(e => e.Id == id).Amount));
    }
}

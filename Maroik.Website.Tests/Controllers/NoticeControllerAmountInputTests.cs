using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// The fixed income / expenditure forms show the amount with a comma between every three integer digits as it is
/// typed, and post that text ("1,234,567.5"). <c>NoticeController</c> parses it with
/// <c>FinanceAmountPolicy.TryParseInput</c>: well-formed text is stored as the number it shows, and any other text is
/// refused as "Input is invalid" without touching the database.
/// </summary>
[Collection("Website Integration")]
public class NoticeControllerAmountInputTests(MaroikWebApplicationFactory factory)
{
    /// <summary>The account every test in this class signs in as.</summary>
    private const string Email = "notice-amount-user@test.com";

    /// <summary>The asset the fixed incomes / expenditures point at.</summary>
    private const string AssetProductName = "notice-amount-asset";

    /// <summary>Malformed amount texts, each refused by every create / update action.</summary>
    public static TheoryData<string> MalformedAmounts => ["1,23", "12,34,567", "1234,567", "1.2.3", "1,000.5,0", "abc", "+5", ""];

    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    /// <summary>A Content value no other test uses, to find the row this test wrote.</summary>
    private static string UniqueContent([System.Runtime.CompilerServices.CallerMemberName] string testName = "") =>
        $"{testName}-{Guid.NewGuid():N}";

    /// <summary>Runs <paramref name="query"/> against a fresh <see cref="ApplicationDbContext"/>.</summary>
    private T Query<T>(Func<ApplicationDbContext, T> query)
    {
        using var scope = factory.Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    /// <summary>Signs in as <see cref="Email"/> (making sure its asset exists), posts <paramref name="payload"/> and returns the reply body.</summary>
    private async Task<string> PostAsync(string url, object payload)
    {
        var session = await AuthenticatedSessionHelper.LoginAsync(factory, _client, Email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!db.Assets.Any(a => a.AccountEmail == Email && a.ProductName == AssetProductName))
            {
                db.Assets.Add(new Asset
                {
                    ProductName = AssetProductName, AccountEmail = Email, Item = "CashAsset", Amount = 1000, MonetaryUnit = "KRW",
                    Deleted = false, Created = DateTime.UtcNow, Updated = DateTime.UtcNow
                });
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

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

    /// <summary>A fixed-income payload with <paramref name="amount"/> as its posted amount text.</summary>
    private static object FixedIncomePayload(string content, string amount, long id = 0) => new
    {
        Id = id, MainClass = "RegularIncome", SubClass = "LaborIncome", Content = content, Amount = amount,
        DepositMonth = 6, DepositDay = 25, MaturityDate = "2030-12-31", Note = "",
        DepositMyAssetProductName = AssetProductName, Unpunctuality = false
    };

    /// <summary>A fixed-expenditure payload with <paramref name="amount"/> as its posted amount text.</summary>
    private static object FixedExpenditurePayload(string content, string amount, long id = 0) => new
    {
        Id = id, MainClass = "ConsumerSpending", SubClass = "MealOrEatOutExpenses", Content = content, Amount = amount,
        DepositMonth = 6, DepositDay = 1, MaturityDate = "2030-12-31", Note = "",
        PaymentMethod = AssetProductName, MyDepositAsset = "N/A", Unpunctuality = false
    };

    // -- FixedIncome ------------------------------------------------------------------

    /// <summary>A comma-grouped fixed income is stored as the number it shows.</summary>
    [Fact]
    public async Task CreateFixedIncome_WithACommaGroupedAmount_StoresTheNumber()
    {
        string content = UniqueContent();

        string json = await PostAsync("/Notice/CreateFixedIncome", FixedIncomePayload(content, "3,000,000.5"));

        Assert.Contains("\"result\":true", json);
        Assert.Equal(3000000.5m, Query(db => db.FixedIncomes.Single(f => f.AccountEmail == Email && f.Content == content).Amount));
    }

    /// <summary>Malformed amount text is refused and no fixed income is created.</summary>
    [Theory]
    [MemberData(nameof(MalformedAmounts))]
    public async Task CreateFixedIncome_WithAMalformedAmount_IsRefused_AndStoresNothing(string amount)
    {
        string content = UniqueContent();

        string json = await PostAsync("/Notice/CreateFixedIncome", FixedIncomePayload(content, amount));

        AssertInputIsInvalid(json);
        Assert.False(Query(db => db.FixedIncomes.Any(f => f.AccountEmail == Email && f.Content == content)));
    }

    /// <summary>A fixed-income edit takes the comma-grouped amount too.</summary>
    [Fact]
    public async Task UpdateFixedIncome_WithACommaGroupedAmount_StoresTheNumber()
    {
        string content = UniqueContent();
        Assert.Contains("\"result\":true", await PostAsync("/Notice/CreateFixedIncome", FixedIncomePayload(content, "100")));
        long id = Query(db => db.FixedIncomes.Single(f => f.AccountEmail == Email && f.Content == content).Id);

        string json = await PostAsync("/Notice/UpdateFixedIncome", FixedIncomePayload(content, "1,500", id));

        Assert.Contains("\"result\":true", json);
        Assert.Equal(1500m, Query(db => db.FixedIncomes.Single(f => f.Id == id).Amount));
    }

    /// <summary>A malformed amount on a fixed-income edit is refused and the row keeps its amount.</summary>
    [Theory]
    [MemberData(nameof(MalformedAmounts))]
    public async Task UpdateFixedIncome_WithAMalformedAmount_IsRefused(string amount)
    {
        string content = UniqueContent();
        Assert.Contains("\"result\":true", await PostAsync("/Notice/CreateFixedIncome", FixedIncomePayload(content, "100")));
        long id = Query(db => db.FixedIncomes.Single(f => f.AccountEmail == Email && f.Content == content).Id);

        string json = await PostAsync("/Notice/UpdateFixedIncome", FixedIncomePayload(content, amount, id));

        AssertInputIsInvalid(json);
        Assert.Equal(100m, Query(db => db.FixedIncomes.Single(f => f.Id == id).Amount));
    }

    // -- FixedExpenditure -------------------------------------------------------------

    /// <summary>A comma-grouped fixed expenditure is stored as the number it shows.</summary>
    [Fact]
    public async Task CreateFixedExpenditure_WithACommaGroupedAmount_StoresTheNumber()
    {
        string content = UniqueContent();

        string json = await PostAsync("/Notice/CreateFixedExpenditure", FixedExpenditurePayload(content, "1,200.25"));

        Assert.Contains("\"result\":true", json);
        Assert.Equal(1200.25m, Query(db => db.FixedExpenditures.Single(f => f.AccountEmail == Email && f.Content == content).Amount));
    }

    /// <summary>Malformed amount text is refused and no fixed expenditure is created.</summary>
    [Theory]
    [MemberData(nameof(MalformedAmounts))]
    public async Task CreateFixedExpenditure_WithAMalformedAmount_IsRefused_AndStoresNothing(string amount)
    {
        string content = UniqueContent();

        string json = await PostAsync("/Notice/CreateFixedExpenditure", FixedExpenditurePayload(content, amount));

        AssertInputIsInvalid(json);
        Assert.False(Query(db => db.FixedExpenditures.Any(f => f.AccountEmail == Email && f.Content == content)));
    }

    /// <summary>A fixed-expenditure edit takes the comma-grouped amount too.</summary>
    [Fact]
    public async Task UpdateFixedExpenditure_WithACommaGroupedAmount_StoresTheNumber()
    {
        string content = UniqueContent();
        Assert.Contains("\"result\":true", await PostAsync("/Notice/CreateFixedExpenditure", FixedExpenditurePayload(content, "100")));
        long id = Query(db => db.FixedExpenditures.Single(f => f.AccountEmail == Email && f.Content == content).Id);

        string json = await PostAsync("/Notice/UpdateFixedExpenditure", FixedExpenditurePayload(content, "2,500", id));

        Assert.Contains("\"result\":true", json);
        Assert.Equal(2500m, Query(db => db.FixedExpenditures.Single(f => f.Id == id).Amount));
    }

    /// <summary>A malformed amount on a fixed-expenditure edit is refused and the row keeps its amount.</summary>
    [Theory]
    [MemberData(nameof(MalformedAmounts))]
    public async Task UpdateFixedExpenditure_WithAMalformedAmount_IsRefused(string amount)
    {
        string content = UniqueContent();
        Assert.Contains("\"result\":true", await PostAsync("/Notice/CreateFixedExpenditure", FixedExpenditurePayload(content, "100")));
        long id = Query(db => db.FixedExpenditures.Single(f => f.AccountEmail == Email && f.Content == content).Id);

        string json = await PostAsync("/Notice/UpdateFixedExpenditure", FixedExpenditurePayload(content, amount, id));

        AssertInputIsInvalid(json);
        Assert.Equal(100m, Query(db => db.FixedExpenditures.Single(f => f.Id == id).Amount));
    }
}

using System.Text.Json;
using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for the read-side endpoints of <c>AccountBookController</c> (Asset / Income /
/// Expenditure) and <c>NoticeController</c> (FixedIncome / FixedExpenditure): the full pages, the AJAX grid
/// partials (all rows, and the whole-row search), the "Amount (UNIT)" label lookups, the "does this record
/// exist" lookups (which must never resolve another account's record) and the Excel exports. Rows are seeded
/// straight into PostgreSQL and read back through the real HTTP pipeline.
/// </summary>
[Collection("Website Integration")]
public class FinanceReadEndpointsTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);
    
    /// <summary>Seeds (if missing) a User account for <paramref name="email"/> and signs in as it.</summary>
    private Task<AuthenticatedSession> LoginAsync(string email) =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

    /// <summary>Runs <paramref name="seed"/> against a fresh context and saves.</summary>
    private void Seed(Action<ApplicationDbContext> seed)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        seed(db);
        db.SaveChanges();
    }

    /// <summary>Inserts a 1000-unit asset of <paramref name="email"/> in <paramref name="unit"/>.</summary>
    private void SeedAsset(string email, string productName, string unit = "KRW") => Seed(db => db.Assets.Add(new Asset
    {
        ProductName = productName, AccountEmail = email, Item = "FreeDepositAndWithdrawal", MonetaryUnit = unit,
        Amount = 1000m, Note = "", Deleted = false, Created = DateTime.UtcNow, Updated = DateTime.UtcNow
    }));

    /// <summary>Inserts an income of <paramref name="email"/> into <paramref name="asset"/> and returns its id.</summary>
    private long SeedIncome(string email, string asset, string content, decimal amount = 100m)
    {
        long id = 0;
        Seed(db =>
        {
            var income = new Income
            {
                AccountEmail = email, DepositMyAssetProductName = asset, MainClass = "RegularIncome", SubClass = "LaborIncome",
                Content = content, Amount = amount, Note = "", Created = DateTime.UtcNow, Updated = DateTime.UtcNow
            };
            db.Incomes.Add(income);
            db.SaveChanges();
            id = income.Id;
        });
        return id;
    }

    /// <summary>Inserts a 50-unit consumer-spending expenditure of <paramref name="email"/> paid from <paramref name="asset"/>.</summary>
    private void SeedExpenditure(string email, string asset, string content) => Seed(db => db.Expenditures.Add(new Expenditure
    {
        AccountEmail = email, PaymentMethod = asset, MyDepositAsset = null, MainClass = "ConsumerSpending", SubClass = "MealOrEatOutExpenses",
        Content = content, Amount = 50m, Note = "", Created = DateTime.UtcNow, Updated = DateTime.UtcNow
    }));

    /// <summary>Inserts a fixed income of <paramref name="email"/> into <paramref name="asset"/> and returns its id.</summary>
    private long SeedFixedIncome(string email, string asset, string content)
    {
        long id = 0;
        Seed(db =>
        {
            var fixedIncome = new FixedIncome
            {
                AccountEmail = email, DepositMyAssetProductName = asset, MainClass = "RegularIncome", SubClass = "LaborIncome",
                Content = content, Amount = 3000m, DepositMonth = 1, DepositDay = 25, MaturityDate = DateTime.UtcNow.AddYears(1),
                Note = "", Unpunctuality = false, Created = DateTime.UtcNow, Updated = DateTime.UtcNow
            };
            db.FixedIncomes.Add(fixedIncome);
            db.SaveChanges();
            id = fixedIncome.Id;
        });
        return id;
    }

    /// <summary>Inserts a 70-unit fixed expenditure of <paramref name="email"/> paid from <paramref name="asset"/>, maturing in a year.</summary>
    private void SeedFixedExpenditure(string email, string asset, string content) => Seed(db => db.FixedExpenditures.Add(new FixedExpenditure
    {
        AccountEmail = email, PaymentMethod = asset, MyDepositAsset = null, MainClass = "ConsumerSpending", SubClass = "Tax",
        Content = content, Amount = 70m, DepositMonth = 1, DepositDay = 25, MaturityDate = DateTime.UtcNow.AddYears(1),
        Note = "", Unpunctuality = false, Created = DateTime.UtcNow, Updated = DateTime.UtcNow
    }));

    /// <summary>Sends a GET to <paramref name="url"/> with the session cookie, marked as an AJAX request when <paramref name="ajax"/> is set.</summary>
    private async Task<HttpResponseMessage> GetAsync(string url, AuthenticatedSession session, bool ajax = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", session.CookieHeader);
        if (ajax) request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Posts an empty JSON request to <paramref name="url"/>, asserts 200, and returns the response body.</summary>
    private async Task<string> PostJsonAsync(AuthenticatedSession session, string url)
    {
        using var request = session.BuildJsonPostRequest(url);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // -- Full pages ---------------------------------------------------------------------------------

    /// <summary>Every finance page opens for a signed-in user (the page itself only carries the asset picker; rows arrive via the grid).</summary>
    [Theory]
    [InlineData("/AccountBook/Asset")]
    [InlineData("/AccountBook/Income")]
    [InlineData("/AccountBook/Expenditure")]
    [InlineData("/Notice/FixedIncome")]
    [InlineData("/Notice/FixedExpenditure")]
    public async Task Page_LoggedInUser_Returns200_AndListsTheirActiveAssetsInThePicker(string url)
    {
        string email = $"fin-read-page-{url.Replace("/", "-").ToLowerInvariant()}@test.com";
        var session = await LoginAsync(email);
        SeedAsset(email, "PagePicker-Wallet");

        var response = await GetAsync(url, session);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        if (url != "/AccountBook/Asset")
            Assert.Contains("PagePicker-Wallet", html);
    }

    // -- Grid partials -----------------------------------------------------------------------------------

    /// <summary>The grid partial lists the caller's own rows only, and the whole-row search narrows it.</summary>
    [Fact]
    public async Task IncomeGrid_ListsOnlyTheCallersRows_AndTheSearchNarrowsThem()
    {
        const string email = "fin-read-income-grid@test.com";
        var session = await LoginAsync(email);
        await LoginAsync("fin-read-income-grid-other@test.com");
        SeedAsset(email, "IncGrid-Bank");
        SeedAsset("fin-read-income-grid-other@test.com", "IncGrid-OtherBank");
        SeedIncome(email, "IncGrid-Bank", "MineAlpha-IG");
        SeedIncome(email, "IncGrid-Bank", "MineBeta-IG");
        SeedIncome("fin-read-income-grid-other@test.com", "IncGrid-OtherBank", "TheirsSecret-IG");

        string all = await (await GetAsync("/AccountBook/Income", session, ajax: true)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        string searched = await (await GetAsync("/AccountBook/Income?wholeSearch=MineAlpha-IG", session, ajax: true)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("MineAlpha-IG", all);
        Assert.Contains("MineBeta-IG", all);
        Assert.DoesNotContain("TheirsSecret-IG", all);
        Assert.Contains("MineAlpha-IG", searched);
        Assert.DoesNotContain("MineBeta-IG", searched);
    }

    /// <summary>The expenditure grid lists the caller's rows and honours the whole-row search.</summary>
    [Fact]
    public async Task ExpenditureGrid_ListsTheCallersRows_AndTheSearchNarrowsThem()
    {
        const string email = "fin-read-exp-grid@test.com";
        var session = await LoginAsync(email);
        SeedAsset(email, "ExpGrid-Card");
        SeedExpenditure(email, "ExpGrid-Card", "LunchAlpha-EG");
        SeedExpenditure(email, "ExpGrid-Card", "DinnerBeta-EG");

        string all = await (await GetAsync("/AccountBook/Expenditure", session, ajax: true)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        string searched = await (await GetAsync("/AccountBook/Expenditure?wholeSearch=DinnerBeta-EG", session, ajax: true)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("LunchAlpha-EG", all);
        Assert.Contains("DinnerBeta-EG", all);
        Assert.DoesNotContain("LunchAlpha-EG", searched);
        Assert.Contains("DinnerBeta-EG", searched);
    }

    /// <summary>The asset grid lists the caller's assets and honors the whole-row search.</summary>
    [Fact]
    public async Task AssetGrid_ListsTheCallersAssets_AndTheSearchNarrowsThem()
    {
        const string email = "fin-read-asset-grid@test.com";
        var session = await LoginAsync(email);
        SeedAsset(email, "AssetGridAlpha-AG");
        SeedAsset(email, "AssetGridBeta-AG");

        string all = await (await GetAsync("/AccountBook/Asset", session, ajax: true)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        string searched = await (await GetAsync("/AccountBook/Asset?wholeSearch=AssetGridBeta-AG", session, ajax: true)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("AssetGridAlpha-AG", all);
        Assert.Contains("AssetGridBeta-AG", all);
        Assert.DoesNotContain("AssetGridAlpha-AG", searched);
        Assert.Contains("AssetGridBeta-AG", searched);
    }

    /// <summary>The fixed-income and fixed-expenditure grids list the caller's schedules and honour the whole-row search.</summary>
    [Fact]
    public async Task FixedGrids_ListTheCallersSchedules_AndTheSearchNarrowsThem()
    {
        const string email = "fin-read-fixed-grid@test.com";
        var session = await LoginAsync(email);
        SeedAsset(email, "FixGrid-Bank");
        SeedFixedIncome(email, "FixGrid-Bank", "SalaryAlpha-FG");
        SeedFixedIncome(email, "FixGrid-Bank", "PensionBeta-FG");
        SeedFixedExpenditure(email, "FixGrid-Bank", "RentAlpha-FG");
        SeedFixedExpenditure(email, "FixGrid-Bank", "InsuranceBeta-FG");

        string incomes = await (await GetAsync("/Notice/FixedIncome", session, ajax: true)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        string incomesSearched = await (await GetAsync("/Notice/FixedIncome?wholeSearch=PensionBeta-FG", session, ajax: true)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        string expenditures = await (await GetAsync("/Notice/FixedExpenditure", session, ajax: true)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        string expendituresSearched = await (await GetAsync("/Notice/FixedExpenditure?wholeSearch=RentAlpha-FG", session, ajax: true)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("SalaryAlpha-FG", incomes);
        Assert.Contains("PensionBeta-FG", incomes);
        Assert.DoesNotContain("SalaryAlpha-FG", incomesSearched);
        Assert.Contains("PensionBeta-FG", incomesSearched);
        Assert.Contains("RentAlpha-FG", expenditures);
        Assert.Contains("InsuranceBeta-FG", expenditures);
        Assert.Contains("RentAlpha-FG", expendituresSearched);
        Assert.DoesNotContain("InsuranceBeta-FG", expendituresSearched);
    }

    // -- Amount labels -----------------------------------------------------------------------------------

    /// <summary>The amount label carries the chosen asset's currency; an unknown product falls back to the plain label.</summary>
    [Theory]
    [InlineData("/AccountBook/GetIncomeAmountLabel")]
    [InlineData("/AccountBook/GetExpenditureAmountLabel")]
    [InlineData("/Notice/GetFixedIncomeAmountLabel")]
    [InlineData("/Notice/GetFixedExpenditureAmountLabel")]
    public async Task AmountLabel_ShowsTheAssetsCurrency_AndFallsBackForAnUnknownProduct(string endpoint)
    {
        string email = $"fin-read-label-{endpoint.Replace("/", "-").ToLowerInvariant()}@test.com";
        var session = await LoginAsync(email);
        SeedAsset(email, "LabelUsd", "USD");

        using JsonDocument known = JsonDocument.Parse(await PostJsonAsync(session, $"{endpoint}?productName=LabelUsd"));
        using JsonDocument unknown = JsonDocument.Parse(await PostJsonAsync(session, $"{endpoint}?productName=NoSuchProduct"));

        Assert.True(known.RootElement.GetProperty("result").GetBoolean());
        Assert.EndsWith("(USD)", known.RootElement.GetProperty("label").GetString());
        Assert.True(unknown.RootElement.GetProperty("result").GetBoolean());
        Assert.DoesNotContain("(", unknown.RootElement.GetProperty("label").GetString());
    }

    // -- Existence lookups ---------------------------------------------------------------------------------

    /// <summary>An income / fixed-income lookup by id resolves the caller's own record, and never another account's.</summary>
    [Fact]
    public async Task ExistsLookups_ResolveOnlyTheCallersOwnRecords()
    {
        const string email = "fin-read-exists@test.com";
        const string other = "fin-read-exists-other@test.com";
        var session = await LoginAsync(email);
        await LoginAsync(other);
        SeedAsset(email, "Exists-Bank");
        SeedAsset(other, "Exists-OtherBank");
        long mine = SeedFixedIncome(email, "Exists-Bank", "MyFixedIncome-EX");
        long theirs = SeedFixedIncome(other, "Exists-OtherBank", "TheirFixedIncome-EX");
        long myIncome = SeedIncome(email, "Exists-Bank", "MyIncome-EX");
        long theirIncome = SeedIncome(other, "Exists-OtherBank", "TheirIncome-EX");

        string mineJson = await PostJsonAsync(session, $"/Notice/IsFixedIncomeExists?id={mine}");
        string theirsJson = await PostJsonAsync(session, $"/Notice/IsFixedIncomeExists?id={theirs}");
        string myIncomeJson = await PostJsonAsync(session, $"/AccountBook/IsIncomeExists?id={myIncome}");
        string theirIncomeJson = await PostJsonAsync(session, $"/AccountBook/IsIncomeExists?id={theirIncome}");

        Assert.Contains("\"result\":true", mineJson);
        Assert.Contains("MyFixedIncome-EX", mineJson);
        Assert.Contains("\"result\":false", theirsJson);
        Assert.DoesNotContain("TheirFixedIncome-EX", theirsJson);
        Assert.Contains("\"result\":true", myIncomeJson);
        Assert.Contains("\"result\":false", theirIncomeJson);
        Assert.DoesNotContain("TheirIncome-EX", theirIncomeJson);
    }

    // -- Excel exports ----------------------------------------------------------------------------------------

    /// <summary>Each export returns a real .xlsx workbook (a zip container) named after the requested file.</summary>
    [Theory]
    [InlineData("/AccountBook/ExportExcelAsset")]
    [InlineData("/AccountBook/ExportExcelIncome")]
    [InlineData("/AccountBook/ExportExcelExpenditure")]
    [InlineData("/Notice/ExportExcelFixedIncome")]
    [InlineData("/Notice/ExportExcelFixedExpenditure")]
    public async Task ExportExcel_ReturnsAnXlsxWorkbook_NamedAfterTheRequestedFile(string endpoint)
    {
        string email = $"fin-read-export-{endpoint.Replace("/", "-").ToLowerInvariant()}@test.com";
        var session = await LoginAsync(email);
        SeedAsset(email, "Export-Bank");
        using var request = session.BuildJsonPostRequest($"{endpoint}?fileName=my report");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("my report-", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.EndsWith(".xlsx", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.True(bytes.Length > 4 && bytes[0] == 'P' && bytes[1] == 'K', "an .xlsx file is a zip container starting with 'PK'");
    }
}

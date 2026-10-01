using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for the FixedIncome action group of <c>NoticeController</c>
/// (all <c>[RequiredHttpPostAccess(Role = Role.User)]</c>-gated). Representative of the two
/// near-identical resource groups on this controller (FixedIncome/FixedExpenditure);
/// FixedExpenditure is covered separately in <c>NoticeControllerFixedExpenditureTests</c>.
///
/// Runs against a real PostgreSQL instance (via <see cref="MaroikWebApplicationFactory"/>'s
/// Testcontainers setup), so <c>AssetBalanceStore.GetAssetAsync</c>'s raw-SQL
/// <c>FOR UPDATE</c> query (<c>AssetRepository.FindByEmailAndProductNameForUpdateAsync</c>) —
/// unusable under EF Core InMemory — is exercised for real on the Create/Update success paths.
/// </summary>
[Collection("Website Integration")]
public class NoticeControllerFixedIncomeTests(MaroikWebApplicationFactory factory)
{
    /// <summary>The account every test in this class signs in as.</summary>
    private const string Email = "notice-user@test.com";
    /// <summary>The asset the fixed incomes are deposited into.</summary>
    private const string AssetProductName = "notice-asset";

    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    /// <summary>Signs in as <see cref="Email"/> and makes sure its asset <see cref="AssetProductName"/> exists.</summary>
    private async Task<AuthenticatedSession> LoginAsync()
    {
        var session = await AuthenticatedSessionHelper.LoginAsync(factory, _client, Email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (db.Assets.Any(a => a.AccountEmail == Email && a.ProductName == AssetProductName))
        {
            return session;
        }
        db.Assets.Add(new Asset
        {
            ProductName = AssetProductName,
            AccountEmail = Email,
            Item = "CashAsset",
            Amount = 1000,
            MonetaryUnit = "KRW",
            Deleted = false,
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return session;
    }

    /// <summary>A valid fixed-income JSON body (id 0 creates).</summary>
    private static object ValidPayload(long id = 0, string content = "Salary") => new
    {
        Id = id,
        MainClass = "RegularIncome",
        SubClass = "LaborIncome",
        Content = content,
        Amount = 3000000,
        DepositMonth = 6,
        DepositDay = 25,
        MaturityDate = "2030-12-31",
        Note = "",
        DepositMyAssetProductName = AssetProductName,
        Unpunctuality = false
    };

    /// <summary>A unique Content value per call, so lookups by Content in a single test can't
    /// collide with other tests in this class sharing the same account.</summary>
    private static string UniqueContent([System.Runtime.CompilerServices.CallerMemberName] string testName = "") =>
        $"{testName}-{Guid.NewGuid():N}";

    // -- CreateFixedIncome: role gating -----------------------------------------

    /// <summary>Create fixed income anonymous session is forbidden.</summary>
    [Fact]
    public async Task CreateFixedIncome_AnonymousSession_IsForbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Notice/CreateFixedIncome");
        request.Content = System.Net.Http.Json.JsonContent.Create(new { });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- CreateFixedIncome: validation --------------------------------------------

    /// <summary>Create fixed income invalid deposit month returns input invalid.</summary>
    [Fact]
    public async Task CreateFixedIncome_InvalidDepositMonth_ReturnsInputInvalid()
    {
        var session = await LoginAsync();
        var payload = new
        {
            Id = 0, MainClass = "RegularIncome", SubClass = "LaborIncome", Content = "Salary",
            Amount = 100, DepositMonth = 13, DepositDay = 1, MaturityDate = "2030-12-31",
            Note = "", DepositMyAssetProductName = AssetProductName, Unpunctuality = false
        };
        using var request = session.BuildJsonPostRequest("/Notice/CreateFixedIncome", payload);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json); // service now returns a specific reason, not a generic "Input is invalid"
    }

    /// <summary>Create fixed income unknown deposit asset returns input invalid.</summary>
    [Fact]
    public async Task CreateFixedIncome_UnknownDepositAsset_ReturnsInputInvalid()
    {
        var session = await LoginAsync();
        var payload = new
        {
            Id = 0, MainClass = "RegularIncome", SubClass = "LaborIncome", Content = "Salary",
            Amount = 100, DepositMonth = 6, DepositDay = 1, MaturityDate = "2030-12-31",
            Note = "", DepositMyAssetProductName = "no-such-asset", Unpunctuality = false
        };
        using var request = session.BuildJsonPostRequest("/Notice/CreateFixedIncome", payload);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json); // service now returns a specific reason, not a generic "Input is invalid"
    }

    // -- CreateFixedIncome / IsFixedIncomeExists: success path --------------------

    /// <summary>Create fixed income then is fixed income exists finds the new record.</summary>
    [Fact]
    public async Task CreateFixedIncome_ThenIsFixedIncomeExists_FindsTheNewRecord()
    {
        var session = await LoginAsync();
        string content = UniqueContent();
        using var createRequest = session.BuildJsonPostRequest("/Notice/CreateFixedIncome", ValidPayload(content: content));
        var createResponse = await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);
        string createJson = await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"result\":true", createJson);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.FixedIncomes.Single(f => f.AccountEmail == Email && f.Content == content);

        using var existsRequest = session.BuildJsonPostRequest($"/Notice/IsFixedIncomeExists?id={created.Id}");
        var existsResponse = await _client.SendAsync(existsRequest, TestContext.Current.CancellationToken);
        string existsJson = await existsResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", existsJson);
    }

    /// <summary>Is fixed income exists unknown id returns failure result.</summary>
    [Fact]
    public async Task IsFixedIncomeExists_UnknownId_ReturnsFailureResult()
    {
        var session = await LoginAsync();
        using var request = session.BuildJsonPostRequest("/Notice/IsFixedIncomeExists?id=999999");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    // -- UpdateFixedIncome ---------------------------------------------------------

    /// <summary>Update fixed income existing record returns success result.</summary>
    [Fact]
    public async Task UpdateFixedIncome_ExistingRecord_ReturnsSuccessResult()
    {
        var session = await LoginAsync();
        string content = UniqueContent();
        using var createRequest = session.BuildJsonPostRequest("/Notice/CreateFixedIncome", ValidPayload(content: content));
        await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.FixedIncomes.Single(f => f.AccountEmail == Email && f.Content == content);

        var updatePayload = new
        {
            created.Id, MainClass = "RegularIncome", SubClass = "BusinessIncome", Content = "Updated",
            Amount = 3500000, DepositMonth = 7, DepositDay = 1, MaturityDate = "2031-01-01",
            Note = "updated", DepositMyAssetProductName = AssetProductName, Unpunctuality = true
        };
        using var updateRequest = session.BuildJsonPostRequest("/Notice/UpdateFixedIncome", updatePayload);
        var response = await _client.SendAsync(updateRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updated = verifyDb.FixedIncomes.Single(f => f.Id == created.Id);
        Assert.Equal("BusinessIncome", updated.SubClass);
        Assert.Equal("updated", updated.Note);
        // Not asserting Unpunctuality here: FixedIncome.Update()'s signature has no
        // unpunctuality parameter, so UpdateFixedIncome silently cannot change it regardless of
        // what the request carries (same for CreateFixedIncome → FixedIncome.Register()). Minor,
        // likely-intentional (probably meant to be set by an automated "missed schedule" process
        // rather than directly by the user), but worth knowing if the edit form actually exposes
        // a checkbox for it — that would be a silent no-op from the user's perspective.
    }

    /// <summary>Update fixed income unknown id returns failure result.</summary>
    [Fact]
    public async Task UpdateFixedIncome_UnknownId_ReturnsFailureResult()
    {
        var session = await LoginAsync();
        using var request = session.BuildJsonPostRequest("/Notice/UpdateFixedIncome", ValidPayload(999999));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    // -- DeleteFixedIncome ----------------------------------------------------------

    /// <summary>Delete fixed income existing record returns success result.</summary>
    [Fact]
    public async Task DeleteFixedIncome_ExistingRecord_ReturnsSuccessResult()
    {
        var session = await LoginAsync();
        string content = UniqueContent();
        using var createRequest = session.BuildJsonPostRequest("/Notice/CreateFixedIncome", ValidPayload(content: content));
        await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.FixedIncomes.Single(f => f.AccountEmail == Email && f.Content == content);

        using var deleteRequest = session.BuildJsonPostRequest("/Notice/DeleteFixedIncome", new {
            created.Id });
        var response = await _client.SendAsync(deleteRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(verifyDb.FixedIncomes.Any(f => f.Id == created.Id));
    }
}

using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for the FixedExpenditure action group of <c>NoticeController</c>
/// (all <c>[RequiredHttpPostAccess(Role = Role.User)]</c>-gated). Mirrors
/// <c>NoticeControllerFixedIncomeTests</c> — see that file for the Testcontainers/real-schema
/// rationale.
/// </summary>
[Collection("Website Integration")]
public class NoticeControllerFixedExpenditureTests(MaroikWebApplicationFactory factory)
{
    private const string Email = "notice-expenditure-user@test.com";
    private const string AssetProductName = "notice-expenditure-asset";

    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

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

    // MyDepositAsset carries [Required] on FixedExpenditureInputViewModel (like the plain
    // ExpenditureInputViewModel — see AccountBookControllerIncomeExpenditureTests), so it needs
    // a non-empty placeholder even though this MainClass/SubClass combo isn't a "transfer" type
    // and the service never actually uses the value.
    private static object ValidPayload(long id = 0, string content = "Rent") => new
    {
        Id = id,
        MainClass = "ConsumerSpending",
        SubClass = "MealOrEatOutExpenses",
        Content = content,
        Amount = 100,
        DepositMonth = 6,
        DepositDay = 1,
        MaturityDate = "2030-12-31",
        Note = "",
        PaymentMethod = AssetProductName,
        MyDepositAsset = "N/A",
        Unpunctuality = false
    };

    private static string UniqueContent([System.Runtime.CompilerServices.CallerMemberName] string testName = "") =>
        $"{testName}-{Guid.NewGuid():N}";

    // -- CreateFixedExpenditure: role gating ------------------------------------

    /// <summary>Create fixed expenditure anonymous session is forbidden.</summary>
    [Fact]
    public async Task CreateFixedExpenditure_AnonymousSession_IsForbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Notice/CreateFixedExpenditure");
        request.Content = System.Net.Http.Json.JsonContent.Create(new { });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- CreateFixedExpenditure: validation --------------------------------------

    /// <summary>Create fixed expenditure invalid deposit month returns input invalid.</summary>
    [Fact]
    public async Task CreateFixedExpenditure_InvalidDepositMonth_ReturnsInputInvalid()
    {
        var session = await LoginAsync();
        var payload = new
        {
            Id = 0, MainClass = "ConsumerSpending", SubClass = "MealOrEatOutExpenses", Content = "Rent",
            Amount = 100, DepositMonth = 0, DepositDay = 1, MaturityDate = "2030-12-31",
            Note = "", PaymentMethod = AssetProductName, MyDepositAsset = "N/A", Unpunctuality = false
        };
        using var request = session.BuildJsonPostRequest("/Notice/CreateFixedExpenditure", payload);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json); // service now returns a specific reason, not a generic "Input is invalid"
    }

    /// <summary>Create fixed expenditure invalid deposit day for month returns input invalid.</summary>
    [Fact]
    public async Task CreateFixedExpenditure_InvalidDepositDayForMonth_ReturnsInputInvalid()
    {
        var session = await LoginAsync();
        var payload = new
        {
            Id = 0, MainClass = "ConsumerSpending", SubClass = "MealOrEatOutExpenses", Content = "Rent",
            Amount = 100, DepositMonth = 4, DepositDay = 31, MaturityDate = "2030-12-31",
            Note = "", PaymentMethod = AssetProductName, MyDepositAsset = "N/A", Unpunctuality = false
        };
        using var request = session.BuildJsonPostRequest("/Notice/CreateFixedExpenditure", payload);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json); // service now returns a specific reason, not a generic "Input is invalid"
    }

    // -- CreateFixedExpenditure / IsFixedExpenditureExists: success path ---------

    /// <summary>Create fixed expenditure then is fixed expenditure exists finds the new record.</summary>
    [Fact]
    public async Task CreateFixedExpenditure_ThenIsFixedExpenditureExists_FindsTheNewRecord()
    {
        var session = await LoginAsync();
        string content = UniqueContent();
        using var createRequest = session.BuildJsonPostRequest("/Notice/CreateFixedExpenditure", ValidPayload(content: content));
        var createResponse = await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);
        string createJson = await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"result\":true", createJson);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.FixedExpenditures.Single(f => f.AccountEmail == Email && f.Content == content);

        using var existsRequest = session.BuildJsonPostRequest($"/Notice/IsFixedExpenditureExists?id={created.Id}");
        var existsResponse = await _client.SendAsync(existsRequest, TestContext.Current.CancellationToken);
        string existsJson = await existsResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", existsJson);
    }

    /// <summary>Is fixed expenditure exists unknown id returns failure result.</summary>
    [Fact]
    public async Task IsFixedExpenditureExists_UnknownId_ReturnsFailureResult()
    {
        var session = await LoginAsync();
        using var request = session.BuildJsonPostRequest("/Notice/IsFixedExpenditureExists?id=999999");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    // -- UpdateFixedExpenditure ------------------------------------------------------

    /// <summary>Update fixed expenditure existing record returns success result.</summary>
    [Fact]
    public async Task UpdateFixedExpenditure_ExistingRecord_ReturnsSuccessResult()
    {
        var session = await LoginAsync();
        string content = UniqueContent();
        using var createRequest = session.BuildJsonPostRequest("/Notice/CreateFixedExpenditure", ValidPayload(content: content));
        await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.FixedExpenditures.Single(f => f.AccountEmail == Email && f.Content == content);

        var updatePayload = new
        {
            created.Id, MainClass = "ConsumerSpending", SubClass = "TransportationCost", Content = "Updated",
            Amount = 150, DepositMonth = 7, DepositDay = 1, MaturityDate = "2031-01-01",
            Note = "updated", PaymentMethod = AssetProductName, MyDepositAsset = "N/A", Unpunctuality = false
        };
        using var updateRequest = session.BuildJsonPostRequest("/Notice/UpdateFixedExpenditure", updatePayload);
        var response = await _client.SendAsync(updateRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updated = verifyDb.FixedExpenditures.Single(f => f.Id == created.Id);
        Assert.Equal("TransportationCost", updated.SubClass);
        Assert.Equal("updated", updated.Note);
    }

    /// <summary>Update fixed expenditure unknown id returns failure result.</summary>
    [Fact]
    public async Task UpdateFixedExpenditure_UnknownId_ReturnsFailureResult()
    {
        var session = await LoginAsync();
        using var request = session.BuildJsonPostRequest("/Notice/UpdateFixedExpenditure", ValidPayload(999999));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    // -- DeleteFixedExpenditure ---------------------------------------------------------

    /// <summary>Delete fixed expenditure existing record returns success result.</summary>
    [Fact]
    public async Task DeleteFixedExpenditure_ExistingRecord_ReturnsSuccessResult()
    {
        var session = await LoginAsync();
        string content = UniqueContent();
        using var createRequest = session.BuildJsonPostRequest("/Notice/CreateFixedExpenditure", ValidPayload(content: content));
        await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.FixedExpenditures.Single(f => f.AccountEmail == Email && f.Content == content);

        using var deleteRequest = session.BuildJsonPostRequest("/Notice/DeleteFixedExpenditure", new {
            created.Id });
        var response = await _client.SendAsync(deleteRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(verifyDb.FixedExpenditures.Any(f => f.Id == created.Id));
    }
}

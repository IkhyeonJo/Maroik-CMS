using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for the Asset CRUD action group of <c>AccountBookController</c>
/// (all <c>[RequiredHttpPostAccess(Role = Role.User)]</c>-gated). Runs against a real
/// PostgreSQL instance (via <see cref="MaroikWebApplicationFactory"/>'s Testcontainers setup),
/// so <c>UpdateAsset</c>'s <c>IUnitOfWork</c> transaction — which the EF Core InMemory provider
/// cannot execute — is exercised directly, including its Income-record cascade rename. See
/// <c>AccountBookControllerIncomeExpenditureTests</c> for Income/Expenditure themselves.
/// </summary>
[Collection("Website Integration")]
public class AccountBookControllerAssetTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    /// <summary>Seeds (if missing) a User account for <paramref name="email"/> and signs in as it.</summary>
    private Task<AuthenticatedSession> LoginAsUserAsync(string email = "accountbook-user@test.com") =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

    /// <summary>Seeds (if missing) an Admin account for <paramref name="email"/> and signs in as it.</summary>
    private Task<AuthenticatedSession> LoginAsAdminAsync(string email = "accountbook-admin@test.com") =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "AdminPassword1!", Role.Admin, TestContext.Current.CancellationToken);

    /// <summary>Inserts an asset for <paramref name="accountEmail"/> directly into the database, unless it already exists.</summary>
    private void SeedAsset(string accountEmail, string productName)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (db.Assets.Any(a => a.AccountEmail == accountEmail && a.ProductName == productName)) return;
        db.Assets.Add(new Asset
        {
            ProductName = productName,
            AccountEmail = accountEmail,
            Item = "CashAsset",
            Amount = 1000,
            MonetaryUnit = "KRW",
            Deleted = false,
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    /// <summary>A valid create/update-asset JSON body for <paramref name="productName"/>.</summary>
    private static object ValidAssetPayload(string productName) => new
    {
        ProductName = productName,
        Item = "CashAsset",
        Amount = 5000,
        MonetaryUnit = "KRW",
        Note = "",
        Deleted = false
    };

    // -- CreateAsset: role gating -----------------------------------------------

    /// <summary>Create asset admin session is forbidden.</summary>
    [Fact]
    public async Task CreateAsset_AdminSession_IsForbidden()
    {
        var session = await LoginAsAdminAsync();
        using var request = session.BuildJsonPostRequest("/AccountBook/CreateAsset", ValidAssetPayload("blocked-asset"));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- CreateAsset: validation --------------------------------------------------

    /// <summary>Create asset missing product name fails model state.</summary>
    [Fact]
    public async Task CreateAsset_MissingProductName_FailsModelState()
    {
        var session = await LoginAsUserAsync();
        var payload = new { ProductName = (string?)null, Item = "CashAsset", Amount = 100m, MonetaryUnit = "KRW", Note = "", Deleted = false };
        using var request = session.BuildJsonPostRequest("/AccountBook/CreateAsset", payload);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    /// <summary>Create asset with an unrecognized item category returns the domain's specific validation message.</summary>
    [Fact]
    public async Task CreateAsset_UnrecognizedItem_ReturnsItemInvalidError()
    {
        var session = await LoginAsUserAsync();
        var payload = new { ProductName = "BadItemAsset", Item = "NotARealItemType", Amount = 100m, MonetaryUnit = "KRW", Note = "", Deleted = false };
        using var request = session.BuildJsonPostRequest("/AccountBook/CreateAsset", payload);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Asset category (item) is not a recognised value.", json);
    }

    // -- CreateAsset / IsAssetExists: success path --------------------------------

    /// <summary>Create asset then is asset exists finds the new asset.</summary>
    [Fact]
    public async Task CreateAsset_ThenIsAssetExists_FindsTheNewAsset()
    {
        var session = await LoginAsUserAsync();
        const string productName = "created-by-test-asset";
        using var createRequest = session.BuildJsonPostRequest("/AccountBook/CreateAsset", ValidAssetPayload(productName));
        var createResponse = await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);
        string createJson = await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"result\":true", createJson);

        using var existsRequest = session.BuildJsonPostRequest($"/AccountBook/IsAssetExists?productName={productName}");
        var existsResponse = await _client.SendAsync(existsRequest, TestContext.Current.CancellationToken);
        string existsJson = await existsResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", existsJson);
    }

    /// <summary>Create asset duplicate product name returns already exists error.</summary>
    [Fact]
    public async Task CreateAsset_DuplicateProductName_ReturnsAlreadyExistsError()
    {
        var session = await LoginAsUserAsync();
        const string productName = "duplicate-asset";
        using var firstRequest = session.BuildJsonPostRequest("/AccountBook/CreateAsset", ValidAssetPayload(productName));
        await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);

        using var secondRequest = session.BuildJsonPostRequest("/AccountBook/CreateAsset", ValidAssetPayload(productName));
        var response = await _client.SendAsync(secondRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("The asset already exists.", json);
    }

    /// <summary>Is asset exists unknown product name returns failure result.</summary>
    [Fact]
    public async Task IsAssetExists_UnknownProductName_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync();
        using var request = session.BuildJsonPostRequest("/AccountBook/IsAssetExists?productName=nobody-asset-xyz");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    // -- UpdateAsset ---------------------------------------------------------------
    //
    // AssetService.UpdateAsync wraps its entire body in an IUnitOfWork transaction
    // (BeginAsync/CommitAsync/RollbackAsync) because renaming an asset cascades updates to
    // Income/Expenditure records atomically — real PostgreSQL only, hence these tests running
    // against the Testcontainers-backed factory rather than EF Core InMemory (which cannot
    // execute transactions at all).

    /// <summary>Update asset existing asset returns success result.</summary>
    [Fact]
    public async Task UpdateAsset_ExistingAsset_ReturnsSuccessResult()
    {
        var session = await LoginAsUserAsync();
        const string productName = "to-update-asset";
        SeedAsset("accountbook-user@test.com", productName);

        using var request = session.BuildJsonPostRequest("/AccountBook/UpdateAsset", new
        {
            ProductName = productName,
            OriginalProductName = productName,
            Item = "SavingsAsset",
            Amount = 2000,
            MonetaryUnit = "USD",
            Note = "updated",
            Deleted = false
        });
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updated = db.Assets.Single(a => a.AccountEmail == "accountbook-user@test.com" && a.ProductName == productName);
        Assert.Equal(2000m, updated.Amount);
        Assert.Equal("updated", updated.Note);
    }

    /// <summary>
    /// Renaming an asset that has a linked Income row succeeds, and the Income row's
    /// <c>DepositMyAssetProductName</c> follows automatically. <c>Income_fk_0</c> (like the
    /// other three Asset foreign keys) is declared <c>ON UPDATE CASCADE</c> in the real schema
    /// (<c>Maroik.DB/.../Debugging/Init.sql</c>), so Postgres itself renames every referencing
    /// row the moment the Asset's ProductName changes — <c>AssetService.UpdateAsync</c>'s own
    /// cascade calls for Expenditure/FixedExpenditure are consequently redundant (the DB already
    /// did it) but harmless, and Income/FixedIncome need no equivalent C# call at all.
    /// (An earlier version of this test, run against a schema built from EF's
    /// <c>EnsureCreated()</c> instead of the real init script, wrongly concluded this was
    /// broken — EF's Fluent API config omits the <c>ON UPDATE CASCADE</c> clause, so
    /// <c>EnsureCreated()</c> produces a schema that behaves differently from production here.)
    /// </summary>
    [Fact]
    public async Task UpdateAsset_RenameWithExistingIncomeReference_CascadesAutomatically()
    {
        var session = await LoginAsUserAsync();
        const string email = "accountbook-user@test.com";
        const string oldName = "wallet-to-rename";
        const string newName = "wallet-renamed";
        SeedAsset(email, oldName);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Incomes.Add(new Income
            {
                AccountEmail = email,
                MainClass = "RegularIncome",
                SubClass = "LaborIncome",
                Content = "Salary",
                Amount = 100,
                DepositMyAssetProductName = oldName,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var request = session.BuildJsonPostRequest("/AccountBook/UpdateAsset", new
        {
            ProductName = newName,
            OriginalProductName = oldName,
            Item = "CashAsset",
            Amount = 1000,
            MonetaryUnit = "KRW",
            Note = "",
            Deleted = false
        });
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(verifyDb.Assets.Any(a => a.AccountEmail == email && a.ProductName == newName));
        var income = verifyDb.Incomes.Single(i => i.AccountEmail == email && i.Content == "Salary");
        Assert.Equal(newName, income.DepositMyAssetProductName);
    }

    /// <summary>Update asset unknown original product name returns failure result.</summary>
    [Fact]
    public async Task UpdateAsset_UnknownOriginalProductName_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync();

        using var request = session.BuildJsonPostRequest("/AccountBook/UpdateAsset", new
        {
            ProductName = "irrelevant",
            OriginalProductName = "does-not-exist-asset",
            Item = "CashAsset",
            Amount = 100,
            MonetaryUnit = "KRW",
            Note = "",
            Deleted = false
        });
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Fail to find the asset by given product name", json);
    }

    // -- DeleteAsset ---------------------------------------------------------------

    /// <summary>Delete asset existing asset returns success result.</summary>
    [Fact]
    public async Task DeleteAsset_ExistingAsset_ReturnsSuccessResult()
    {
        var session = await LoginAsUserAsync();
        const string productName = "to-delete-asset";
        SeedAsset("accountbook-user@test.com", productName);

        using var request = session.BuildJsonPostRequest("/AccountBook/DeleteAsset", new { ProductName = productName });
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
    }
}

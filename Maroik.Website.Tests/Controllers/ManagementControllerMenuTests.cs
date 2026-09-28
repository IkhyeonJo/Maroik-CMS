using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Website.Constants;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for the Menu (Category/SubCategory) CRUD action group of
/// <c>ManagementController</c> — previously entirely uncovered beyond a GET smoke test in
/// <c>ManagementControllerTests.cs</c>. Along the way this uncovered a real bug in
/// <c>MenuService.UpdateCategoryAsync</c>/<c>UpdateSubCategoryAsync</c>: both built the updated
/// entity via <c>Reconstitute</c> and called the repository's generic <c>UpdateEntityAsync</c>
/// directly, which silently no-ops (returns without throwing) when the given ID doesn't match
/// any row — so updating a nonexistent category/sub-category used to report
/// "successfully updated" while changing nothing. Fixed by checking existence first, matching
/// the fetch-then-act pattern already used elsewhere (e.g. <c>AccountService.UpdateAccountAsync</c>).
/// </summary>
[Collection("Website Integration")]
public class ManagementControllerMenuTests(MaroikWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);
    
    private Task<AuthenticatedSession> LoginAsAdminAsync(string email = "management-menu-admin@test.com") =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "AdminPassword1!", Role.Admin, TestContext.Current.CancellationToken);

    private Task<AuthenticatedSession> LoginAsUserAsync(string email = "management-menu-user@test.com") =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

    private static string UniqueName(string testName = "") =>
        $"{testName}-{Guid.NewGuid():N}";

    private static object ValidCategoryPayload(string name, int id = 0) => new
    {
        Id = id,
        Name = name,
        DisplayName = name,
        IconPath = "/icons/test.png",
        Controller = "Notice",
        Action = "",
        Role = Role.User,
        Order = 500
    };

    private static object ValidSubCategoryPayload(string name, long categoryId, int id = 0) => new
    {
        Id = id,
        CategoryId = categoryId,
        Name = name,
        DisplayName = name,
        IconPath = "/icons/test.png",
        Controller = "",
        Action = "Index",
        Role = Role.User,
        Order = 500
    };

    // -- CreateCategory: role gating --------------------------------------------

    /// <summary>Create category user session is forbidden.</summary>
    [Fact]
    public async Task CreateCategory_UserSession_IsForbidden()
    {
        var user = await LoginAsUserAsync();
        using var request = user.BuildJsonPostRequest("/Management/CreateCategory", ValidCategoryPayload(UniqueName()));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- CreateCategory / IsCategoryExists: success path ------------------------

    /// <summary>Create category then is category exists finds the new category.</summary>
    [Fact]
    public async Task CreateCategory_ThenIsCategoryExists_FindsTheNewCategory()
    {
        var admin = await LoginAsAdminAsync();
        string name = UniqueName();
        using var createRequest = admin.BuildJsonPostRequest("/Management/CreateCategory", ValidCategoryPayload(name));
        var createResponse = await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);
        string createJson = await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"result\":true", createJson);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.Categories.Single(c => c.Name == name);

        using var existsRequest = admin.BuildJsonPostRequest($"/Management/IsCategoryExists?id={created.Id}");
        var existsResponse = await _client.SendAsync(existsRequest, TestContext.Current.CancellationToken);
        string existsJson = await existsResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", existsJson);
    }

    /// <summary>Is category exists unknown id returns failure result.</summary>
    [Fact]
    public async Task IsCategoryExists_UnknownId_ReturnsFailureResult()
    {
        var admin = await LoginAsAdminAsync();
        using var request = admin.BuildJsonPostRequest("/Management/IsCategoryExists?id=999999");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    // -- UpdateCategory ----------------------------------------------------------

    /// <summary>Update category existing category persists changes.</summary>
    [Fact]
    public async Task UpdateCategory_ExistingCategory_PersistsChanges()
    {
        var admin = await LoginAsAdminAsync();
        string name = UniqueName();
        using var createRequest = admin.BuildJsonPostRequest("/Management/CreateCategory", ValidCategoryPayload(name));
        await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.Categories.Single(c => c.Name == name);

        string updatedName = UniqueName();
        using var updateRequest = admin.BuildJsonPostRequest("/Management/UpdateCategory", ValidCategoryPayload(updatedName, (int)created.Id));
        var response = await _client.SendAsync(updateRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(updatedName, verifyDb.Categories.Single(c => c.Id == created.Id).Name);
    }

    /// <summary>
    /// Regression test for the "silently no-ops but reports success" bug found and fixed in
    /// <c>MenuService.UpdateCategoryAsync</c> — see this file's class-level doc comment.
    /// </summary>
    [Fact]
    public async Task UpdateCategory_UnknownId_ReturnsFailureResult()
    {
        var admin = await LoginAsAdminAsync();
        using var request = admin.BuildJsonPostRequest("/Management/UpdateCategory", ValidCategoryPayload("Ghost", 999999));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    // -- DeleteCategory ------------------------------------------------------------

    /// <summary>Delete category existing category removes it.</summary>
    [Fact]
    public async Task DeleteCategory_ExistingCategory_RemovesIt()
    {
        var admin = await LoginAsAdminAsync();
        string name = UniqueName();
        using var createRequest = admin.BuildJsonPostRequest("/Management/CreateCategory", ValidCategoryPayload(name));
        await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.Categories.Single(c => c.Name == name);

        using var deleteRequest = admin.BuildJsonPostRequest("/Management/DeleteCategory", ValidCategoryPayload(name, (int)created.Id));
        var response = await _client.SendAsync(deleteRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(verifyDb.Categories.Any(c => c.Id == created.Id));
    }

    // -- CreateSubCategory / IsSubCategoryExists: success path -------------------

    /// <summary>Create sub category then is sub category exists finds the new sub category.</summary>
    [Fact]
    public async Task CreateSubCategory_ThenIsSubCategoryExists_FindsTheNewSubCategory()
    {
        var admin = await LoginAsAdminAsync();
        string catName = UniqueName("cat");
        using var createCatRequest = admin.BuildJsonPostRequest("/Management/CreateCategory", ValidCategoryPayload(catName));
        await _client.SendAsync(createCatRequest, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = db.Categories.Single(c => c.Name == catName);

        string subName = UniqueName();
        using var createSubRequest = admin.BuildJsonPostRequest("/Management/CreateSubCategory", ValidSubCategoryPayload(subName, category.Id));
        var createResponse = await _client.SendAsync(createSubRequest, TestContext.Current.CancellationToken);
        string createJson = await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"result\":true", createJson);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdSub = verifyDb.SubCategories.Single(s => s.Name == subName);

        using var existsRequest = admin.BuildJsonPostRequest($"/Management/IsSubCategoryExists?id={createdSub.Id}");
        var existsResponse = await _client.SendAsync(existsRequest, TestContext.Current.CancellationToken);
        string existsJson = await existsResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", existsJson);
    }

    /// <summary>Is sub category exists unknown id returns failure result.</summary>
    [Fact]
    public async Task IsSubCategoryExists_UnknownId_ReturnsFailureResult()
    {
        var admin = await LoginAsAdminAsync();
        using var request = admin.BuildJsonPostRequest("/Management/IsSubCategoryExists?id=999999");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    // -- UpdateSubCategory ----------------------------------------------------------

    /// <summary>Update sub category unknown id returns failure result.</summary>
    [Fact]
    public async Task UpdateSubCategory_UnknownId_ReturnsFailureResult()
    {
        var admin = await LoginAsAdminAsync();
        using var request = admin.BuildJsonPostRequest("/Management/UpdateSubCategory", ValidSubCategoryPayload("Ghost", categoryId: 1, id: 999999));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    // -- DeleteSubCategory ------------------------------------------------------------

    /// <summary>Delete sub category existing sub category removes it.</summary>
    [Fact]
    public async Task DeleteSubCategory_ExistingSubCategory_RemovesIt()
    {
        var admin = await LoginAsAdminAsync();
        string catName = UniqueName("cat");
        using var createCatRequest = admin.BuildJsonPostRequest("/Management/CreateCategory", ValidCategoryPayload(catName));
        await _client.SendAsync(createCatRequest, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = db.Categories.Single(c => c.Name == catName);

        string subName = UniqueName();
        using var createSubRequest = admin.BuildJsonPostRequest("/Management/CreateSubCategory", ValidSubCategoryPayload(subName, category.Id));
        await _client.SendAsync(createSubRequest, TestContext.Current.CancellationToken);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdSub = verifyDb.SubCategories.Single(s => s.Name == subName);

        using var deleteRequest = admin.BuildJsonPostRequest("/Management/DeleteSubCategory", ValidSubCategoryPayload(subName, category.Id, (int)createdSub.Id));
        var response = await _client.SendAsync(deleteRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var finalScope = factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(finalDb.SubCategories.Any(s => s.Id == createdSub.Id));
    }

    // -- Cache invalidation --------------------------------------------------------

    private static readonly string[] _navigationCacheKeyList =
    [
        NavigationCacheKeys.AdminCategories,
        NavigationCacheKeys.AdminSubCategories,
        NavigationCacheKeys.UserCategories,
        NavigationCacheKeys.UserSubCategories,
        NavigationCacheKeys.AnonymousCategories,
        NavigationCacheKeys.AnonymousSubCategories
    ];

    /// <summary>
    /// Warms the navigation cache with real data (via an unrelated admin request), sends
    /// <paramref name="request"/>, asserts it reported success, then asserts every navigation
    /// cache entry was cleared by it. Used to guard each menu-mutating action's call to
    /// ManagementController's InvalidateNavigationCacheAsync — these edits must clear the
    /// distributed-cache entries, not rely solely on the 10-minute sliding TTL in
    /// AuthorizationFilter to eventually pick up the change.
    /// </summary>
    private async Task AssertClearsNavigationCacheAsync(AuthenticatedSession admin, HttpRequestMessage request)
    {
        var cache = factory.Services.GetRequiredService<IDistributedCache>();

        // Warm the cache with real data via any admin request — AuthorizationFilter populates
        // it from the DB on first miss for every request, not just menu-editing ones.
        using var warmupRequest = new HttpRequestMessage(HttpMethod.Get, "/Management/Profile");
        warmupRequest.Headers.Add("Cookie", admin.CookieHeader);
        var warmupResponse = await _client.SendAsync(warmupRequest, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, warmupResponse.StatusCode);

        foreach (var key in _navigationCacheKeyList)
        {
            Assert.NotNull(await cache.GetAsync(key, TestContext.Current.CancellationToken));
        }

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"result\":true", json);

        foreach (var key in _navigationCacheKeyList)
        {
            Assert.Null(await cache.GetAsync(key, TestContext.Current.CancellationToken));
        }
    }

    /// <summary>Create category clears navigation cache entries.</summary>
    [Fact]
    public async Task CreateCategory_ClearsNavigationCacheEntries()
    {
        var admin = await LoginAsAdminAsync();

        using var request = admin.BuildJsonPostRequest("/Management/CreateCategory", ValidCategoryPayload(UniqueName()));
        await AssertClearsNavigationCacheAsync(admin, request);
    }

    /// <summary>Update category clears navigation cache entries.</summary>
    [Fact]
    public async Task UpdateCategory_ClearsNavigationCacheEntries()
    {
        var admin = await LoginAsAdminAsync();
        string name = UniqueName();
        using var createRequest = admin.BuildJsonPostRequest("/Management/CreateCategory", ValidCategoryPayload(name));
        await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.Categories.Single(c => c.Name == name);

        using var request = admin.BuildJsonPostRequest("/Management/UpdateCategory", ValidCategoryPayload(UniqueName(), (int)created.Id));
        await AssertClearsNavigationCacheAsync(admin, request);
    }

    /// <summary>Delete category clears navigation cache entries.</summary>
    [Fact]
    public async Task DeleteCategory_ClearsNavigationCacheEntries()
    {
        var admin = await LoginAsAdminAsync();
        string name = UniqueName();
        using var createRequest = admin.BuildJsonPostRequest("/Management/CreateCategory", ValidCategoryPayload(name));
        await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.Categories.Single(c => c.Name == name);

        using var request = admin.BuildJsonPostRequest("/Management/DeleteCategory", ValidCategoryPayload(name, (int)created.Id));
        await AssertClearsNavigationCacheAsync(admin, request);
    }

    /// <summary>Create sub category clears navigation cache entries.</summary>
    [Fact]
    public async Task CreateSubCategory_ClearsNavigationCacheEntries()
    {
        var admin = await LoginAsAdminAsync();
        string catName = UniqueName("cat");
        using var createCatRequest = admin.BuildJsonPostRequest("/Management/CreateCategory", ValidCategoryPayload(catName));
        await _client.SendAsync(createCatRequest, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = db.Categories.Single(c => c.Name == catName);

        using var request = admin.BuildJsonPostRequest("/Management/CreateSubCategory", ValidSubCategoryPayload(UniqueName(), category.Id));
        await AssertClearsNavigationCacheAsync(admin, request);
    }

    /// <summary>Update sub category clears navigation cache entries.</summary>
    [Fact]
    public async Task UpdateSubCategory_ClearsNavigationCacheEntries()
    {
        var admin = await LoginAsAdminAsync();
        string catName = UniqueName("cat");
        using var createCatRequest = admin.BuildJsonPostRequest("/Management/CreateCategory", ValidCategoryPayload(catName));
        await _client.SendAsync(createCatRequest, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = db.Categories.Single(c => c.Name == catName);

        string subName = UniqueName();
        using var createSubRequest = admin.BuildJsonPostRequest("/Management/CreateSubCategory", ValidSubCategoryPayload(subName, category.Id));
        await _client.SendAsync(createSubRequest, TestContext.Current.CancellationToken);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdSub = verifyDb.SubCategories.Single(s => s.Name == subName);

        using var request = admin.BuildJsonPostRequest("/Management/UpdateSubCategory", ValidSubCategoryPayload(UniqueName(), category.Id, (int)createdSub.Id));
        await AssertClearsNavigationCacheAsync(admin, request);
    }

    /// <summary>Delete sub category clears navigation cache entries.</summary>
    [Fact]
    public async Task DeleteSubCategory_ClearsNavigationCacheEntries()
    {
        var admin = await LoginAsAdminAsync();
        string catName = UniqueName("cat");
        using var createCatRequest = admin.BuildJsonPostRequest("/Management/CreateCategory", ValidCategoryPayload(catName));
        await _client.SendAsync(createCatRequest, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = db.Categories.Single(c => c.Name == catName);

        string subName = UniqueName();
        using var createSubRequest = admin.BuildJsonPostRequest("/Management/CreateSubCategory", ValidSubCategoryPayload(subName, category.Id));
        await _client.SendAsync(createSubRequest, TestContext.Current.CancellationToken);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdSub = verifyDb.SubCategories.Single(s => s.Name == subName);

        using var request = admin.BuildJsonPostRequest("/Management/DeleteSubCategory", ValidSubCategoryPayload(subName, category.Id, (int)createdSub.Id));
        await AssertClearsNavigationCacheAsync(admin, request);
    }
}

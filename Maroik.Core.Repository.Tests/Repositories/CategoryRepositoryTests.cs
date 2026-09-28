using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Menu;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using OrmCategory = Maroik.Core.PostgreSQL.Models.Category;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="CategoryRepository"/> against a real PostgreSQL database (Testcontainers)
/// preloaded with the <c>Init.sql</c> schema and seed data (13 navigation categories).
/// <c>Category</c> is a root table (no FK) with the checks <c>Category_Role_check</c> and
/// <c>Category_Order_check</c>. The global <c>GetAllAsync</c> also returns the seed rows, so its
/// assertions are scoped to the names this test inserts.
/// </summary>
public sealed class CategoryRepositoryTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    private CategoryRepository Sut => new(Context);

    private static OrmCategory MakeCategory(string name, string role = Role.User, long order = 1) => new()
    {
        Name = name,
        DisplayName = name,
        IconPath = "/icons/icon.svg",
        Controller = name,
        Action = "Index",
        Role = role,
        Order = order
    };

    private async Task SeedAsync(params OrmCategory[] categories)
    {
        await Context.Categories.AddRangeAsync(categories);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    // -- GetAllAsync ----------------------------------------------------------

    /// <summary>Verifies that <c>GetAllAsync</c> includes every category this test inserted.</summary>
    [Fact]
    public async Task GetAllAsync_ReturnsInsertedCategories()
    {
        string a = Unique("Dashboard");
        string b = Unique("Finance");
        await SeedAsync(MakeCategory(a, order: 1), MakeCategory(b, order: 2));

        List<Category> result = [.. await Sut.GetAllAsync(TestContext.Current.CancellationToken)];

        Assert.Equal(2, result.Count(c => c.Name == a || c.Name == b));
    }

    /// <summary>Verifies that <c>GetAllAsync</c> also returns the seeded navigation categories.</summary>
    [Fact]
    public async Task GetAllAsync_IncludesSeededCategories()
    {
        List<Category> result = [.. await Sut.GetAllAsync(TestContext.Current.CancellationToken)];

        Assert.Contains(result, c => c is { Controller: "Dashboard", Role: Role.Admin });
    }

    // -- CreateAsync --------------------------------------------------

    /// <summary>Verifies that <c>CreateAsync</c> inserts a category row.</summary>
    [Fact]
    public async Task CreateAsync_AddsCategoryToDatabase()
    {
        string name = Unique("NewMenu");
        var category = Category.Reconstitute(
            id: 0, name: name, displayName: "New Menu", iconPath: "/icons/new.svg",
            controller: name, action: "Index", role: Role.User, order: 5);

        await Sut.CreateAsync(category, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmCategory? saved = await Context.Categories.FirstOrDefaultAsync(c => c.Name == name, TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal(Role.User, saved.Role);
        Assert.Equal(5, saved.Order);
    }

    /// <summary>PostgreSQL enforces <c>Category_Role_check</c> — an unknown role throws (InMemory did not).</summary>
    [Fact]
    public async Task CreateAsync_Throws_WhenRoleIsNotAllowed()
    {
        var category = Category.Reconstitute(
            id: 0, name: Unique("BadRole"), displayName: "x", iconPath: "/x",
            controller: "x", action: "Index", role: "Superuser", order: 0);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => Sut.CreateAsync(category, TestContext.Current.CancellationToken));
    }

    // -- UpdateEntityAsync --------------------------------------------------

    /// <summary>Verifies that <c>UpdateEntityAsync</c> persists changes to an existing category.</summary>
    [Fact]
    public async Task UpdateEntityAsync_UpdatesExistingCategory()
    {
        var seed = MakeCategory(Unique("OldName"), order: 1);
        await SeedAsync(seed);

        string newName = Unique("UpdatedName");
        var category = Category.Reconstitute(
            id: seed.Id, name: newName, displayName: "Updated Display", iconPath: "/icons/updated.svg",
            controller: newName, action: "Index", role: Role.Admin, order: 10);

        await Sut.UpdateEntityAsync(category, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmCategory? updated = await Context.Categories.FirstOrDefaultAsync(c => c.Id == seed.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(newName, updated.Name);
        Assert.Equal(Role.Admin, updated.Role);
        Assert.Equal(10, updated.Order);
    }

    // -- DeleteByIdAsync --------------------------------------------------

    /// <summary>Verifies that <c>DeleteByIdAsync</c> removes the category row.</summary>
    [Fact]
    public async Task DeleteByIdAsync_RemovesCategoryFromDatabase()
    {
        var seed = MakeCategory(Unique("ToDelete"));
        await SeedAsync(seed);

        await Sut.DeleteByIdAsync(seed.Id, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Assert.Null(await Context.Categories.FirstOrDefaultAsync(c => c.Id == seed.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies that <c>DeleteByIdAsync</c> does not affect other categories.</summary>
    [Fact]
    public async Task DeleteByIdAsync_DoesNotAffectOtherCategories()
    {
        var keep = MakeCategory(Unique("Keep"), order: 1);
        var remove = MakeCategory(Unique("Remove"), order: 2);
        await SeedAsync(keep, remove);

        await Sut.DeleteByIdAsync(remove.Id, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Assert.True(await Context.Categories.AnyAsync(c => c.Id == keep.Id, TestContext.Current.CancellationToken));
        Assert.False(await Context.Categories.AnyAsync(c => c.Id == remove.Id, TestContext.Current.CancellationToken));
    }
}

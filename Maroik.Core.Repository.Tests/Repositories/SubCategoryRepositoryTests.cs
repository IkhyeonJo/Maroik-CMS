using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Menu;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using OrmCategory = Maroik.Core.PostgreSQL.Models.Category;
using OrmSubCategory = Maroik.Core.PostgreSQL.Models.SubCategory;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="SubCategoryRepository"/> against a real PostgreSQL database (Testcontainers)
/// preloaded with the <c>Init.sql</c> schema and seed data (14 sub-categories).
/// <c>SubCategory</c> carries the FK <c>SubCategory_fk_0</c> to <c>Category.ID</c> (unenforced by
/// InMemory) plus <c>SubCategory_Role_check</c> / <c>SubCategory_Order_check</c>. Every test creates
/// a real parent category and scopes the global <c>GetAllAsync</c> assertions to its own rows.
/// </summary>
public sealed class SubCategoryRepositoryTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    private SubCategoryRepository Sut => new(Context);

    private static OrmSubCategory MakeSubCategory(long categoryId, string name, string role = Role.User, long order = 1) => new()
    {
        CategoryId = categoryId,
        Name = name,
        DisplayName = name,
        IconPath = "/icons/sub.svg",
        Action = name,
        Role = role,
        Order = order
    };

    /// <summary>Inserts a real parent <c>Category</c> and returns its id.</summary>
    private async Task<long> SeedCategoryAsync()
    {
        var category = new OrmCategory
        {
            Name = Unique("ParentCategory"),
            DisplayName = "Parent",
            Controller = Unique("ParentController"),
            IconPath = "/icons/icon.svg",
            Action = "Index",
            Role = Role.User,
            Order = 1
        };
        await Context.Categories.AddAsync(category);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return category.Id;
    }

    private async Task SeedAsync(params OrmSubCategory[] items)
    {
        await Context.SubCategories.AddRangeAsync(items);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    // -- GetAllAsync ----------------------------------------------------------

    /// <summary>Verifies that <c>GetAllAsync</c> includes every sub-category this test inserted.</summary>
    [Fact]
    public async Task GetAllAsync_ReturnsInsertedSubCategories()
    {
        long catId = await SeedCategoryAsync();
        string a = Unique("SubA");
        string b = Unique("SubB");
        await SeedAsync(MakeSubCategory(catId, a, order: 1), MakeSubCategory(catId, b, order: 2));

        List<SubCategory> result = [.. await Sut.GetAllAsync(TestContext.Current.CancellationToken)];

        Assert.Equal(2, result.Count(sc => sc.Name == a || sc.Name == b));
    }

    // -- CreateAsync -----------------------------------------------

    /// <summary>Verifies that <c>CreateAsync</c> inserts a sub-category row.</summary>
    [Fact]
    public async Task CreateAsync_AddsSubCategoryToDatabase()
    {
        long catId = await SeedCategoryAsync();
        string name = Unique("NewSubMenu");

        var subCategory = SubCategory.Reconstitute(
            id: 0, categoryId: catId, name: name, displayName: "New Sub Menu",
            iconPath: "/icons/new.svg", action: "NewAction", role: Role.User, order: 5);

        await Sut.CreateAsync(subCategory, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmSubCategory? saved = await Context.SubCategories.FirstOrDefaultAsync(sc => sc.Name == name, TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal(catId, saved.CategoryId);
        Assert.Equal(Role.User, saved.Role);
    }

    /// <summary>PostgreSQL enforces <c>SubCategory_fk_0</c> — a sub-category under a non-existent category throws (InMemory did not).</summary>
    [Fact]
    public async Task CreateAsync_Throws_WhenParentCategoryDoesNotExist()
    {
        var subCategory = SubCategory.Reconstitute(
            id: 0, categoryId: long.MaxValue, name: Unique("Orphan"), displayName: "x",
            iconPath: "/x", action: "x", role: Role.User, order: 0);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => Sut.CreateAsync(subCategory, TestContext.Current.CancellationToken));
    }

    // -- UpdateEntityAsync -----------------------------------------------

    /// <summary>Verifies that <c>UpdateEntityAsync</c> persists changes to an existing sub-category.</summary>
    [Fact]
    public async Task UpdateEntityAsync_UpdatesExistingSubCategory()
    {
        long catId = await SeedCategoryAsync();
        var seed = MakeSubCategory(catId, Unique("OldSub"), order: 1);
        await SeedAsync(seed);

        string newName = Unique("UpdatedSub");
        var subCategory = SubCategory.Reconstitute(
            id: seed.Id, categoryId: catId, name: newName, displayName: "Updated Sub",
            iconPath: "/icons/updated.svg", action: "UpdatedAction", role: Role.Admin, order: 10);

        await Sut.UpdateEntityAsync(subCategory, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmSubCategory? updated = await Context.SubCategories.FirstOrDefaultAsync(sc => sc.Id == seed.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(newName, updated.Name);
        Assert.Equal(Role.Admin, updated.Role);
        Assert.Equal(10, updated.Order);
    }

    // -- DeleteByIdAsync -----------------------------------------------

    /// <summary>Verifies that <c>DeleteByIdAsync</c> removes the sub-category row.</summary>
    [Fact]
    public async Task DeleteByIdAsync_RemovesSubCategoryFromDatabase()
    {
        long catId = await SeedCategoryAsync();
        var seed = MakeSubCategory(catId, Unique("ToDelete"));
        await SeedAsync(seed);

        await Sut.DeleteByIdAsync(seed.Id, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Assert.Null(await Context.SubCategories.FirstOrDefaultAsync(sc => sc.Id == seed.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies that <c>DeleteByIdAsync</c> does not affect other sub-categories.</summary>
    [Fact]
    public async Task DeleteByIdAsync_DoesNotAffectOtherSubCategories()
    {
        long catId = await SeedCategoryAsync();
        var keep = MakeSubCategory(catId, Unique("Sub1"), order: 1);
        var remove = MakeSubCategory(catId, Unique("Sub2"), order: 2);
        await SeedAsync(keep, remove);

        await Sut.DeleteByIdAsync(remove.Id, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Assert.True(await Context.SubCategories.AnyAsync(sc => sc.Id == keep.Id, TestContext.Current.CancellationToken));
        Assert.False(await Context.SubCategories.AnyAsync(sc => sc.Id == remove.Id, TestContext.Current.CancellationToken));
    }
}

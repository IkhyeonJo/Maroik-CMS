using ErrorOr;
using Maroik.Core.Domain.Accounts;
using Maroik.Core.Domain.Errors;
using Maroik.Core.Domain.Menus;

namespace Maroik.Core.Domain.Tests.Menus;

/// <summary>
/// Unit tests for <see cref="MenuSafeguardPolicy"/> and <see cref="MenuSnapshot"/>: a menu change may not take away
/// the administrators' last route to the menu-management page (<c>Management/Menu</c>), or nobody could repair the
/// menu from the UI again.
/// </summary>
public class MenuSafeguardPolicyTests
{
    /// <summary>The seeded Admin "Management" category: a dropdown (no direct action) whose items are sub-categories.</summary>
    private static Category AdminManagement(long id = 2, string? role = Role.Admin, string? controller = "Management", string? action = "")
        => Category.Reconstitute(id, "Management", "Management", "nav-icon fas fa-cog", controller, action, role, 4);

    /// <summary>The seeded Admin "Menu" sub-category under <see cref="AdminManagement"/>.</summary>
    private static SubCategory AdminMenu(long id = 3, long categoryId = 2, string? role = Role.Admin, string? action = "Menu")
        => SubCategory.Reconstitute(id, categoryId, "Menu", "Menu", "far fa-circle nav-icon", action, role, 3);

    /// <summary>The seeded menu's route to menu management: the Admin Management category and its Menu item.</summary>
    private static MenuSnapshot Seeded() => new([AdminManagement()], [AdminMenu()]);

    /// <summary>Asserts the refusal: a conflict with the policy's code and its English message template (the resx key).</summary>
    private static void AssertRefused(ErrorOr<Success> result)
    {
        Assert.True(result.IsError);
        Assert.Equal("Menu.LastMenuManagementRoute", result.FirstError.Code);
        Assert.Equal(ErrorType.Conflict, result.FirstError.Type);
        Assert.Equal(
            "The menu entry that lets administrators open menu management cannot be removed, moved or given to another role.",
            result.FirstError.Metadata![DomainError.MessageTemplateMetadataKey]);
    }

    // -- Which menus reach Management/Menu --------------------------------------

    /// <summary>The seeded dropdown (Admin category "Management" + Admin item "Menu") is a route.</summary>
    [Fact]
    public void AdminCanReachMenuManagement_ThroughTheSeededDropdown()
        => Assert.True(Seeded().AdminCanReachMenuManagement);

    /// <summary>A single Admin category pointing straight at Management/Menu is a route on its own.</summary>
    [Fact]
    public void AdminCanReachMenuManagement_ThroughASingleCategoryWithTheMenuAction()
        => Assert.True(new MenuSnapshot([AdminManagement(action: "Menu")], []).AdminCanReachMenuManagement);

    /// <summary>
    /// A category with its own action is matched on that action only; its sub-categories are not looked at — the same rule
    /// <c>MenuNavigationMatcher</c> grants access by.
    /// </summary>
    [Fact]
    public void AdminCanReachMenuManagement_IgnoresTheItemsOfACategoryThatHasItsOwnAction()
        => Assert.False(new MenuSnapshot([AdminManagement(action: "Account")], [AdminMenu()]).AdminCanReachMenuManagement);

    /// <summary>The category must belong to the Admin role (exact match, as the filter does).</summary>
    [Theory]
    [InlineData(Role.User)]
    [InlineData(Role.Anonymous)]
    [InlineData(null)]
    public void AdminCanReachMenuManagement_RequiresAnAdminCategory(string? role)
        => Assert.False(new MenuSnapshot([AdminManagement(role: role)], [AdminMenu()]).AdminCanReachMenuManagement);

    /// <summary>The item must belong to the Admin role too.</summary>
    [Theory]
    [InlineData(Role.User)]
    [InlineData(Role.Anonymous)]
    [InlineData(null)]
    public void AdminCanReachMenuManagement_RequiresAnAdminItem(string? role)
        => Assert.False(new MenuSnapshot([AdminManagement()], [AdminMenu(role: role)]).AdminCanReachMenuManagement);

    /// <summary>The category must target the Management controller, matched case-sensitively like the route.</summary>
    [Theory]
    [InlineData("Dashboard")]
    [InlineData("management")]
    [InlineData(null)]
    public void AdminCanReachMenuManagement_RequiresTheManagementController(string? controller)
        => Assert.False(new MenuSnapshot([AdminManagement(controller: controller)], [AdminMenu()]).AdminCanReachMenuManagement);

    /// <summary>The item must target the Menu action, matched case-sensitively.</summary>
    [Theory]
    [InlineData("Account")]
    [InlineData("menu")]
    [InlineData(null)]
    public void AdminCanReachMenuManagement_RequiresTheMenuAction(string? action)
        => Assert.False(new MenuSnapshot([AdminManagement()], [AdminMenu(action: action)]).AdminCanReachMenuManagement);

    /// <summary>The item must hang under that category.</summary>
    [Fact]
    public void AdminCanReachMenuManagement_RequiresTheItemToBelongToTheCategory()
        => Assert.False(new MenuSnapshot([AdminManagement()], [AdminMenu(categoryId: 99)]).AdminCanReachMenuManagement);

    // -- Snapshot edits ---------------------------------------------------------

    /// <summary>Deleting a category also drops its items, as the SubCategory_fk_0 ON DELETE CASCADE does.</summary>
    [Fact]
    public void WithoutCategory_AlsoDropsItsItems()
    {
        MenuSnapshot after = new MenuSnapshot([AdminManagement(), AdminManagement(id: 7, action: "Other")],
            [AdminMenu(), AdminMenu(id: 8, categoryId: 7)]).WithoutCategory(2);

        Assert.Equal([7L], after.Categories.Select(c => c.Id));
        Assert.Equal([8L], after.SubCategories.Select(s => s.Id));
    }

    /// <summary>A replaced category takes the place of the one with its id.</summary>
    [Fact]
    public void WithCategory_ReplacesTheCategoryWithTheSameId()
    {
        MenuSnapshot after = Seeded().WithCategory(AdminManagement(role: Role.User));

        Assert.Equal(Role.User, Assert.Single(after.Categories).Role);
        Assert.False(after.AdminCanReachMenuManagement);
    }

    /// <summary>A replaced item takes the place of the one with its id.</summary>
    [Fact]
    public void WithSubCategory_ReplacesTheItemWithTheSameId()
    {
        MenuSnapshot after = Seeded().WithSubCategory(AdminMenu(action: "Account"));

        Assert.Equal("Account", Assert.Single(after.SubCategories).Action);
        Assert.False(after.AdminCanReachMenuManagement);
    }

    /// <summary>A deleted item leaves the snapshot.</summary>
    [Fact]
    public void WithoutSubCategory_DropsTheItem()
        => Assert.Empty(Seeded().WithoutSubCategory(3).SubCategories);

    /// <summary>The edits return a new snapshot and leave the original as it was.</summary>
    [Fact]
    public void Edits_LeaveTheOriginalSnapshotUnchanged()
    {
        MenuSnapshot before = Seeded();

        _ = before.WithoutCategory(2);
        _ = before.WithoutSubCategory(3);
        _ = before.WithCategory(AdminManagement(role: Role.User));
        _ = before.WithSubCategory(AdminMenu(role: Role.User));

        Assert.True(before.AdminCanReachMenuManagement);
    }

    /// <summary>
    /// The verdict is taken with the snapshot: <see cref="Category.Update"/> / <see cref="SubCategory.Update"/> change the
    /// object in place, so a service that updates a row the "before" snapshot also holds must still see "before" as it
    /// was — or the change would be judged against itself and always let through.
    /// </summary>
    [Fact]
    public void AdminCanReachMenuManagement_IsFixedWhenTheSnapshotIsTaken()
    {
        Category management = AdminManagement();
        SubCategory menu = AdminMenu();
        MenuSnapshot before = new([management], [menu]);

        _ = management.Update("Management", "Management", "nav-icon fas fa-cog", "Management", "", Role.User, 4);
        _ = menu.Update(2, "Menu", "Menu", "far fa-circle nav-icon", "Account", Role.Admin, 3);

        Assert.True(before.AdminCanReachMenuManagement);
        AssertRefused(MenuSafeguardPolicy.CheckChange(before, before.WithCategory(management)));
    }

    // -- CheckChange ------------------------------------------------------------

    /// <summary>Every way of taking the last route away is refused.</summary>
    [Fact]
    public void CheckChange_RefusesTakingTheLastRouteAway()
    {
        MenuSnapshot before = Seeded();

        AssertRefused(MenuSafeguardPolicy.CheckChange(before, before.WithoutCategory(2)));
        AssertRefused(MenuSafeguardPolicy.CheckChange(before, before.WithoutSubCategory(3)));
        AssertRefused(MenuSafeguardPolicy.CheckChange(before, before.WithCategory(AdminManagement(role: Role.User))));
        AssertRefused(MenuSafeguardPolicy.CheckChange(before, before.WithCategory(AdminManagement(controller: "Dashboard"))));
        AssertRefused(MenuSafeguardPolicy.CheckChange(before, before.WithSubCategory(AdminMenu(role: Role.User))));
        AssertRefused(MenuSafeguardPolicy.CheckChange(before, before.WithSubCategory(AdminMenu(categoryId: 99))));
    }

    /// <summary>One of two routes may go: the other still reaches menu management.</summary>
    [Fact]
    public void CheckChange_AllowsRemovingARouteWhenAnotherRemains()
    {
        MenuSnapshot before = new([AdminManagement(), AdminManagement(id: 7, action: "Menu")], [AdminMenu()]);

        Assert.False(MenuSafeguardPolicy.CheckChange(before, before.WithoutCategory(2)).IsError);
    }

    /// <summary>A change that keeps the route (renaming, reordering, re-iconing it) is allowed.</summary>
    [Fact]
    public void CheckChange_AllowsAChangeThatKeepsTheRoute()
    {
        MenuSnapshot before = Seeded();
        SubCategory renamed = SubCategory.Reconstitute(3, 2, "Menus", "Menu settings", "fas fa-bars", "Menu", Role.Admin, 9);

        Assert.False(MenuSafeguardPolicy.CheckChange(before, before.WithSubCategory(renamed)).IsError);
    }

    /// <summary>
    /// A menu that already had no route is not held hostage: changes to it are allowed (the guard protects a route, it
    /// does not demand one).
    /// </summary>
    [Fact]
    public void CheckChange_AllowsChangesWhenThereWasNoRouteToBeginWith()
    {
        MenuSnapshot before = new([AdminManagement(role: Role.User)], [AdminMenu()]);

        Assert.False(MenuSafeguardPolicy.CheckChange(before, before.WithoutSubCategory(3)).IsError);
    }

    // -- With… edits replace only the row they name --------------------------------

    /// <summary>Editing another category replaces that category only: the Management category, and with it the route, stay.</summary>
    [Fact]
    public void WithCategory_ReplacesOnlyTheCategoryWithItsId()
    {
        Category dashboard = Category.Reconstitute(1, "Dashboard", "Dashboard", "nav-icon fas fa-tachometer-alt", "Dashboard", "AdminIndex", Role.Admin, 1);
        MenuSnapshot before = new([dashboard, AdminManagement()], [AdminMenu()]);
        Category renamed = Category.Reconstitute(1, "Home", "Home", "nav-icon fas fa-home", "Dashboard", "AdminIndex", Role.Admin, 1);

        MenuSnapshot after = before.WithCategory(renamed);

        Assert.Equal(["Home", "Management"], after.Categories.Select(c => c.Name));
        Assert.True(after.AdminCanReachMenuManagement);
    }

    /// <summary>Editing another menu item replaces that item only: the Menu item, and with it the route, stay.</summary>
    [Fact]
    public void WithSubCategory_ReplacesOnlyTheSubCategoryWithItsId()
    {
        SubCategory account = SubCategory.Reconstitute(4, 2, "Account", "Account", "far fa-circle nav-icon", "Account", Role.Admin, 1);
        MenuSnapshot before = new([AdminManagement()], [account, AdminMenu()]);
        SubCategory renamed = SubCategory.Reconstitute(4, 2, "Accounts", "Accounts", "far fa-circle nav-icon", "Account", Role.Admin, 1);

        MenuSnapshot after = before.WithSubCategory(renamed);

        Assert.Equal(["Accounts", "Menu"], after.SubCategories.Select(s => s.Name));
        Assert.True(after.AdminCanReachMenuManagement);
    }
}

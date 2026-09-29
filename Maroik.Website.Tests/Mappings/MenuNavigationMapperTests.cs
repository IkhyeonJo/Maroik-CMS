using Maroik.Core.Contract.Dtos;
using Maroik.Website.Mappings;

namespace Maroik.Website.Tests.Mappings;

/// <summary>
/// Unit tests for <see cref="MenuNavigationMapper"/> — maps navigation Category/SubCategory DTOs
/// into the sorted, active-flagged sidebar view model. Matching-algorithm tests live in
/// <see cref="Maroik.Website.Tests.Extensions.MenuNavigationMatcherTests"/>.
/// </summary>
public class MenuNavigationMapperTests
{
    /// <summary>A category that is itself a link (it has an action).</summary>
    private static CategoryResponse SingleCategory(long id, string controller, string action, long order = 0) => new()
    {
        Id = id,
        DisplayName = $"Category{id}",
        Controller = controller,
        Action = action,
        Order = order
    };

    /// <summary>A category that only groups sub-categories (no action).</summary>
    private static CategoryResponse ParentCategory(long id, string controller, long order = 0) => new()
    {
        Id = id,
        DisplayName = $"Category{id}",
        Controller = controller,
        Action = null,
        Order = order
    };

    /// <summary>A sub-category of <paramref name="categoryId"/> linking to <paramref name="action"/>.</summary>
    private static SubCategoryResponse SubCategory(long id, long categoryId, string action, long order = 0) => new()
    {
        Id = id,
        CategoryId = categoryId,
        DisplayName = $"SubCategory{id}",
        Action = action,
        Order = order
    };

    // -- ToSidebarMenu -------------------------------------------------------------

    /// <summary>To sidebar menu sorts categories by order.</summary>
    [Fact]
    public void ToSidebarMenu_SortsCategoriesByOrder()
    {
        var categories = new[]
        {
            SingleCategory(1, "Second", "Index", order: 2),
            SingleCategory(2, "First", "Index", order: 1)
        };

        var menu = categories.ToSidebarMenu([], "None", "None");

        Assert.Equal(2, menu.Categories[0].Category.Id);
        Assert.Equal(1, menu.Categories[1].Category.Id);
    }

    /// <summary>To sidebar menu single category is active when controller and action match.</summary>
    [Fact]
    public void ToSidebarMenu_SingleCategory_IsActiveWhenControllerAndActionMatch()
    {
        var categories = new[] { SingleCategory(1, "Dashboard", "UserIndex") };

        var menu = categories.ToSidebarMenu([], "Dashboard", "UserIndex");

        Assert.True(menu.Categories[0].IsActive);
        Assert.Empty(menu.Categories[0].SubCategories);
    }

    /// <summary>To sidebar menu single category not active when route differs.</summary>
    [Fact]
    public void ToSidebarMenu_SingleCategory_NotActiveWhenRouteDiffers()
    {
        var categories = new[] { SingleCategory(1, "Dashboard", "UserIndex") };

        var menu = categories.ToSidebarMenu([], "Forum", "FreeForum");

        Assert.False(menu.Categories[0].IsActive);
    }

    /// <summary>To sidebar menu parent category is active when any child matches.</summary>
    [Fact]
    public void ToSidebarMenu_ParentCategory_IsActiveWhenAnyChildMatches()
    {
        var categories = new[] { ParentCategory(1, "AccountBook") };
        var subCategories = new[]
        {
            SubCategory(10, categoryId: 1, action: "Income", order: 1),
            SubCategory(11, categoryId: 1, action: "Expenditure", order: 2)
        };

        var menu = categories.ToSidebarMenu(subCategories, "AccountBook", "Expenditure");

        Assert.True(menu.Categories[0].IsActive);
        Assert.False(menu.Categories[0].SubCategories.Single(s => s.SubCategory.Id == 10).IsActive);
        Assert.True(menu.Categories[0].SubCategories.Single(s => s.SubCategory.Id == 11).IsActive);
    }

    /// <summary>To sidebar menu parent category not active when no child matches.</summary>
    [Fact]
    public void ToSidebarMenu_ParentCategory_NotActiveWhenNoChildMatches()
    {
        var categories = new[] { ParentCategory(1, "AccountBook") };
        var subCategories = new[] { SubCategory(10, categoryId: 1, action: "Income") };

        var menu = categories.ToSidebarMenu(subCategories, "AccountBook", "Asset");

        Assert.False(menu.Categories[0].IsActive);
        Assert.All(menu.Categories[0].SubCategories, sub => Assert.False(sub.IsActive));
    }

    /// <summary>To sidebar menu sub categories sorted by order.</summary>
    [Fact]
    public void ToSidebarMenu_SubCategories_SortedByOrder()
    {
        var categories = new[] { ParentCategory(1, "AccountBook") };
        var subCategories = new[]
        {
            SubCategory(10, categoryId: 1, action: "Second", order: 2),
            SubCategory(11, categoryId: 1, action: "First", order: 1)
        };

        var menu = categories.ToSidebarMenu(subCategories, "None", "None");

        Assert.Equal(11, menu.Categories[0].SubCategories[0].SubCategory.Id);
        Assert.Equal(10, menu.Categories[0].SubCategories[1].SubCategory.Id);
    }
}

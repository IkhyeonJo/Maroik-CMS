using Maroik.Core.Contract.Dtos;
using Maroik.Website.Extensions;

namespace Maroik.Website.Tests.Extensions;

/// <summary>
/// Unit tests for <see cref="MenuNavigationMatcher"/> — the single source of truth for
/// resolving which navigation Category/SubCategory matches the current controller/action.
/// This logic used to be triplicated (Admin/User/Anonymous) across <c>AuthorizationFilter</c>,
/// <c>_MainLeftSideBar.cshtml</c>, and <c>_BodyPageHeader.cshtml</c>.
/// </summary>
public class MenuNavigationMatcherTests
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

    /// <summary>Resolve active menu item single category matches returns match with no sub category.</summary>
    [Fact]
    public void ResolveActiveMenuItem_SingleCategoryMatches_ReturnsMatchWithNoSubCategory()
    {
        var categories = new[] { SingleCategory(1, "Dashboard", "UserIndex") };

        var match = categories.ResolveActiveMenuItem([], "Dashboard", "UserIndex");

        Assert.True(match.IsMatch);
        Assert.Equal(1, match.Category!.Id);
        Assert.Null(match.SubCategory);
    }

    /// <summary>Resolve active menu item sub category matches returns match with both category and sub category.</summary>
    [Fact]
    public void ResolveActiveMenuItem_SubCategoryMatches_ReturnsMatchWithBothCategoryAndSubCategory()
    {
        var categories = new[] { ParentCategory(1, "AccountBook") };
        var subCategories = new[] { SubCategory(10, categoryId: 1, action: "Income") };

        var match = categories.ResolveActiveMenuItem(subCategories, "AccountBook", "Income");

        Assert.True(match.IsMatch);
        Assert.Equal(1, match.Category!.Id);
        Assert.Equal(10, match.SubCategory!.Id);
    }

    /// <summary>Resolve active menu item no match returns is match false.</summary>
    [Fact]
    public void ResolveActiveMenuItem_NoMatch_ReturnsIsMatchFalse()
    {
        var categories = new[] { SingleCategory(1, "Dashboard", "UserIndex") };

        var match = categories.ResolveActiveMenuItem([], "Forum", "FreeForum");

        Assert.False(match.IsMatch);
        Assert.Null(match.Category);
        Assert.Null(match.SubCategory);
    }

    /// <summary>Resolve active menu item sub category belongs to different category does not match.</summary>
    [Fact]
    public void ResolveActiveMenuItem_SubCategoryBelongsToDifferentCategory_DoesNotMatch()
    {
        var categories = new[] { ParentCategory(1, "AccountBook") };
        // SubCategory's CategoryId (2) does not match the parent category's Id (1).
        var subCategories = new[] { SubCategory(10, categoryId: 2, action: "Income") };

        var match = categories.ResolveActiveMenuItem(subCategories, "AccountBook", "Income");

        Assert.False(match.IsMatch);
    }

    /// <summary>Resolve active menu item action matches but controller does not does not match.</summary>
    [Fact]
    public void ResolveActiveMenuItem_ActionMatchesButControllerDoesNot_DoesNotMatch()
    {
        var categories = new[] { SingleCategory(1, "Dashboard", "UserIndex") };

        var match = categories.ResolveActiveMenuItem([], "Forum", "UserIndex");

        Assert.False(match.IsMatch);
    }

    /// <summary>Resolve active menu item empty categories returns is match false.</summary>
    [Fact]
    public void ResolveActiveMenuItem_EmptyCategories_ReturnsIsMatchFalse()
    {
        var match = Array.Empty<CategoryResponse>().ResolveActiveMenuItem([], "Dashboard", "UserIndex");

        Assert.False(match.IsMatch);
    }
}

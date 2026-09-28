using Maroik.Core.Domain.Menu;
namespace Maroik.Core.Domain.Tests.Menu;

/// <summary>
/// Unit tests for <see cref="SubCategory"/>.
/// Covers creation validation (name empty guard) and reconstitution from trusted data.
/// </summary>
public class SubCategoryTests
{
    // -- Create ---------------------------------------------------------------

    /// <summary>Create returns sub category, when valid.</summary>
    [Fact]
    public void Create_ReturnsSubCategory_WhenValid()
    {
        var result = SubCategory.Create(10, "FreeForum", "Free Forum", "/icons/forum.svg", "List", "User", 1);

        Assert.False(result.IsError);
        Assert.Equal(10, result.Value.CategoryId);
        Assert.Equal("FreeForum", result.Value.Name);
        Assert.Equal("List", result.Value.Action);
        Assert.Equal("User", result.Value.Role);
        Assert.Equal(1, result.Value.Order);
    }

    /// <summary>Create always assigns id 0 — the repository/database assigns the real identity on insert.</summary>
    [Fact]
    public void Create_AlwaysAssignsIdZero()
    {
        var result = SubCategory.Create(10, "FreeForum", "Free Forum", "/icons/forum.svg", "List", "User", 1);

        Assert.False(result.IsError);
        Assert.Equal(0, result.Value.Id);
    }

    /// <summary>Create returns error, when name empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ReturnsError_WhenNameEmpty(string? name)
    {
        var result = SubCategory.Create(10, name, "Free Forum", null, "List", "User", 1);

        Assert.True(result.IsError);
        Assert.Equal("SubCategory.NameEmpty", result.FirstError.Code);
    }

    // -- Reconstitute -----------------------------------------------------------

    /// <summary>Reconstitute rebuilds sub category without validation.</summary>
    [Fact]
    public void Reconstitute_RebuildsSubCategory_WithoutValidation()
    {
        var subCategory = SubCategory.Reconstitute(5, 10, "Notice", "Notice", "/icons/notice.svg", "List", "Admin", 2);

        Assert.Equal(5, subCategory.Id);
        Assert.Equal(10, subCategory.CategoryId);
        Assert.Equal("Notice", subCategory.Name);
        Assert.Equal("Notice", subCategory.DisplayName);
        Assert.Equal("/icons/notice.svg", subCategory.IconPath);
        Assert.Equal("List", subCategory.Action);
        Assert.Equal("Admin", subCategory.Role);
        Assert.Equal(2, subCategory.Order);
    }

    /// <summary>Reconstitute allows null name.</summary>
    [Fact]
    public void Reconstitute_AllowsNullName()
    {
        var subCategory = SubCategory.Reconstitute(1, 10, null, null, null, null, null, 0);

        Assert.Null(subCategory.Name);
    }

    // -- Update -----------------------------------------------------------------

    /// <summary>Update replaces all fields, when valid.</summary>
    [Fact]
    public void Update_ReplacesAllFields_WhenValid()
    {
        var subCategory = SubCategory.Reconstitute(1, 10, "Old", "Old", "/old.svg", "Old", "User", 1);

        var result = subCategory.Update(20, "New", "New", "/new.svg", "New", "Admin", 2);

        Assert.False(result.IsError);
        Assert.Equal(20, subCategory.CategoryId);
        Assert.Equal("New", subCategory.Name);
        Assert.Equal("New", subCategory.DisplayName);
        Assert.Equal("/new.svg", subCategory.IconPath);
        Assert.Equal("New", subCategory.Action);
        Assert.Equal("Admin", subCategory.Role);
        Assert.Equal(2, subCategory.Order);
    }

    /// <summary>Update returns error and leaves fields unchanged, when name empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_ReturnsError_WhenNameEmpty(string? name)
    {
        var subCategory = SubCategory.Reconstitute(1, 10, "Old", "Old", "/old.svg", "Old", "User", 1);

        var result = subCategory.Update(20, name, "New", "/new.svg", "New", "Admin", 2);

        Assert.True(result.IsError);
        Assert.Equal("SubCategory.NameEmpty", result.FirstError.Code);
        Assert.Equal("Old", subCategory.Name);
        Assert.Equal(10, subCategory.CategoryId);
    }
}

using Maroik.Core.Domain.Menu;
namespace Maroik.Core.Domain.Tests.Menu;

/// <summary>
/// Unit tests for <see cref="MenuFieldPolicy"/> and its application in <see cref="Category"/> and
/// <see cref="SubCategory"/>: the length / role / order rules that mirror the database constraints, and
/// the leniency that keeps missing values working as before.
/// </summary>
public class MenuFieldPolicyTests
{
    private static readonly string _tooLong = new('x', 256);

    // -- Validate ---------------------------------------------------------------

    /// <summary>Accepts values the database accepts, including missing optional text and a missing role.</summary>
    [Fact]
    public void Validate_AcceptsValidAndMissingValues()
    {
        Assert.False(MenuFieldPolicy.Validate("Home", "Home", "/i.svg", "Home", "Index", "User", 0).IsError);
        Assert.False(MenuFieldPolicy.Validate("Home", null, null, null, null, null, 3).IsError);
        Assert.False(MenuFieldPolicy.Validate(new string('x', 255), new string('x', 255), new string('x', 255), new string('x', 255), new string('x', 255), "Admin", 1).IsError);
    }

    /// <summary>Every text field over the 255-character column width is rejected, naming the field.</summary>
    [Theory]
    [InlineData("Name")]
    [InlineData("DisplayName")]
    [InlineData("IconPath")]
    [InlineData("Controller")]
    [InlineData("Action")]
    public void Validate_RejectsOverlongText(string field)
    {
        string name = field == "Name" ? _tooLong : "n";
        string display = field == "DisplayName" ? _tooLong : "d";
        string icon = field == "IconPath" ? _tooLong : "i";
        string controller = field == "Controller" ? _tooLong : "c";
        string action = field == "Action" ? _tooLong : "a";

        var result = MenuFieldPolicy.Validate(name, display, icon, controller, action, "User", 1);

        Assert.True(result.IsError);
        Assert.Equal("Menu.FieldTooLong", result.FirstError.Code);
        Assert.Contains(field, result.FirstError.Description);
    }

    /// <summary>Only Admin / User / Anonymous (exact case) are accepted as a role.</summary>
    [Theory]
    [InlineData("Guest")]
    [InlineData("admin")]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_RejectsUnknownRole(string role)
    {
        var result = MenuFieldPolicy.Validate("n", "d", "i", "c", "a", role, 1);

        Assert.True(result.IsError);
        Assert.Equal("Menu.RoleInvalid", result.FirstError.Code);
    }

    /// <summary>A negative order is rejected (mirrors the *_Order_check constraint).</summary>
    [Fact]
    public void Validate_RejectsNegativeOrder()
    {
        var result = MenuFieldPolicy.Validate("n", "d", "i", "c", "a", "User", -1);

        Assert.True(result.IsError);
        Assert.Equal("Menu.OrderNegative", result.FirstError.Code);
    }

    /// <summary>The role taxonomy holds exactly the three values the database allows.</summary>
    [Fact]
    public void Roles_AreTheThreeDatabaseValues()
 #pragma warning disable CA1861
        => Assert.Equal(new[] { "Admin", "Anonymous", "User" }, MenuFieldPolicy.Roles.All.OrderBy(r => r).ToArray());
 #pragma warning restore CA1861

    // -- Category / SubCategory apply it -----------------------------------------

    /// <summary>Category.Create and Update reject a rule violation and leave an existing category untouched.</summary>
    [Fact]
    public void Category_RejectsInvalidFields_OnCreateAndUpdate()
    {
        Assert.Equal("Menu.RoleInvalid", Category.Create("n", "d", "i", "c", "a", "Guest", 1).FirstError.Code);
        Assert.Equal("Menu.OrderNegative", Category.Create("n", "d", "i", "c", "a", "User", -5).FirstError.Code);
        Assert.Equal("Menu.FieldTooLong", Category.Create("n", _tooLong, "i", "c", "a", "User", 1).FirstError.Code);

        var category = Category.Reconstitute(1, "Old", "Old", "/i.svg", "Home", "Index", "User", 2);
        var update = category.Update("New", "New", "/i.svg", "Home", "Index", "Guest", 9);

        Assert.True(update.IsError);
        Assert.Equal("Menu.RoleInvalid", update.FirstError.Code);
        Assert.Equal("Old", category.Name);
        Assert.Equal("User", category.Role);
        Assert.Equal(2, category.Order);
    }

    /// <summary>SubCategory.Create and Update reject a rule violation and leave an existing sub-category untouched.</summary>
    [Fact]
    public void SubCategory_RejectsInvalidFields_OnCreateAndUpdate()
    {
        Assert.Equal("Menu.RoleInvalid", SubCategory.Create(10, "n", "d", "i", "a", "Guest", 1).FirstError.Code);
        Assert.Equal("Menu.OrderNegative", SubCategory.Create(10, "n", "d", "i", "a", "User", -5).FirstError.Code);
        Assert.Equal("Menu.FieldTooLong", SubCategory.Create(10, "n", "d", "i", _tooLong, "User", 1).FirstError.Code);

        var sub = SubCategory.Reconstitute(1, 10, "Old", "Old", "/i.svg", "List", "User", 2);
        var update = sub.Update(10, "New", "New", "/i.svg", "List", "Guest", 9);

        Assert.True(update.IsError);
        Assert.Equal("Menu.RoleInvalid", update.FirstError.Code);
        Assert.Equal("Old", sub.Name);
        Assert.Equal("User", sub.Role);
        Assert.Equal(2, sub.Order);
    }

    /// <summary>A missing role is still accepted (the repository stores it as the restrictive Admin).</summary>
    [Fact]
    public void Category_AcceptsMissingRole()
        => Assert.False(Category.Create("n", "d", "i", "c", "a", null, 1).IsError);
}

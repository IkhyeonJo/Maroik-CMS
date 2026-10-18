using System.Xml.Linq;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Accounts;
using Maroik.Core.Domain.Menus;
using Maroik.Website.Controllers;
using Maroik.Website.Extensions;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Ties <see cref="MenuSafeguardPolicy"/> (Domain) to the Website it protects: the route it keeps is the real menu-management
/// action, it judges a menu exactly as <see cref="MenuNavigationMatcher"/> grants access by, and its refusal is translated by
/// <see cref="ManagementController"/>'s resx pair. No host or database: these read the types and the resx files directly.
/// </summary>
public class MenuSafeguardTests
{
    /// <summary>The refusal's English template — the resx key <see cref="ManagementController"/> looks it up under.</summary>
    private const string RefusalKey =
        "The menu entry that lets administrators open menu management cannot be removed, moved or given to another role.";

    /// <summary>The Maroik.Website project folder.</summary>
    private static readonly string _websiteDir = FindWebsiteDirectory();

    /// <summary>Walks up from the test output to the folder holding <c>Maroik.sln</c>, then into Maroik.Website.</summary>
    private static string FindWebsiteDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Maroik.sln"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Could not locate Maroik.sln"), "Maroik.Website");
    }

    /// <summary>The route the safeguard keeps is <c>ManagementController.Menu</c>: renaming either breaks this test, not the guard.</summary>
    [Fact]
    public void TheGuardedRoute_IsTheMenuManagementAction()
    {
        Assert.Equal(MenuSafeguardPolicy.MenuManagementController, nameof(ManagementController)[..^"Controller".Length]);
        Assert.Equal(nameof(ManagementController.Menu), MenuSafeguardPolicy.MenuManagementAction);
    }

    /// <summary>One menu, as a domain snapshot and as the DTO lists the filter matches against.</summary>
    public sealed record MenuCase(string Name, (long Id, string? Controller, string? Action, string? Role)[] Categories,
        (long Id, long CategoryId, string? Action, string? Role)[] SubCategories)
    {
        /// <inheritdoc />
        public override string ToString() => Name;
    }

    /// <summary>Menus that do and do not reach Management/Menu, covering each part of the matching rule.</summary>
    public static TheoryData<MenuCase> Menus() =>
    [
        new MenuCase("seeded dropdown", [(2, "Management", "", Role.Admin)], [(3, 2, "Menu", Role.Admin)]),
        new MenuCase("single category", [(2, "Management", "Menu", Role.Admin)], []),
        new MenuCase("category with its own other action", [(2, "Management", "Account", Role.Admin)], [(3, 2, "Menu", Role.Admin)]),
        new MenuCase("User category", [(2, "Management", "", Role.User)], [(3, 2, "Menu", Role.Admin)]),
        new MenuCase("User item", [(2, "Management", "", Role.Admin)], [(3, 2, "Menu", Role.User)]),
        new MenuCase("other controller", [(2, "Dashboard", "", Role.Admin)], [(3, 2, "Menu", Role.Admin)]),
        new MenuCase("other action", [(2, "Management", "", Role.Admin)], [(3, 2, "Account", Role.Admin)]),
        new MenuCase("lower-case action", [(2, "Management", "", Role.Admin)], [(3, 2, "menu", Role.Admin)]),
        new MenuCase("item under another category", [(2, "Management", "", Role.Admin), (5, "Dashboard", "", Role.Admin)], [(3, 5, "Menu", Role.Admin)]),
        new MenuCase("second route only", [(2, "Management", "", Role.User), (7, "Management", "Menu", Role.Admin)], [(3, 2, "Menu", Role.Admin)]),
    ];

    /// <summary>
    /// The safeguard judges a menu the way access is granted: <see cref="MenuSnapshot.AdminCanReachMenuManagement"/> agrees with
    /// <see cref="MenuNavigationMatcher"/> run over the Admin rows (as <c>AuthorizationFilter</c> splits them by role) for an Admin
    /// GET of Management/Menu. If the two rules drift apart, the guard protects a route the filter no longer uses.
    /// </summary>
    [Theory]
    [MemberData(nameof(Menus))]
    public void TheSnapshot_AgreesWithTheNavigationMatcher(MenuCase menu)
    {
        var snapshot = new MenuSnapshot(
            [.. menu.Categories.Select(c => Category.Reconstitute(c.Id, "n", "n", "i", c.Controller, c.Action, c.Role, 0))],
            [.. menu.SubCategories.Select(s => SubCategory.Reconstitute(s.Id, s.CategoryId, "n", "n", "i", s.Action, s.Role, 0))]);
        List<CategoryResponse> adminCategories =
        [
            .. menu.Categories.Where(c => c.Role == Role.Admin)
                .Select(c => new CategoryResponse { Id = c.Id, Controller = c.Controller, Action = c.Action, Role = c.Role })
        ];
        List<SubCategoryResponse> adminSubCategories =
        [
            .. menu.SubCategories.Where(s => s.Role == Role.Admin)
                .Select(s => new SubCategoryResponse { Id = s.Id, CategoryId = s.CategoryId, Action = s.Action, Role = s.Role })
        ];

        bool granted = adminCategories.ResolveActiveMenuItem(adminSubCategories, "Management", "Menu").IsMatch;

        Assert.Equal(granted, snapshot.AdminCanReachMenuManagement);
    }

    /// <summary>The refusal is translated by <see cref="ManagementController"/>'s resx pair, in both cultures.</summary>
    [Theory]
    [InlineData("en-US", RefusalKey)]
    [InlineData("ko-KR", "관리자가 메뉴 관리에 들어가는 메뉴 항목은 삭제하거나, 옮기거나, 다른 역할로 바꿀 수 없습니다.")]
    public void TheRefusal_IsLocalized(string culture, string expected)
    {
        string path = Path.Combine(_websiteDir, "Resources", "Controllers", $"ManagementController.{culture}.resx");

        string? value = XDocument.Load(path).Root!.Elements("data")
            .Where(d => (string?)d.Attribute("name") == RefusalKey)
            .Select(d => (string?)d.Element("value"))
            .SingleOrDefault();

        Assert.Equal(expected, value);
    }
}

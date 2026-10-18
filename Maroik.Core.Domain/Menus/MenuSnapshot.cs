
namespace Maroik.Core.Domain.Menus;

/// <summary>
/// The whole navigation menu at one moment — every category and sub-category, of every role — as
/// <see cref="MenuSafeguardPolicy"/> judges it. The <c>With…</c> / <c>Without…</c> edits return a new snapshot and leave
/// this one untouched, so a service can describe "the menu after this change" next to "the menu before it".
/// </summary>
public sealed class MenuSnapshot(IReadOnlyList<Category> categories, IReadOnlyList<SubCategory> subCategories)
{
    /// <summary>Every category in the menu.</summary>
    public IReadOnlyList<Category> Categories { get; } = categories;

    /// <summary>Every sub-category in the menu.</summary>
    public IReadOnlyList<SubCategory> SubCategories { get; } = subCategories;

    /// <summary>
    /// True when an Admin GET of <c>Management/Menu</c> resolves to a menu item — the rule <c>MenuNavigationMatcher</c>
    /// grants access by, over the Admin rows only (roles match exactly, as <c>AuthorizationFilter</c> splits them): an
    /// Admin category on the Management controller that either names the Menu action itself, or has no action of its own
    /// and an Admin sub-category under it naming the Menu action. A category with its own action is matched on that
    /// action alone; its sub-categories are not looked at.
    /// <para>
    /// Worked out once, when the snapshot is taken: <see cref="Category.Update"/> / <see cref="SubCategory.Update"/>
    /// change the row in place, and a service updating a row its "before" snapshot also holds must not see "before" change
    /// with it — the change would be judged against itself and always let through.
    /// </para>
    /// </summary>
    public bool AdminCanReachMenuManagement { get; } =
        categories.Any(category =>
            category.Role == Accounts.Role.Admin
            && category.Controller == MenuSafeguardPolicy.MenuManagementController
            && (string.IsNullOrEmpty(category.Action)
                ? subCategories.Any(sub =>
                    sub.CategoryId == category.Id
                    && sub.Role == Accounts.Role.Admin
                    && sub.Action == MenuSafeguardPolicy.MenuManagementAction)
                : category.Action == MenuSafeguardPolicy.MenuManagementAction));

    /// <summary>This menu with <paramref name="updated"/> in place of the category that has its id.</summary>
    public MenuSnapshot WithCategory(Category updated) =>
        new([.. Categories.Select(c => c.Id == updated.Id ? updated : c)], SubCategories);

    /// <summary>
    /// This menu without the category <paramref name="id"/> and without its sub-categories — deleting a category deletes
    /// its items too (<c>SubCategory_fk_0</c> is <c>ON DELETE CASCADE</c>).
    /// </summary>
    public MenuSnapshot WithoutCategory(long id) =>
        new([.. Categories.Where(c => c.Id != id)], [.. SubCategories.Where(s => s.CategoryId != id)]);

    /// <summary>This menu with <paramref name="updated"/> in place of the sub-category that has its id.</summary>
    public MenuSnapshot WithSubCategory(SubCategory updated) =>
        new(Categories, [.. SubCategories.Select(s => s.Id == updated.Id ? updated : s)]);

    /// <summary>This menu without the sub-category <paramref name="id"/>.</summary>
    public MenuSnapshot WithoutSubCategory(long id) =>
        new(Categories, [.. SubCategories.Where(s => s.Id != id)]);
}

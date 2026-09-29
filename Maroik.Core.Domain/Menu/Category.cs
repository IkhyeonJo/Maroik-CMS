using ErrorOr;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.Menu;

/// <summary>Top-level sidebar navigation menu item.</summary>
public sealed class Category : AggregateRoot<long>
{
    /// <summary>Internal code name (e.g. "Dashboard").</summary>
    public string? Name { get; private set; }

    /// <summary>Label shown in the sidebar navigation.</summary>
    public string? DisplayName { get; private set; }

    /// <summary>CSS icon classes rendered beside the label (e.g. "nav-icon fas fa-bell").</summary>
    public string? IconPath { get; private set; }

    /// <summary>Target MVC controller name.</summary>
    public string? Controller { get; private set; }

    /// <summary>Target MVC action name.</summary>
    public string? Action { get; private set; }

    /// <summary>The one role whose sidebar shows this menu item ("Admin", "User" or "Anonymous"; matched exactly, not as a hierarchy).</summary>
    public string? Role { get; private set; }

    /// <summary>Sidebar display order (ascending).</summary>
    public long Order { get; private set; }

    /// <summary>Sets every field; reached only through <see cref="Reconstitute"/> / <see cref="Create"/>.</summary>
    private Category(
        long id,
        string? name,
        string? displayName,
        string? iconPath,
        string? controller,
        string? action,
        string? role,
        long order) : base(id)
    {
        Name = name;
        DisplayName = displayName;
        IconPath = iconPath;
        Controller = controller;
        Action = action;
        Role = role;
        Order = order;
    }

    /// <summary>
    /// Rebuilds a <see cref="Category"/> from trusted repository data.
    /// Bypasses validation — use only with data from the trusted repository layer.
    /// </summary>
    public static Category Reconstitute(
        long id, string? name, string? displayName, string? iconPath,
        string? controller, string? action, string? role, long order)
        => new(id, name, displayName, iconPath, controller, action, role, order);

    /// <summary>Creates a new navigation category after validating required fields.</summary>
    public static ErrorOr<Category> Create(
        string? name, string? displayName, string? iconPath,
        string? controller, string? action, string? role, long order)
    {
        if (string.IsNullOrWhiteSpace(name))
            return LocalizableError.Validation("Category.NameEmpty", "Category name cannot be empty.");

        var fieldsResult = MenuFieldPolicy.Validate(name, displayName, iconPath, controller, action, role, order);
        if (fieldsResult.IsError) return fieldsResult.Errors;

        return new Category(0, name, displayName, iconPath, controller, action, role, order);
    }

    /// <summary>Updates this category's fields after validating the name and every field against <see cref="MenuFieldPolicy"/>.</summary>
    public ErrorOr<Success> Update(
        string? name, string? displayName, string? iconPath,
        string? controller, string? action, string? role, long order)
    {
        if (string.IsNullOrWhiteSpace(name))
            return LocalizableError.Validation("Category.NameEmpty", "Category name cannot be empty.");

        var fieldsResult = MenuFieldPolicy.Validate(name, displayName, iconPath, controller, action, role, order);
        if (fieldsResult.IsError) return fieldsResult.Errors;

        Name = name;
        DisplayName = displayName;
        IconPath = iconPath;
        Controller = controller;
        Action = action;
        Role = role;
        Order = order;
        return Result.Success;
    }
}

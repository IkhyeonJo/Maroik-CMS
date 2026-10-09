using ErrorOr;
using Maroik.Core.Domain.Errors;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.Menu;

/// <summary>Sidebar dropdown sub-menu item belonging to a <see cref="Category"/>.</summary>
public sealed class SubCategory : AggregateRoot<long>
{
    /// <summary>ID of the parent category.</summary>
    public long CategoryId { get; private set; }

    /// <summary>Internal code name (e.g. "FreeForum").</summary>
    public string? Name { get; private set; }

    /// <summary>Label displayed in the sidebar dropdown.</summary>
    public string? DisplayName { get; private set; }

    /// <summary>CSS icon classes rendered beside the label.</summary>
    public string? IconPath { get; private set; }

    /// <summary>Target MVC action name (shares the parent category's controller).</summary>
    public string? Action { get; private set; }

    /// <summary>The one role whose sidebar shows this sub-menu item ("Admin", "User" or "Anonymous"; matched exactly, not as a hierarchy).</summary>
    public string? Role { get; private set; }

    /// <summary>Display order within the parent category (ascending).</summary>
    public long Order { get; private set; }

    /// <summary>Sets every field; reached only through <see cref="Reconstitute"/> / <see cref="Create"/>.</summary>
    private SubCategory(
        long id,
        long categoryId,
        string? name,
        string? displayName,
        string? iconPath,
        string? action,
        string? role,
        long order) : base(id)
    {
        CategoryId = categoryId;
        Name = name;
        DisplayName = displayName;
        IconPath = iconPath;
        Action = action;
        Role = role;
        Order = order;
    }

    /// <summary>
    /// Rebuilds a <see cref="SubCategory"/> from trusted repository data.
    /// Bypasses validation — use only with data from the trusted repository layer.
    /// </summary>
    public static SubCategory Reconstitute(
        long id, long categoryId, string? name, string? displayName,
        string? iconPath, string? action, string? role, long order)
        => new(id, categoryId, name, displayName, iconPath, action, role, order);

    /// <summary>Creates a new sub-category after validating required fields.</summary>
    public static ErrorOr<SubCategory> Create(
        long categoryId, string? name, string? displayName,
        string? iconPath, string? action, string? role, long order)
    {
        if (string.IsNullOrWhiteSpace(name))
            return DomainError.Validation("SubCategory.NameEmpty", "Sub-category name cannot be empty.");

        var fieldsResult = MenuFieldPolicy.Validate(name, displayName, iconPath, controller: null, action, role, order);
        if (fieldsResult.IsError) return fieldsResult.Errors;

        return new SubCategory(0, categoryId, name, displayName, iconPath, action, role, order);
    }

    /// <summary>Updates this sub-category's fields after validating the name and every field against <see cref="MenuFieldPolicy"/>.</summary>
    public ErrorOr<Success> Update(
        long categoryId, string? name, string? displayName,
        string? iconPath, string? action, string? role, long order)
    {
        if (string.IsNullOrWhiteSpace(name))
            return DomainError.Validation("SubCategory.NameEmpty", "Sub-category name cannot be empty.");

        var fieldsResult = MenuFieldPolicy.Validate(name, displayName, iconPath, controller: null, action, role, order);
        if (fieldsResult.IsError) return fieldsResult.Errors;

        CategoryId = categoryId;
        Name = name;
        DisplayName = displayName;
        IconPath = iconPath;
        Action = action;
        Role = role;
        Order = order;
        return Result.Success;
    }
}

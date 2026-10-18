using ErrorOr;
using Maroik.Core.Domain.Errors;

namespace Maroik.Core.Domain.Menus;

/// <summary>
/// The menu safeguard: the navigation menu is the access-control list, and menu management is itself a page in it, so a
/// menu change that takes away the administrators' last route to <c>Management/Menu</c> would leave nobody able to repair
/// the menu from the UI. Such a change is refused. Everything else stays the administrator's call — any other entry,
/// and this one too while a second route to it exists. The counterpart of <c>AdminSafeguardPolicy</c>'s "last active
/// administrator" rule, for the menu.
/// </summary>
public static class MenuSafeguardPolicy
{
    /// <summary>The controller of the menu-management page (<c>ManagementController</c>).</summary>
    public const string MenuManagementController = "Management";

    /// <summary>The action of the menu-management page (<c>ManagementController.Menu</c>).</summary>
    public const string MenuManagementAction = "Menu";

    /// <summary>
    /// Refuses the change from <paramref name="before"/> to <paramref name="after"/> when it is the one that cuts the
    /// administrators off from menu management. A menu that had no route to begin with is not held to having one.
    /// </summary>
    public static ErrorOr<Success> CheckChange(MenuSnapshot before, MenuSnapshot after)
    {
        if (before.AdminCanReachMenuManagement && !after.AdminCanReachMenuManagement)
            return DomainError.Conflict("Menu.LastMenuManagementRoute",
                "The menu entry that lets administrators open menu management cannot be removed, moved or given to another role.");

        return Result.Success;
    }
}

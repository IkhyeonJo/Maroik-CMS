namespace Maroik.Website.Constants;

/// <summary>
/// Keys for values stashed on <c>HttpContext.Items</c> to hand data between the global filters
/// within a single request.
/// </summary>
internal static class HttpContextItemKeys
{
    /// <summary>
    /// The logged-in account (<c>AccountResponse</c>) for this request. Stashed by
    /// <see cref="Maroik.Website.Filters.AuthorizationFilter"/> for a signed-in session after re-loading and
    /// re-validating it from the database — the only place the account is loaded; then
    /// <see cref="Maroik.Website.Filters.ViewBagPopulatorFilter"/> fills in the anonymous placeholder when
    /// nothing was stashed (signed out), so it is always set by the time an action runs. Read it through
    /// <see cref="Maroik.Website.Extensions.HttpContextAccountExtensions"/>, never directly.
    /// </summary>
    internal const string LoggedInAccount = "__Maroik.LoggedInAccount";

    /// <summary>
    /// The per-role navigation menu (<see cref="Maroik.Website.Filters.NavigationMenus"/>) that
    /// <see cref="Maroik.Website.Filters.AuthorizationFilter"/> loaded for this request, so
    /// <see cref="Maroik.Website.Filters.ViewBagPopulatorFilter"/> can publish it to the ViewBag.
    /// </summary>
    internal const string NavigationMenus = "__Maroik.NavigationMenus";
}

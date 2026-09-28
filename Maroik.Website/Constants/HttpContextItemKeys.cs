namespace Maroik.Website.Constants;

/// <summary>
/// Keys for values stashed on <c>HttpContext.Items</c> to hand data between the global filters
/// within a single request.
/// </summary>
internal static class HttpContextItemKeys
{
    /// <summary>
    /// The logged-in account (<c>AccountResponse</c>) that <see cref="Maroik.Website.Filters.AuthorizationFilter"/>
    /// re-loaded and re-validated from the database for this request, so
    /// <see cref="Maroik.Website.Filters.ViewBagPopulatorFilter"/> can reuse it instead of issuing an
    /// identical second query.
    /// </summary>
    internal const string LoggedInAccount = "__Maroik.LoggedInAccount";

    /// <summary>
    /// The per-role navigation menu (<see cref="Maroik.Website.Filters.NavigationMenus"/>) that
    /// <see cref="Maroik.Website.Filters.AuthorizationFilter"/> loaded for this request, so
    /// <see cref="Maroik.Website.Filters.ViewBagPopulatorFilter"/> can publish it to the ViewBag.
    /// </summary>
    internal const string NavigationMenus = "__Maroik.NavigationMenus";
}

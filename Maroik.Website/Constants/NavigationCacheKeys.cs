namespace Maroik.Website.Constants;

/// <summary>
/// Distributed-cache keys used by <see cref="Maroik.Website.Filters.AuthorizationFilter"/> to store
/// role-partitioned navigation menu data (categories and sub-categories).
/// </summary>
internal static class NavigationCacheKeys
{
    /// <summary>Top-level menu categories shown to administrators.</summary>
    internal const string AdminCategories = "nav_admin_categories";
    /// <summary>Sub-menus shown to administrators.</summary>
    internal const string AdminSubCategories = "nav_admin_subcategories";
    /// <summary>Top-level menu categories shown to signed-in users.</summary>
    internal const string UserCategories = "nav_user_categories";
    /// <summary>Sub-menus shown to signed-in users.</summary>
    internal const string UserSubCategories = "nav_user_subcategories";
    /// <summary>Top-level menu categories shown to anonymous visitors.</summary>
    internal const string AnonymousCategories = "nav_anonymous_categories";
    /// <summary>Sub-menus shown to anonymous visitors.</summary>
    internal const string AnonymousSubCategories = "nav_anonymous_subcategories";
}

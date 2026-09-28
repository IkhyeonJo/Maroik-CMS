using Maroik.Website.Constants;
using Microsoft.Extensions.Caching.Distributed;

namespace Maroik.Website.Extensions;

/// <summary>
/// Extension methods for invalidating the navigation-menu cache entries populated by
/// <see cref="Maroik.Website.Filters.AuthorizationFilter"/>.
/// </summary>
public static class NavigationCacheExtensions
{
    extension(IDistributedCache cache)
    {
        /// <summary>
        /// Removes all cached navigation menu entries (Admin/User/Anonymous categories and
        /// sub-categories) so the next request re-reads the current Category/SubCategory rows
        /// from the database instead of serving a stale menu.
        /// </summary>
        public Task InvalidateNavigationMenuCacheAsync() => Task.WhenAll(
            cache.RemoveAsync(NavigationCacheKeys.AdminCategories),
            cache.RemoveAsync(NavigationCacheKeys.AdminSubCategories),
            cache.RemoveAsync(NavigationCacheKeys.UserCategories),
            cache.RemoveAsync(NavigationCacheKeys.UserSubCategories),
            cache.RemoveAsync(NavigationCacheKeys.AnonymousCategories),
            cache.RemoveAsync(NavigationCacheKeys.AnonymousSubCategories));
    }
}

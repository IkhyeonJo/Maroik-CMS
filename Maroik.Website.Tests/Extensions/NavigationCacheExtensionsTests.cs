using Maroik.Website.Constants;
using Maroik.Website.Extensions;
using Microsoft.Extensions.Caching.Distributed;
using Moq;

namespace Maroik.Website.Tests.Extensions;

/// <summary>
/// Unit tests for <see cref="NavigationCacheExtensions.InvalidateNavigationMenuCacheAsync"/> --
/// verifies every one of the six role-partitioned navigation cache keys is removed, so a stale menu
/// can never survive under a key this method forgot to include.
/// </summary>
public class NavigationCacheExtensionsTests
{
    /// <summary>All six Admin/User/Anonymous category and sub-category cache keys are removed.</summary>
    [Fact]
    public async Task InvalidateNavigationMenuCacheAsync_RemovesAllSixNavigationCacheKeys()
    {
        var cache = new Mock<IDistributedCache>();
        var removedKeys = new List<string>();
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => removedKeys.Add(key))
            .Returns(Task.CompletedTask);

        await cache.Object.InvalidateNavigationMenuCacheAsync();

        Assert.Equal(
            new[]
            {
                NavigationCacheKeys.AdminCategories,
                NavigationCacheKeys.AdminSubCategories,
                NavigationCacheKeys.UserCategories,
                NavigationCacheKeys.UserSubCategories,
                NavigationCacheKeys.AnonymousCategories,
                NavigationCacheKeys.AnonymousSubCategories,
            }.OrderBy(x => x),
            removedKeys.OrderBy(x => x));
    }

    /// <summary>A failure removing one key still surfaces (via the awaited Task.WhenAll) rather than being silently swallowed.</summary>
    [Fact]
    public async Task InvalidateNavigationMenuCacheAsync_PropagatesFailure_WhenAnyRemoveFails()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.RemoveAsync(NavigationCacheKeys.UserCategories, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));
        cache.Setup(c => c.RemoveAsync(It.IsNotIn(NavigationCacheKeys.UserCategories), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.Object.InvalidateNavigationMenuCacheAsync());
    }
}

using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.Finance;

/// <summary>
/// Membership check for <see cref="AssetItemType"/>, the source of truth for
/// <see cref="Asset.Item"/> values — mirrors the DB's <c>Asset_Item_check</c> constraint.
/// Domain-validated by <see cref="Asset.Create"/> / <see cref="Asset.Update"/>; also exposed here so
/// the service layer's pre-repository-lookup short-circuit (<c>AssetService</c>) can check the same
/// allow-list without hand-duplicating the member list. Built from <see cref="AssetItemType"/>'s
/// member names (rather than a hand-curated constant list, as the other taxonomies use) so it can
/// never drift from the enum it mirrors.
/// </summary>
public static class AssetItems
{
    /// <summary>Membership set backing <see cref="All"/> / <see cref="IsKnown"/>, built from the enum's member names.</summary>
    private static readonly StringTaxonomy _taxonomy = new(Enum.GetNames<AssetItemType>());

    /// <summary>Every defined <see cref="AssetItemType"/> member name.</summary>
    public static IReadOnlySet<string> All => _taxonomy.All;

    /// <summary>Returns true when <paramref name="item"/> names a defined <see cref="AssetItemType"/> member.</summary>
    public static bool IsKnown(string? item) => _taxonomy.IsKnown(item);
}

using Maroik.Core.Domain.ValueObjects;

namespace Maroik.Core.Domain.Finances;

/// <summary>
/// Resolves which monetary unit an account's dashboard should treat as its effective default,
/// self-correcting when the stored value is missing or no longer backed by any asset. Takes the
/// account's currently stored unit as a <see cref="CurrencyCode"/> rather than an Account reference,
/// since Finance must stay independent of the Account bounded context.
/// </summary>
public static class DefaultMonetaryUnitPolicy
{
    /// <summary>
    /// Returns the effective default monetary unit given <paramref name="allAssets"/>. When
    /// <paramref name="currentUnit"/> is missing or no longer matches any asset's currency, falls
    /// back to the most common currency among the assets. Returns null when there are no assets
    /// at all (nothing to fall back to). <paramref name="allAssets"/> must be the account's full
    /// asset list (deleted included), as the original asset lookup it replaced never filtered on
    /// Deleted.
    /// </summary>
    public static CurrencyCode? Resolve(CurrencyCode? currentUnit, IReadOnlyCollection<Asset> allAssets)
    {
        HashSet<CurrencyCode> availableUnits = [.. allAssets.Select(a => a.Balance.Currency)];
        if (availableUnits.Count == 0) return null;

        if (currentUnit != null && availableUnits.Contains(currentUnit)) return currentUnit;

        return allAssets.GroupBy(a => a.Balance.Currency).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key;
    }

    /// <summary>
    /// The monetary units an account may pick as its default: the distinct currency labels of all its assets, deleted
    /// ones included (the dashboard counts records on deleted assets too), in the order the assets come.
    /// </summary>
    public static IReadOnlyList<string> Selectable(IEnumerable<Asset> allAssets) =>
        [.. allAssets.Select(a => a.Balance.Currency.Value).Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// The default monetary unit an account may choose: <paramref name="requested"/> when it is one of the
    /// <see cref="Selectable"/> units, otherwise none.
    /// </summary>
    public static string? Choose(string? requested, IReadOnlyCollection<Asset> allAssets) =>
        requested != null && Selectable(allAssets).Contains(requested, StringComparer.Ordinal) ? requested : null;
}

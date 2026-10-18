namespace Maroik.Core.Domain.Finances;

/// <summary>
/// Which income / expenditure records the dashboard counts in the account's default monetary unit. A record carries
/// no currency of its own on the dashboard: it is in the currency of the asset it names (the deposit asset of an
/// income, the payment method of an expenditure). That asset is looked up among <em>all</em> the account's assets,
/// deleted included, so a record tied to a since-deleted asset still counts in its currency instead of dropping out.
/// </summary>
public sealed class DefaultUnitScope
{
    /// <summary>The default monetary unit the records are counted in.</summary>
    private readonly string _defaultUnit;

    /// <summary>Builds the scope for <paramref name="defaultUnit"/> over the account's full asset list.</summary>
    private DefaultUnitScope(string defaultUnit, IReadOnlyDictionary<string, string> currencyByProduct)
    {
        _defaultUnit = defaultUnit;
        _currencyByProduct = currencyByProduct;
    }

    /// <summary>Each asset's currency label, by product name.</summary>
    private readonly IReadOnlyDictionary<string, string> _currencyByProduct;

    /// <summary>
    /// The scope of <paramref name="defaultUnit"/> over <paramref name="allAssets"/> (the account's assets, deleted
    /// included). A product name is unique within an account (the <c>(ProductName, AccountEmail)</c> primary key).
    /// </summary>
    public static DefaultUnitScope Of(string defaultUnit, IEnumerable<Asset> allAssets) =>
        new(defaultUnit, allAssets.ToDictionary(a => a.ProductName, a => a.Balance.Currency.Value, StringComparer.Ordinal).AsReadOnly());

    /// <summary>Whether <paramref name="income"/> counts: its deposit asset carries the default unit.</summary>
    public bool Includes(Income income) => CarriesTheDefaultUnit(income.DepositMyAssetProductName);

    /// <summary>Whether <paramref name="expenditure"/> counts: its payment asset carries the default unit.</summary>
    public bool Includes(Expenditure expenditure) => CarriesTheDefaultUnit(expenditure.PaymentMethod);

    /// <summary>Whether the asset named <paramref name="productName"/> carries the default unit (an unknown asset does not).</summary>
    private bool CarriesTheDefaultUnit(string productName) => _currencyByProduct.GetValueOrDefault(productName) == _defaultUnit;
}

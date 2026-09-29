// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object used to create or update an asset (bank account, stock account, cash, etc.).
/// </summary>
public class AssetRequest
{
    /// <summary>Product name that identifies the asset (part of the composite primary key).</summary>
    public string? ProductName { get; set; }

    /// <summary>Asset category — an <c>AssetItemType</c> member name (e.g. "FreeDepositAndWithdrawal", "CashAsset").</summary>
    public string? Item { get; set; }

    /// <summary>Current balance or value of the asset.</summary>
    public decimal Amount { get; set; }

    /// <summary>Currency unit (e.g. "KRW", "USD").</summary>
    public string? MonetaryUnit { get; set; }

    /// <summary>Optional memo / note about the asset.</summary>
    public string? Note { get; set; }

    /// <summary>Soft-delete flag. True means the asset is logically deleted.</summary>
    public bool Deleted { get; set; }
}

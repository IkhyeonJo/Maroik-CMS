// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object returned when reading asset information.
/// </summary>
public class AssetResponse
{
    /// <summary>Product name (composite primary key component).</summary>
    public string? ProductName { get; set; }

    /// <summary>Owner's account email (composite primary key / foreign key).</summary>
    public string? AccountEmail { get; set; }

    /// <summary>Asset category — an <c>AssetItemType</c> member name (e.g. "FreeDepositAndWithdrawal", "CashAsset").</summary>
    public string? Item { get; set; }

    /// <summary>Current balance or value.</summary>
    public decimal Amount { get; set; }

    /// <summary>Currency unit (e.g. "KRW", "USD").</summary>
    public string? MonetaryUnit { get; set; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTime Created { get; set; }

    /// <summary>UTC last-updated timestamp.</summary>
    public DateTime Updated { get; set; }

    /// <summary>Optional memo / note.</summary>
    public string? Note { get; set; }

    /// <summary>Soft-delete flag.</summary>
    public bool Deleted { get; set; }
}

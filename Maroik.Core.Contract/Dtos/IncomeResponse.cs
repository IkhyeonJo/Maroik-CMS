// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object returned when reading a single income record.
/// </summary>
public class IncomeResponse
{
    /// <summary>Income ID (primary key).</summary>
    public long Id { get; set; }

    /// <summary>Email of the owning account.</summary>
    public string? AccountEmail { get; set; }

    /// <summary>Top-level income category.</summary>
    public string? MainClass { get; set; }

    /// <summary>Detailed income sub-category.</summary>
    public string? SubClass { get; set; }

    /// <summary>Description of the income source.</summary>
    public string? Content { get; set; }

    /// <summary>Income amount received.</summary>
    public decimal Amount { get; set; }

    /// <summary>Asset where the income was deposited.</summary>
    public string? DepositMyAssetProductName { get; set; }

    /// <summary>UTC timestamp when the income was recorded.</summary>
    public DateTime Created { get; set; }

    /// <summary>UTC last-updated timestamp.</summary>
    public DateTime Updated { get; set; }

    /// <summary>Optional free-text note.</summary>
    public string? Note { get; set; }

    /// <summary>Currency code (e.g. "KRW", "USD"). Populated by the service from the deposit asset.</summary>
    public string? MonetaryUnit { get; set; }
}

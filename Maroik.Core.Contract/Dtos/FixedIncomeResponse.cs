// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object returned when reading a fixed (recurring) income record.
/// </summary>
public class FixedIncomeResponse
{
    /// <summary>Fixed income ID (primary key).</summary>
    public long Id { get; set; }

    /// <summary>Email of the owning account.</summary>
    public string? AccountEmail { get; set; }

    /// <summary>Top-level income category.</summary>
    public string? MainClass { get; set; }

    /// <summary>Detailed income sub-category.</summary>
    public string? SubClass { get; set; }

    /// <summary>Description of the recurring income source.</summary>
    public string? Content { get; set; }

    /// <summary>Expected income amount per cycle.</summary>
    public decimal Amount { get; set; }

    /// <summary>Target deposit asset product name.</summary>
    public string? DepositMyAssetProductName { get; set; }

    /// <summary>Income cycle month (1–12).</summary>
    public short DepositMonth { get; set; }

    /// <summary>Income credit day of the month.</summary>
    public short DepositDay { get; set; }

    /// <summary>Recurring income end date.</summary>
    public DateTime MaturityDate { get; set; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTime Created { get; set; }

    /// <summary>UTC last-updated timestamp.</summary>
    public DateTime Updated { get; set; }

    /// <summary>Optional free-text note.</summary>
    public string? Note { get; set; }

    /// <summary>True when the income was not received on the scheduled day.</summary>
    public bool Unpunctuality { get; set; }

    /// <summary>Currency code (e.g. "KRW", "USD"). Populated by the service from the deposit asset.</summary>
    public string? MonetaryUnit { get; set; }
}

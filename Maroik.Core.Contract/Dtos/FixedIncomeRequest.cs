// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object used to create or update a fixed (recurring) income record.
/// Fixed income items are credited on a specific day each month (e.g. salary, pension).
/// </summary>
public class FixedIncomeRequest
{
    /// <summary>Fixed income ID (auto-incremented primary key; 0 for new records).</summary>
    public long Id { get; set; }

    /// <summary>Top-level income category (e.g. "RegularIncome").</summary>
    public string? MainClass { get; set; }

    /// <summary>Detailed income sub-category (e.g. "LaborIncome", "PensionIncome").</summary>
    public string? SubClass { get; set; }

    /// <summary>Description of the recurring income source (e.g. "Monthly salary").</summary>
    public string? Content { get; set; }

    /// <summary>Expected income amount per cycle.</summary>
    public decimal Amount { get; set; }

    /// <summary>Asset where the income will be deposited (foreign key to Asset.ProductName).</summary>
    public string? DepositMyAssetProductName { get; set; }

    /// <summary>Month of the income cycle (1–12).</summary>
    public short DepositMonth { get; set; }

    /// <summary>Day of the month the income is credited.</summary>
    public short DepositDay { get; set; }

    /// <summary>Date when the recurring income ends (e.g. contract end date).</summary>
    public DateTime MaturityDate { get; set; }

    /// <summary>Optional free-text note.</summary>
    public string? Note { get; set; }

    /// <summary>When true, the income was not received on the scheduled day.</summary>
    public bool Unpunctuality { get; set; }
}

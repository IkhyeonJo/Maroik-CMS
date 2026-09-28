// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object used to create or update a single income record.
/// </summary>
public class IncomeRequest
{
    /// <summary>Income ID (auto-incremented primary key; 0 for new records).</summary>
    public long Id { get; set; }

    /// <summary>Top-level income category (e.g. "RegularIncome", "IrregularIncome").</summary>
    public string? MainClass { get; set; }

    /// <summary>Detailed income sub-category (e.g. "LaborIncome", "FinancialIncome").</summary>
    public string? SubClass { get; set; }

    /// <summary>Description of the income source.</summary>
    public string? Content { get; set; }

    /// <summary>Income amount received.</summary>
    public decimal Amount { get; set; }

    /// <summary>Asset product name of the account where the income was deposited (foreign key).</summary>
    public string? DepositMyAssetProductName { get; set; }

    /// <summary>UTC timestamp when the income was recorded.</summary>
    public DateTime Created { get; set; }

    /// <summary>Optional free-text note.</summary>
    public string? Note { get; set; }
}

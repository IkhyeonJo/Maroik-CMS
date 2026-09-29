// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object used to create or update a fixed (recurring) expenditure record.
/// A fixed expenditure is scheduled on a month/day (<see cref="DepositMonth"/>/<see cref="DepositDay"/>) until its maturity date (e.g. subscriptions, insurance premiums).
/// </summary>
public class FixedExpenditureRequest
{
    /// <summary>Fixed expenditure ID (auto-incremented primary key; 0 for new records).</summary>
    public long Id { get; set; }

    /// <summary>Top-level expense category.</summary>
    public string? MainClass { get; set; }

    /// <summary>Detailed expense sub-category.</summary>
    public string? SubClass { get; set; }

    /// <summary>Description of the recurring expense (e.g. "Netflix subscription").</summary>
    public string? Content { get; set; }

    /// <summary>Recurring charge amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Asset used as the payment method (foreign key to Asset.ProductName).</summary>
    public string? PaymentMethod { get; set; }

    /// <summary>Asset credited by a transfer-type charge (must differ from <see cref="PaymentMethod"/>); ignored otherwise.</summary>
    public string? MyDepositAsset { get; set; }

    /// <summary>Month (1–12) of the scheduled charge date.</summary>
    public short DepositMonth { get; set; }

    /// <summary>Day of <see cref="DepositMonth"/> the charge is due.</summary>
    public short DepositDay { get; set; }

    /// <summary>Date when the recurring charge ends (contract or subscription expiry date).</summary>
    public DateTime MaturityDate { get; set; }

    /// <summary>Optional free-text note.</summary>
    public string? Note { get; set; }

    /// <summary>User-chosen "always notify" flag: when true the schedule is always noticed, regardless of its date.</summary>
    public bool Unpunctuality { get; set; }
}

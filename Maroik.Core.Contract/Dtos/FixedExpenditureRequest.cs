// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object used to create or update a fixed (recurring) expenditure record.
/// Fixed expenditures are charged on a specific day each month (e.g. subscriptions, insurance premiums).
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

    /// <summary>Debited asset product name.</summary>
    public string? MyDepositAsset { get; set; }

    /// <summary>Month of the billing cycle (1–12).</summary>
    public short DepositMonth { get; set; }

    /// <summary>Day of the month the charge is deducted.</summary>
    public short DepositDay { get; set; }

    /// <summary>Date when the recurring charge ends (contract or subscription expiry date).</summary>
    public DateTime MaturityDate { get; set; }

    /// <summary>Optional free-text note.</summary>
    public string? Note { get; set; }

    /// <summary>When true, the charge was not made on the scheduled day (payment was late or missed).</summary>
    public bool Unpunctuality { get; set; }
}

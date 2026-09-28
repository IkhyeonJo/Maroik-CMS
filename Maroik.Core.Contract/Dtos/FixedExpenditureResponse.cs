// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object returned when reading a fixed (recurring) expenditure record.
/// </summary>
public class FixedExpenditureResponse
{
    /// <summary>Fixed expenditure ID (primary key).</summary>
    public long Id { get; set; }

    /// <summary>Email of the owning account.</summary>
    public string? AccountEmail { get; set; }

    /// <summary>Top-level expense category.</summary>
    public string? MainClass { get; set; }

    /// <summary>Detailed expense sub-category.</summary>
    public string? SubClass { get; set; }

    /// <summary>Description of the recurring expense.</summary>
    public string? Content { get; set; }

    /// <summary>Recurring charge amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Payment method asset product name.</summary>
    public string? PaymentMethod { get; set; }

    /// <summary>Debited asset product name.</summary>
    public string? MyDepositAsset { get; set; }

    /// <summary>Billing month (1–12).</summary>
    public short DepositMonth { get; set; }

    /// <summary>Billing day of the month.</summary>
    public short DepositDay { get; set; }

    /// <summary>Contract/subscription expiry date.</summary>
    public DateTime MaturityDate { get; set; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTime Created { get; set; }

    /// <summary>UTC last-updated timestamp.</summary>
    public DateTime Updated { get; set; }

    /// <summary>Optional free-text note.</summary>
    public string? Note { get; set; }

    /// <summary>True when the scheduled payment was late or missed.</summary>
    public bool Unpunctuality { get; set; }

    /// <summary>Currency code (e.g. "KRW", "USD"). Populated by the service from the payment-method asset.</summary>
    public string? MonetaryUnit { get; set; }
}

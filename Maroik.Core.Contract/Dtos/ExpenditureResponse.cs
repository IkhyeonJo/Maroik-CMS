// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object returned when reading a single expenditure record.
/// </summary>
public class ExpenditureResponse
{
    /// <summary>Expenditure ID (primary key).</summary>
    public long Id { get; set; }

    /// <summary>Email of the owning account.</summary>
    public string? AccountEmail { get; set; }

    /// <summary>Top-level expense category.</summary>
    public string? MainClass { get; set; }

    /// <summary>Detailed expense sub-category.</summary>
    public string? SubClass { get; set; }

    /// <summary>Description of the expenditure.</summary>
    public string? Content { get; set; }

    /// <summary>Amount spent.</summary>
    public decimal Amount { get; set; }

    /// <summary>Asset the amount is debited from.</summary>
    public string? PaymentMethod { get; set; }

    /// <summary>Asset credited by a transfer-type expenditure; null for every other expenditure.</summary>
    public string? MyDepositAsset { get; set; }

    /// <summary>UTC timestamp when the expenditure was recorded.</summary>
    public DateTime Created { get; set; }

    /// <summary>UTC last-updated timestamp.</summary>
    public DateTime Updated { get; set; }

    /// <summary>Optional free-text note.</summary>
    public string? Note { get; set; }

    /// <summary>Currency code (e.g. "KRW", "USD"). Populated by the service from the payment-method asset.</summary>
    public string? MonetaryUnit { get; set; }
}

// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object used to create or update a single expenditure record.
/// </summary>
public class ExpenditureRequest
{
    /// <summary>Expenditure ID (auto-incremented primary key; 0 for new records).</summary>
    public long Id { get; set; }

    /// <summary>Top-level expense category (e.g. "ConsumerSpending", "NonConsumerSpending").</summary>
    public string? MainClass { get; set; }

    /// <summary>Detailed expense sub-category (e.g. "MealOrEatOutExpenses", "Tax").</summary>
    public string? SubClass { get; set; }

    /// <summary>Description of what was spent on.</summary>
    public string? Content { get; set; }

    /// <summary>Amount spent.</summary>
    public decimal Amount { get; set; }

    /// <summary>Asset product name used as payment method (foreign key to Asset.ProductName).</summary>
    public string? PaymentMethod { get; set; }

    /// <summary>Asset that was debited (same as PaymentMethod in most cases).</summary>
    public string? MyDepositAsset { get; set; }

    /// <summary>UTC timestamp when the expenditure occurred / was recorded.</summary>
    public DateTime Created { get; set; }

    /// <summary>Optional free-text note.</summary>
    public string? Note { get; set; }
}

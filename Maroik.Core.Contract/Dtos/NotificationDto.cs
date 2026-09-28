namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Badge counts displayed in the navigation header to alert the user about
/// upcoming or overdue fixed income / expenditure items.
/// </summary>
public class NotificationDto
{
    /// <summary>Number of fixed income items whose deposit day is approaching within the notice period.</summary>
    public int FixedIncomesNoticed { get; init; }

    /// <summary>Number of fixed expenditure items whose payment day is approaching within the notice period.</summary>
    public int FixedExpendituresNoticed { get; init; }

    /// <summary>Number of fixed income items whose maturity date has already passed.</summary>
    public int FixedIncomesExpired { get; init; }

    /// <summary>Number of fixed expenditure items whose maturity date has already passed.</summary>
    public int FixedExpendituresExpired { get; init; }
}

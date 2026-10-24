using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.Budgets;

/// <summary>
/// Aggregate root for one cell input of a budget sheet: the stored form (<see cref="BudgetCellInput"/>) of what was typed, at a row
/// key and column. Values are never stored: they are calculated from the inputs on every load.
/// </summary>
public sealed class BudgetSheetCell : AggregateRoot<long>
{
    /// <summary>The sheet's id.</summary>
    public long SheetId { get; }

    /// <summary>The row's stable key.</summary>
    public string RowKey { get; }

    /// <summary>The column letter.</summary>
    public string Column { get; }

    /// <summary>The stored input.</summary>
    public string Input { get; private set; }

    /// <summary>UTC timestamp of the most recent change.</summary>
    public DateTime Updated { get; private set; }

    /// <summary>Assigns every field verbatim.</summary>
    private BudgetSheetCell(long id, long sheetId, string rowKey, string column, string input, DateTime updated) : base(id)
    {
        SheetId = sheetId;
        RowKey = rowKey;
        Column = column;
        Input = input;
        Updated = updated;
    }

    /// <summary>A new cell input (already accepted by <see cref="BudgetCellInput.Parse"/>).</summary>
    public static BudgetSheetCell Create(long sheetId, string rowKey, string column, string input, DateTime utcNow) =>
        new(0, sheetId, rowKey, column, input, utcNow);

    /// <summary>Rebuilds a cell input from trusted repository data.</summary>
    public static BudgetSheetCell Reconstitute(long id, long sheetId, string rowKey, string column, string input, DateTime updated) =>
        new(id, sheetId, rowKey, column, input, updated);

    /// <summary>Replaces the input (already accepted by <see cref="BudgetCellInput.Parse"/>).</summary>
    public void ChangeInput(string input, DateTime utcNow)
    {
        Input = input;
        Updated = utcNow;
    }
}

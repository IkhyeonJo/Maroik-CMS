namespace Maroik.Core.Domain.Budgets;

/// <summary>A row of a budget sheet.</summary>
/// <param name="Number">Its row number (1-based) in the sheet as laid out now.</param>
/// <param name="Key">Its stable key (what stored formulas refer to).</param>
/// <param name="Kind">What it is.</param>
/// <param name="MainClass">Its main class (header, line, subtotal), or null.</param>
/// <param name="SubClass">Its sub-class (line), or null.</param>
/// <param name="Currency">Its currency (line, subtotal, total), or null.</param>
public sealed record BudgetRow(int Number, string Key, BudgetRowKind Kind, string? MainClass, string? SubClass, string? Currency)
{
    /// <summary>The free columns of every fixed row.</summary>
    private const string FreeColumns = "OPQRSTUVWXYZ";

    /// <summary>
    /// The columns of this row a user can edit, as one string of letters: a line's months (B..M) and the free columns O..Z; only O..Z in
    /// a header, subtotal or total row; every column in a free row. The cell roles follow this (<see cref="BudgetSheetLayout.Role"/>).
    /// </summary>
    public string EditableColumns => Kind switch
    {
        BudgetRowKind.Line => "BCDEFGHIJKLM" + FreeColumns,
        BudgetRowKind.Free => "ABCDEFGHIJKLMN" + FreeColumns,
        _ => FreeColumns,
    };
}

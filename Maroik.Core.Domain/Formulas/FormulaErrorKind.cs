namespace Maroik.Core.Domain.Formulas;

/// <summary>Excel's error values (and #CYCLE! for a cell in a circular reference).</summary>
public enum FormulaErrorKind
{
    /// <summary>#DIV/0! — division by zero (or an average of nothing).</summary>
    DivideByZero,

    /// <summary>#VALUE! — a value of the wrong type.</summary>
    Value,

    /// <summary>#REF! — a reference to a cell that does not exist.</summary>
    Ref,

    /// <summary>#NAME? — an unknown function or name.</summary>
    Name,

    /// <summary>#NUM! — a number out of range.</summary>
    Num,

    /// <summary>#N/A — no value available.</summary>
    NotAvailable,

    /// <summary>#CYCLE! — the cell is part of a circular reference.</summary>
    Cycle,
}

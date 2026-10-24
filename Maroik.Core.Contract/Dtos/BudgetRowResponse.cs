namespace Maroik.Core.Contract.Dtos;

/// <summary>A row of a budget sheet.</summary>
public sealed class BudgetRowResponse
{
    /// <summary>Its row number (1-based).</summary>
    public int Number { get; init; }

    /// <summary>Header, Line, Subtotal, Total or Free.</summary>
    public string Kind { get; init; } = "";

    /// <summary>Its main class (header, line, subtotal), or null.</summary>
    public string? MainClass { get; init; }

    /// <summary>Its sub-class (line), or null.</summary>
    public string? SubClass { get; init; }

    /// <summary>Its currency (line, subtotal, total), or null.</summary>
    public string? Currency { get; init; }

    /// <summary>The letters of the columns a user can edit in this row.</summary>
    public string EditableColumns { get; init; } = "";
}

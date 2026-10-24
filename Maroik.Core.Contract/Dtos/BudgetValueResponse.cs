namespace Maroik.Core.Contract.Dtos;

/// <summary>A calculated value: its kind and either its number or its text.</summary>
public sealed class BudgetValueResponse
{
    /// <summary>Number, Text, Boolean, Empty or Error.</summary>
    public string Kind { get; init; } = "";

    /// <summary>The number, for a number.</summary>
    public decimal? Number { get; init; }

    /// <summary>The text: a text value, TRUE / FALSE, or the error's #... text; null for a number or empty.</summary>
    public string? Text { get; init; }
}

namespace Maroik.Core.Contract.Dtos;

/// <summary>A token of a formula being typed, as the editor colours it.</summary>
public sealed class BudgetTokenResponse
{
    /// <summary>Its start index in the input.</summary>
    public int Start { get; init; }

    /// <summary>Its length.</summary>
    public int Length { get; init; }

    /// <summary>Its colour class.</summary>
    public string Class { get; init; } = "";

    /// <summary>For a reference, its colour index.</summary>
    public int? Reference { get; init; }

    /// <summary>For a parenthesis, the token index of its partner.</summary>
    public int? Partner { get; init; }
}

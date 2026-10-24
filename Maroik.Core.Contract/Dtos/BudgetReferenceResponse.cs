namespace Maroik.Core.Contract.Dtos;

/// <summary>A distinct reference of a formula being typed: its colour index and the cells it outlines.</summary>
public sealed class BudgetReferenceResponse
{
    /// <summary>Its colour index.</summary>
    public int Index { get; init; }

    /// <summary>The top-left cell outlined, or null.</summary>
    public string? TopLeft { get; init; }

    /// <summary>The bottom-right cell outlined, or null.</summary>
    public string? BottomRight { get; init; }
}

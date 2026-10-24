namespace Maroik.Website.Models.ViewModels.AccountBooks;

/// <summary>A reference of the formula being typed and the block it outlines on the grid.</summary>
public sealed class BudgetReferenceViewModel
{
    /// <summary>Its colour index.</summary>
    public int Index { get; init; }

    /// <summary>The outlined block's top-left cell, or empty when nothing is outlined.</summary>
    public string TopLeft { get; init; } = "";

    /// <summary>Its bottom-right cell, or empty.</summary>
    public string BottomRight { get; init; } = "";
}

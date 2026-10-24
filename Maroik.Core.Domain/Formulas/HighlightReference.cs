namespace Maroik.Core.Domain.Formulas;

/// <summary>A distinct reference of a formula being typed: its colour index and, on the home sheet, the cells the grid outlines.</summary>
/// <param name="Index">Its colour index (the order of first appearance).</param>
/// <param name="TopLeft">The A1 address of the outlined block's top-left cell, or null when nothing is outlined.</param>
/// <param name="BottomRight">The A1 address of its bottom-right cell, or null.</param>
public sealed record HighlightReference(int Index, string? TopLeft, string? BottomRight);

namespace Maroik.Core.Domain.Formulas;

/// <summary>A stretch of a cell's input text: where a token, node or diagnostic starts and how many characters it covers.</summary>
/// <param name="Start">Index of the first character in the cell input.</param>
/// <param name="Length">Number of characters.</param>
public readonly record struct TextSpan(int Start, int Length)
{
    /// <summary>Index just past the last character.</summary>
    public int End => Start + Length;

    /// <summary>The span from the start of <paramref name="first"/> to the end of <paramref name="last"/>.</summary>
    public static TextSpan Covering(TextSpan first, TextSpan last) => new(first.Start, last.End - first.Start);
}

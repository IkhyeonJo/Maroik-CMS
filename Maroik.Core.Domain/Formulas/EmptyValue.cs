namespace Maroik.Core.Domain.Formulas;

/// <summary>An empty cell (0 in arithmetic, "" in text, FALSE in logic).</summary>
public sealed record EmptyValue : FormulaValue
{
    /// <summary>The one empty value.</summary>
    public static EmptyValue Instance { get; } = new();

    /// <summary>Use <see cref="Instance"/>.</summary>
    private EmptyValue() { }
}

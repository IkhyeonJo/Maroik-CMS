namespace Maroik.Core.Domain.Formulas;

/// <summary>What a cell holds: a constant value or a bound formula.</summary>
public sealed record CellDefinition
{
    /// <summary>The constant, for a value cell.</summary>
    public FormulaValue? Constant { get; private init; }

    /// <summary>The formula, for a formula cell.</summary>
    public BoundFormula? Formula { get; private init; }

    /// <summary>Use <see cref="Of(FormulaValue)"/> or <see cref="Of(BoundFormula)"/>.</summary>
    private CellDefinition() { }

    /// <summary>A constant cell.</summary>
    public static CellDefinition Of(FormulaValue constant) => new() { Constant = constant };

    /// <summary>A formula cell.</summary>
    public static CellDefinition Of(BoundFormula formula) => new() { Formula = formula };
}

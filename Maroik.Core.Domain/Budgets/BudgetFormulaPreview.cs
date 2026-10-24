using Maroik.Core.Domain.Formulas;

namespace Maroik.Core.Domain.Budgets;

/// <summary>What the editor shows for an input being typed: its colouring and, for a formula that can be saved, the value it would have.</summary>
/// <param name="Highlight">The colouring (empty for an input that is not a formula).</param>
/// <param name="Value">The value it would have, or null.</param>
public sealed record BudgetFormulaPreview(FormulaHighlight Highlight, FormulaValue? Value);

namespace Maroik.Core.Domain.Budgets;

/// <summary>The outcome of an edit: the inputs to store and the open sheet's cells whose value or input changed.</summary>
/// <param name="Writes">The inputs to store.</param>
/// <param name="Changed">The changed cells of the edited sheet.</param>
public sealed record BudgetEdit(IReadOnlyList<BudgetCellWrite> Writes, IReadOnlyList<BudgetCellView> Changed);

namespace Maroik.Core.Domain.Formulas;

/// <summary>
/// The reactive calculation engine: holds every cell's definition and value. <see cref="RecalculateAll"/> calculates the workbook in
/// dependency order; after an edit, <see cref="Recalculate"/> evaluates only the cells the edit reaches — the edited cells and their
/// dependents, transitively, across sheets — in dependency order, and reports the cells whose value changed. The cells of a
/// circular reference are #CYCLE! (and so is what reads them); everything else still calculates.
/// </summary>
/// <param name="actualOf">The actual total behind a budget cell (null when the cell is not one), for ACTUAL.</param>
/// <param name="normalize">
/// Turns a cell's evaluated value into the value it holds (what it shows and its readers see), e.g. #NUM! for a negative budget;
/// null keeps every value as evaluated.
/// </param>
public sealed class RecalculationEngine(Func<CellAddress, decimal?> actualOf, Func<CellAddress, FormulaValue, FormulaValue>? normalize = null)
{
    /// <summary>The definition of every cell that has one.</summary>
    private readonly Dictionary<CellAddress, CellDefinition> _definitions = [];

    /// <summary>The current value of every cell that has one (others are empty).</summary>
    private readonly Dictionary<CellAddress, FormulaValue> _values = [];

    /// <summary>The cells' dependencies.</summary>
    private readonly FormulaDependencyGraph _graph = new();

    /// <summary>#CYCLE!.</summary>
    private static readonly ErrorValue Cycle = new(FormulaErrorKind.Cycle);

    /// <summary>Sets (or, with null, clears) a cell's definition; its value changes on the next recalculation.</summary>
    public void Define(CellAddress cell, CellDefinition? definition)
    {
        if (definition is null)
        {
            _definitions.Remove(cell);
            _graph.Remove(cell);
            return;
        }

        _definitions[cell] = definition;
        _graph.SetPrecedents(cell, definition.Formula?.Precedents ?? []);
    }

    /// <summary>The current value of a cell (empty when it has none).</summary>
    public FormulaValue ValueOf(CellAddress cell) => _values.GetValueOrDefault(cell, EmptyValue.Instance);

    /// <summary>Calculates every defined cell.</summary>
    public void RecalculateAll()
    {
        _values.Clear();
        Evaluate([.. _definitions.Keys]);
    }

    /// <summary>Recalculates what the edited <paramref name="changed"/> cells reach; returns the cells whose value changed, in calculation order.</summary>
    public IReadOnlyList<CellAddress> Recalculate(IEnumerable<CellAddress> changed)
    {
        IReadOnlySet<CellAddress> reached = _graph.Reach(changed);
        Dictionary<CellAddress, FormulaValue> before = reached.ToDictionary(c => c, ValueOf);
        foreach (CellAddress cell in reached.Where(c => !_definitions.ContainsKey(c)))
            _values.Remove(cell);

        IReadOnlyList<CellAddress> order = Evaluate([.. reached.Where(_definitions.ContainsKey)]);
        return [.. reached.Where(c => !_definitions.ContainsKey(c)).Concat(order).Where(c => before[c] != ValueOf(c))];
    }

    /// <summary>
    /// The value <paramref name="formula"/> would have in <paramref name="cell"/>, against the current values, without defining it
    /// (the editor's preview); #CYCLE! when it would read itself.
    /// </summary>
    public FormulaValue Preview(CellAddress cell, BoundFormula formula)
    {
        IReadOnlySet<CellAddress> dependents = _graph.Reach([cell]);
        return formula.Precedents.Any(dependents.Contains) ? Cycle : FormulaEvaluator.Evaluate(formula, Context);
    }

    /// <summary>What a formula reads during evaluation.</summary>
    private FormulaContext Context => new(ValueOf, actualOf);

    /// <summary>Evaluates <paramref name="cells"/> in dependency order (cyclic ones become #CYCLE!); returns the order used.</summary>
    private List<CellAddress> Evaluate(List<CellAddress> cells)
    {
        var (order, cyclic) = _graph.Order(cells);
        foreach (CellAddress cell in cyclic)
            _values[cell] = Cycle;
        foreach (CellAddress cell in order)
        {
            CellDefinition definition = _definitions[cell];
            FormulaValue value = definition.Constant ?? FormulaEvaluator.Evaluate(definition.Formula!, Context);
            _values[cell] = normalize is null ? value : normalize(cell, value);
        }

        return [.. cells.Where(cyclic.Contains), .. order];
    }
}

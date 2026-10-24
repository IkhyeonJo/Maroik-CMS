namespace Maroik.Core.Domain.Formulas;

/// <summary>
/// The dependency graph of a workbook's cells: for each formula cell the cells it reads (precedents), and the reverse edges
/// (dependents), so an edit can find every cell it reaches. <see cref="Order"/> finds circular references as strongly connected
/// components (Tarjan) and orders the rest so every cell comes after the cells it reads (Kahn). Both walks are iterative, so a long
/// chain of references cannot overflow the stack.
/// </summary>
public sealed class FormulaDependencyGraph
{
    /// <summary>The cells each cell reads.</summary>
    private readonly Dictionary<CellAddress, HashSet<CellAddress>> _precedents = [];

    /// <summary>The cells that read each cell.</summary>
    private readonly Dictionary<CellAddress, HashSet<CellAddress>> _dependents = [];

    /// <summary>Replaces the cells <paramref name="cell"/> reads.</summary>
    public void SetPrecedents(CellAddress cell, IEnumerable<CellAddress> precedents)
    {
        Remove(cell);
        HashSet<CellAddress> set = [.. precedents];
        if (set.Count == 0) return;
        _precedents[cell] = set;
        foreach (CellAddress precedent in set)
        {
            if (!_dependents.TryGetValue(precedent, out HashSet<CellAddress>? dependents))
                _dependents[precedent] = dependents = [];
            dependents.Add(cell);
        }
    }

    /// <summary>Drops the cells <paramref name="cell"/> reads (the cells that read it keep their edges).</summary>
    public void Remove(CellAddress cell)
    {
        if (!_precedents.Remove(cell, out HashSet<CellAddress>? old)) return;
        foreach (CellAddress precedent in old)
        {
            HashSet<CellAddress> dependents = _dependents[precedent];
            dependents.Remove(cell);
            if (dependents.Count == 0) _dependents.Remove(precedent);
        }
    }

    /// <summary>The cells that read <paramref name="cell"/> directly.</summary>
    public IReadOnlyCollection<CellAddress> DependentsOf(CellAddress cell) =>
        _dependents.TryGetValue(cell, out HashSet<CellAddress>? set) ? set : [];

    /// <summary>The cells <paramref name="cell"/> reads directly.</summary>
    public IReadOnlyCollection<CellAddress> PrecedentsOf(CellAddress cell) =>
        _precedents.TryGetValue(cell, out HashSet<CellAddress>? set) ? set : [];

    /// <summary>The given cells and every cell that depends on them, directly or not.</summary>
    public IReadOnlySet<CellAddress> Reach(IEnumerable<CellAddress> cells)
    {
        HashSet<CellAddress> reached = [];
        Queue<CellAddress> queue = new();
        foreach (CellAddress cell in cells)
            if (reached.Add(cell)) queue.Enqueue(cell);
        while (queue.TryDequeue(out CellAddress? cell))
            foreach (CellAddress dependent in DependentsOf(cell))
                if (reached.Add(dependent)) queue.Enqueue(dependent);
        return reached;
    }

    /// <summary>
    /// Orders <paramref name="cells"/> (edges to other cells are ignored): the cells on a circular reference — a strongly connected
    /// component of more than one cell, or a cell that reads itself — are returned apart, every other cell comes after the cells it
    /// reads. Ties keep the order of <paramref name="cells"/>.
    /// </summary>
    public (List<CellAddress> Order, IReadOnlySet<CellAddress> Cyclic) Order(IReadOnlyCollection<CellAddress> cells)
    {
        HashSet<CellAddress> subset = [.. cells];
        HashSet<CellAddress> cyclic = Cycles(cells, subset);

        Dictionary<CellAddress, int> waiting = [];
        Queue<CellAddress> ready = new();
        foreach (CellAddress cell in cells.Distinct())
        {
            if (cyclic.Contains(cell)) continue;
            int count = PrecedentsOf(cell).Count(p => subset.Contains(p) && !cyclic.Contains(p));
            waiting[cell] = count;
            if (count == 0) ready.Enqueue(cell);
        }

        List<CellAddress> order = [];
        while (ready.TryDequeue(out CellAddress? cell))
        {
            order.Add(cell);
            foreach (CellAddress dependent in DependentsOf(cell))
                if (waiting.TryGetValue(dependent, out int left) && (waiting[dependent] = left - 1) == 0)
                    ready.Enqueue(dependent);
        }

        return (order, cyclic);
    }

    /// <summary>The cells of <paramref name="subset"/> on a circular reference (iterative Tarjan over the subset's edges).</summary>
    private HashSet<CellAddress> Cycles(IEnumerable<CellAddress> cells, HashSet<CellAddress> subset)
    {
        Dictionary<CellAddress, int> index = [], low = [];
        Stack<CellAddress> stack = new();
        HashSet<CellAddress> onStack = [], cyclic = [];
        int next = 0;

        foreach (CellAddress root in cells)
        {
            if (index.ContainsKey(root)) continue;
            var work = new Stack<(CellAddress Cell, IEnumerator<CellAddress> Edges)>();
            Visit(root);
            while (work.Count > 0)
            {
                var (cell, edges) = work.Peek();
                if (edges.MoveNext())
                {
                    CellAddress precedent = edges.Current;
                    if (!subset.Contains(precedent)) continue;
                    if (!index.ContainsKey(precedent)) Visit(precedent);
                    else if (onStack.Contains(precedent)) low[cell] = Math.Min(low[cell], index[precedent]);
                    continue;
                }

                edges.Dispose();
                work.Pop();
                if (work.Count > 0) low[work.Peek().Cell] = Math.Min(low[work.Peek().Cell], low[cell]);
                if (low[cell] != index[cell]) continue;

                List<CellAddress> component = [];
                CellAddress member;
                do
                {
                    member = stack.Pop();
                    onStack.Remove(member);
                    component.Add(member);
                } while (member != cell);

                if (component.Count > 1 || PrecedentsOf(cell).Contains(cell))
                    cyclic.UnionWith(component);
            }

            void Visit(CellAddress cell)
            {
                index[cell] = low[cell] = next++;
                stack.Push(cell);
                onStack.Add(cell);
                work.Push((cell, PrecedentsOf(cell).ToList().GetEnumerator()));
            }
        }

        return cyclic;
    }
}

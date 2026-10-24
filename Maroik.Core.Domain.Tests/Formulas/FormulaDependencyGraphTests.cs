using Maroik.Core.Domain.Formulas;
using static Maroik.Core.Domain.Tests.Fixtures.FormulaFixture;

namespace Maroik.Core.Domain.Tests.Formulas;

/// <summary>
/// Unit tests for <see cref="FormulaDependencyGraph"/>: precedent / dependent edges between cells, the cells an edit reaches, cycles
/// (strongly connected components, Tarjan) and the order cells are recalculated in (precedents first, Kahn).
/// </summary>
public class FormulaDependencyGraphTests
{
    /// <summary>A graph with the given edges, written "B1&lt;-A1" (B1 reads A1).</summary>
    private static FormulaDependencyGraph Graph(params string[] edges)
    {
        var graph = new FormulaDependencyGraph();
        foreach (var group in edges.Select(e => e.Split("<-")).GroupBy(e => e[0]))
            graph.SetPrecedents(At(group.Key), group.Select(e => At(e[1])));
        return graph;
    }

    /// <summary>Dependents and precedents are kept both ways.</summary>
    [Fact]
    public void Edges_AreKeptBothWays()
    {
        FormulaDependencyGraph graph = Graph("B1<-A1", "C1<-A1");

        Assert.Equal([At("B1"), At("C1")], graph.DependentsOf(At("A1")).OrderBy(a => a.Column));
        Assert.Equal([At("A1")], graph.PrecedentsOf(At("B1")));
    }

    /// <summary>Setting a cell's precedents again replaces its old edges.</summary>
    [Fact]
    public void SetPrecedents_ReplacesTheOldEdges()
    {
        FormulaDependencyGraph graph = Graph("B1<-A1");

        graph.SetPrecedents(At("B1"), [At("C1")]);

        Assert.Empty(graph.DependentsOf(At("A1")));
        Assert.Equal([At("B1")], graph.DependentsOf(At("C1")));
    }

    /// <summary>Removing a cell drops its outgoing edges; cells that read it keep theirs.</summary>
    [Fact]
    public void Remove_DropsTheCellsEdges()
    {
        FormulaDependencyGraph graph = Graph("B1<-A1", "C1<-B1");

        graph.Remove(At("B1"));

        Assert.Empty(graph.DependentsOf(At("A1")));
        Assert.Equal([At("C1")], graph.DependentsOf(At("B1")));
    }

    /// <summary>An edit reaches the edited cells and every cell that depends on them, directly or not; nothing else.</summary>
    [Fact]
    public void Reach_IsTheTransitiveDependents()
    {
        FormulaDependencyGraph graph = Graph("B1<-A1", "C1<-B1", "D1<-C1", "E1<-Z1");

        Assert.Equal(["A", "B", "C", "D"], graph.Reach([At("A1")]).Select(a => a.Column).Order(StringComparer.Ordinal));
    }

    /// <summary>The order puts every cell after the cells it reads (a diamond included).</summary>
    [Fact]
    public void Order_PutsPrecedentsFirst()
    {
        FormulaDependencyGraph graph = Graph("B1<-A1", "C1<-A1", "D1<-B1", "D1<-C1");

        var (order, cyclic) = graph.Order([At("D1"), At("C1"), At("B1"), At("A1")]);

        Assert.Empty(cyclic);
        Assert.Equal(At("A1"), order[0]);
        Assert.Equal(At("D1"), order[3]);
    }

    /// <summary>Cells on a cycle (two cells, or one reading itself) are found; cells that only depend on a cycle are ordered after it.</summary>
    [Fact]
    public void Order_FindsCycles()
    {
        FormulaDependencyGraph graph = Graph("A1<-B1", "B1<-A1", "C1<-C1", "D1<-A1", "E1<-Z1");

        var (order, cyclic) = graph.Order([At("A1"), At("B1"), At("C1"), At("D1"), At("E1"), At("Z1")]);

        Assert.Equal(["A", "B", "C"], cyclic.Select(a => a.Column).Order(StringComparer.Ordinal));
        Assert.Equal(["D", "E", "Z"], order.Select(a => a.Column).Order(StringComparer.Ordinal));
        Assert.True(order.IndexOf(At("Z1")) < order.IndexOf(At("E1")));
    }

    /// <summary>The order only covers the cells asked for; edges to other cells are ignored.</summary>
    [Fact]
    public void Order_IsLimitedToTheGivenCells()
    {
        FormulaDependencyGraph graph = Graph("B1<-A1", "C1<-B1");

        var (order, _) = graph.Order([At("C1"), At("B1")]);

        Assert.Equal([At("B1"), At("C1")], order);
    }

    /// <summary>A long chain does not overflow the stack (the walk is iterative).</summary>
    [Fact]
    public void ALongChain_IsOrdered()
    {
        var graph = new FormulaDependencyGraph();
        var cells = Enumerable.Range(1, 20000).Select(i => new CellAddress("S", "R" + i, "B")).ToList();
        for (int i = 1; i < cells.Count; i++) graph.SetPrecedents(cells[i], [cells[i - 1]]);

        var (order, cyclic) = graph.Order(cells);

        Assert.Empty(cyclic);
        Assert.Equal(cells, order);
    }
}

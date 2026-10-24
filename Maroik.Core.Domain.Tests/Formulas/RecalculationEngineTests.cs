using Maroik.Core.Domain.Formulas;
using static Maroik.Core.Domain.Tests.Fixtures.FormulaFixture;

namespace Maroik.Core.Domain.Tests.Formulas;

/// <summary>
/// Unit tests for <see cref="RecalculationEngine"/>: a full calculation in dependency order, then reactive recalculation of an edit —
/// only the cells it reaches, reporting the values that changed — with #CYCLE! for the cells of a circular reference.
/// </summary>
public class RecalculationEngineTests
{
    /// <summary>The definition of a cell from its A1 input on sheet S: a formula, or a number.</summary>
    private static CellDefinition Def(string input) =>
        input.StartsWith('=') ? CellDefinition.Of(Bind(input)) : CellDefinition.Of(new NumberValue(decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>An engine with the given cells ("B1" → input), fully calculated.</summary>
    private static RecalculationEngine Engine(Func<CellAddress, decimal?>? actualOf = null, params (string Cell, string Input)[] cells)
    {
        var engine = new RecalculationEngine(actualOf ?? (_ => null));
        foreach (var (cell, input) in cells) engine.Define(At(cell), Def(input));
        engine.RecalculateAll();
        return engine;
    }

    /// <summary>A full calculation evaluates every formula after the cells it reads.</summary>
    [Fact]
    public void RecalculateAll_FollowsTheDependencies()
    {
        RecalculationEngine engine = Engine(null, ("D1", "=B1+C1"), ("B1", "=A1*2"), ("C1", "=A1+1"), ("A1", "5"));

        Assert.Equal(new NumberValue(16), engine.ValueOf(At("D1")));
        Assert.Equal(EmptyValue.Instance, engine.ValueOf(At("Z9")));
    }

    /// <summary>An edit recalculates the cells it reaches and reports only the values that changed.</summary>
    [Fact]
    public void Recalculate_ReportsTheChangedValues()
    {
        RecalculationEngine engine = Engine(null, ("A1", "5"), ("B1", "=A1*2"), ("C1", "=IF(A1>0,1,0)"), ("D1", "=Z1+1"));

        engine.Define(At("A1"), Def("7"));
        IReadOnlyList<CellAddress> changed = engine.Recalculate([At("A1")]);

        Assert.Equal([At("A1"), At("B1")], changed);
        Assert.Equal(new NumberValue(14), engine.ValueOf(At("B1")));
        Assert.Equal(new NumberValue(1), engine.ValueOf(At("C1")));
    }

    /// <summary>Only the reached cells are evaluated again (an untouched formula is not).</summary>
    [Fact]
    public void Recalculate_EvaluatesOnlyTheReachedCells()
    {
        int reads = 0;
        RecalculationEngine engine = Engine(a => { reads++; return 1m; }, ("A1", "5"), ("B1", "=A1+1"), ("C1", "=ACTUAL(Z1)"));
        reads = 0;

        engine.Define(At("A1"), Def("6"));
        engine.Recalculate([At("A1")]);

        Assert.Equal(0, reads);
    }

    /// <summary>The cells of a cycle are #CYCLE!, and so is what reads them; the rest calculates.</summary>
    [Fact]
    public void ACycle_IsMarked()
    {
        RecalculationEngine engine = Engine(null, ("A1", "=B1"), ("B1", "=A1+1"), ("C1", "=A1*2"), ("D1", "=C1&\"\""), ("E1", "3"), ("F1", "=F1"));

        var cycle = new ErrorValue(FormulaErrorKind.Cycle);
        Assert.Equal([cycle, cycle, cycle, cycle, cycle], new[] { "A1", "B1", "C1", "D1", "F1" }.Select(c => engine.ValueOf(At(c))));
        Assert.Equal(new NumberValue(3), engine.ValueOf(At("E1")));
    }

    /// <summary>An edit that closes a cycle marks it; an edit that breaks it calculates again.</summary>
    [Fact]
    public void Edits_CloseAndBreakCycles()
    {
        RecalculationEngine engine = Engine(null, ("A1", "1"), ("B1", "=A1+1"));

        engine.Define(At("A1"), Def("=B1"));
        engine.Recalculate([At("A1")]);
        Assert.Equal(new ErrorValue(FormulaErrorKind.Cycle), engine.ValueOf(At("B1")));

        engine.Define(At("A1"), Def("10"));
        IReadOnlyList<CellAddress> changed = engine.Recalculate([At("A1")]);
        Assert.Equal(new NumberValue(11), engine.ValueOf(At("B1")));
        Assert.Equal([At("A1"), At("B1")], changed);
    }

    /// <summary>Clearing a cell makes it empty for the cells that read it.</summary>
    [Fact]
    public void Clearing_EmptiesTheCell()
    {
        RecalculationEngine engine = Engine(null, ("A1", "5"), ("B1", "=A1+1"));

        engine.Define(At("A1"), null);
        engine.Recalculate([At("A1")]);

        Assert.Equal(EmptyValue.Instance, engine.ValueOf(At("A1")));
        Assert.Equal(new NumberValue(1), engine.ValueOf(At("B1")));
    }

    /// <summary>Several edits at once (a paste) are recalculated together, each reached cell once.</summary>
    [Fact]
    public void SeveralEdits_AreRecalculatedTogether()
    {
        RecalculationEngine engine = Engine(null, ("A1", "1"), ("A2", "1"), ("B1", "=A1+A2"));

        engine.Define(At("A1"), Def("2"));
        engine.Define(At("A2"), Def("3"));
        IReadOnlyList<CellAddress> changed = engine.Recalculate([At("A1"), At("A2")]);

        Assert.Equal(new NumberValue(5), engine.ValueOf(At("B1")));
        Assert.Equal(3, changed.Count);
    }

    /// <summary>ACTUAL reads the engine's actual source.</summary>
    [Fact]
    public void ACTUAL_ReadsTheActualSource()
    {
        RecalculationEngine engine = Engine(a => a.Column == "B" ? 40m : null, ("C1", "=ACTUAL(B1)"));

        Assert.Equal(new NumberValue(40), engine.ValueOf(At("C1")));
    }

    /// <summary>A formula can be evaluated against the current values without being defined (the editor's preview), with a cycle shown.</summary>
    [Fact]
    public void Preview_EvaluatesWithoutDefining()
    {
        RecalculationEngine engine = Engine(null, ("A1", "5"), ("B1", "=A1+1"));

        Assert.Equal(new NumberValue(12), engine.Preview(At("C1"), Bind("=B1*2")));
        Assert.Equal(new ErrorValue(FormulaErrorKind.Cycle), engine.Preview(At("A1"), Bind("=B1")));
        Assert.Equal(EmptyValue.Instance, engine.ValueOf(At("C1")));
    }

    /// <summary>A normalizer turns a cell's evaluated value into the value the cell shows and its dependents read (e.g. #NUM! for a negative budget).</summary>
    [Fact]
    public void TheNormalizer_AppliesToTheCellAndItsReaders()
    {
        var engine = new RecalculationEngine(_ => null, (cell, value) => cell.Column == "B" && value is NumberValue { Value: < 0 } ? new ErrorValue(FormulaErrorKind.Num) : value);
        engine.Define(At("B1"), Def("=0-5"));
        engine.Define(At("C1"), Def("=B1"));
        engine.Define(At("D1"), Def("=0-5"));
        engine.RecalculateAll();

        Assert.Equal(new ErrorValue(FormulaErrorKind.Num), engine.ValueOf(At("C1")));
        Assert.Equal(new NumberValue(-5), engine.ValueOf(At("D1")));
    }
}

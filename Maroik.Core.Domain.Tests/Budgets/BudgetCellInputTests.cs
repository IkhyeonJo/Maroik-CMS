using Maroik.Core.Domain.Budgets;
using Maroik.Core.Domain.Formulas;
using Maroik.Core.Domain.Tests.Fixtures;
using static Maroik.Core.Domain.Tests.Fixtures.FormulaFixture;

namespace Maroik.Core.Domain.Tests.Budgets;

/// <summary>
/// Unit tests for <see cref="BudgetCellInput"/>: what a cell accepts by its role (a budget cell a number or formula, a free cell
/// anything, the computed and label cells nothing), the stored form of the input, and the cell definition it becomes.
/// </summary>
public class BudgetCellInputTests
{
    /// <summary>A number is stored in its plain invariant form (grouping commas dropped, trailing zeros too).</summary>
    [Theory]
    [InlineData("1,234.50", "1234.5")]
    [InlineData(" 300000 ", "300000")]
    [InlineData("-5", "-5")]
    [InlineData("0", "0")]
    public void ANumber_IsStoredPlain(string input, string stored)
    {
        Assert.Equal(stored, BudgetCellInput.Parse(BudgetCellRole.Budget, input, Scope).Value);
        Assert.Equal(stored, BudgetCellInput.Parse(BudgetCellRole.Free, input, Scope).Value);
    }

    /// <summary>A formula is stored with row keys; an empty input clears the cell.</summary>
    [Fact]
    public void AFormula_IsStoredCanonical_AndEmptyClears()
    {
        Assert.Equal("=B{R2}*1.05", BudgetCellInput.Parse(BudgetCellRole.Budget, "=B2*1.05", Scope).Value);
        Assert.Null(BudgetCellInput.Parse(BudgetCellRole.Budget, "  ", Scope).Value);
        Assert.Null(BudgetCellInput.Parse(BudgetCellRole.Free, null, Scope).Value);
    }

    /// <summary>A formula Excel would refuse is refused with its first problem; one with only warnings (#NAME?, #REF!) is stored.</summary>
    [Fact]
    public void AFormulaWithAnError_IsRefused()
    {
        Assert.Equal("Formula.OperandExpected", BudgetCellInput.Parse(BudgetCellRole.Budget, "=1+", Scope).FirstError.Code);
        Assert.Equal("Formula.ArgumentCount", BudgetCellInput.Parse(BudgetCellRole.Free, "=ROUND(1)", Scope).FirstError.Code);
        Assert.Equal("=FOO(1)+#REF!", BudgetCellInput.Parse(BudgetCellRole.Free, "=FOO(1)+B99", Scope).Value);
    }

    /// <summary>A budget cell refuses text and booleans; a free cell keeps text as typed and reads TRUE / FALSE as booleans.</summary>
    [Fact]
    public void Text_IsForFreeCellsOnly()
    {
        ErrorAssert.Validation(BudgetCellInput.Parse(BudgetCellRole.Budget, "lunch", Scope), "Budget.CellNeedsNumber", "A budget cell takes a number or a formula.");
        ErrorAssert.Validation(BudgetCellInput.Parse(BudgetCellRole.Budget, "TRUE", Scope), "Budget.CellNeedsNumber", "A budget cell takes a number or a formula.");
        Assert.Equal("Tax rate", BudgetCellInput.Parse(BudgetCellRole.Free, "Tax rate", Scope).Value);
        Assert.Equal("TRUE", BudgetCellInput.Parse(BudgetCellRole.Free, "true", Scope).Value);
    }

    /// <summary>Label, computed and blank cells cannot be edited.</summary>
    [Theory]
    [InlineData(BudgetCellRole.Label)]
    [InlineData(BudgetCellRole.Computed)]
    [InlineData(BudgetCellRole.Blank)]
    public void ReadOnlyCells_RefuseInput(BudgetCellRole role) =>
        ErrorAssert.Validation(BudgetCellInput.Parse(role, "1", Scope), "Budget.CellReadOnly", "This cell cannot be edited.");

    /// <summary>An input longer than the limit is refused.</summary>
    [Fact]
    public void ALongInput_IsRefused()
    {
        ErrorAssert.Validation(BudgetCellInput.Parse(BudgetCellRole.Free, new string('a', BudgetCellInput.MaxLength + 1), Scope),
            "Budget.InputTooLong", "A cell can hold at most {0} characters.", BudgetCellInput.MaxLength);
        Assert.False(BudgetCellInput.Parse(BudgetCellRole.Free, new string('a', BudgetCellInput.MaxLength), Scope).IsError);
    }

    /// <summary>A stored input becomes its definition: a bound formula, a number, a boolean or text.</summary>
    [Fact]
    public void Define_MakesTheCellDefinition()
    {
        Assert.Equal([At("B2")], BudgetCellInput.Define("=B{R2}+1", Scope).Formula!.Precedents);
        Assert.Equal(new NumberValue(1234.5m), BudgetCellInput.Define("1234.5", Scope).Constant);
        Assert.Equal(new BooleanValue(false), BudgetCellInput.Define("FALSE", Scope).Constant);
        Assert.Equal(new TextValue("Tax rate"), BudgetCellInput.Define("Tax rate", Scope).Constant);
    }
}

using Maroik.Core.Domain.Formulas;
using static Maroik.Core.Domain.Tests.Fixtures.FormulaFixture;

namespace Maroik.Core.Domain.Tests.Formulas;

/// <summary>
/// Unit tests for <see cref="FormulaEvaluator"/>: Excel's value rules (coercion, comparison, error propagation), decimal arithmetic,
/// and the functions of the catalog.
/// </summary>
public class FormulaEvaluatorTests
{
    /// <summary>A number value.</summary>
    private static NumberValue N(decimal value) => new(value);

    /// <summary>A text value.</summary>
    private static TextValue T(string value) => new(value);

    /// <summary>A boolean value.</summary>
    private static BooleanValue B(bool value) => new(value);

    /// <summary>An error value.</summary>
    private static ErrorValue E(FormulaErrorKind kind) => new(kind);

    /// <summary>Arithmetic with Excel's precedence, in decimals (0.1 + 0.2 is exactly 0.3).</summary>
    [Theory]
    [InlineData("=1+2*3", 7)]
    [InlineData("=-2^2", 4)]
    [InlineData("=2^3^2", 64)]
    [InlineData("=0.1+0.2", 0.3)]
    [InlineData("=10/4", 2.5)]
    [InlineData("=50%", 0.5)]
    [InlineData("=2^-1", 0.5)]
    [InlineData("=-(3-5)", 2)]
    [InlineData("=+3", 3)]
    public void Arithmetic_IsExcels(string input, double expected) => Assert.Equal(N((decimal)expected), Evaluate(input));

    /// <summary>A fractional power is computed in double precision and comes back as a decimal.</summary>
    [Fact]
    public void AFractionalPower_IsApproximate()
    {
        NumberValue value = Assert.IsType<NumberValue>(Evaluate("=2^0.5"));

        Assert.Equal(1.41421356, (double)value.Value, 8);
    }

    /// <summary>Division by zero, overflow and impossible powers are errors.</summary>
    [Theory]
    [InlineData("=1/0", FormulaErrorKind.DivideByZero)]
    [InlineData("=1E28*1E28", FormulaErrorKind.Num)]
    [InlineData("=7.9E28+7.9E28", FormulaErrorKind.Num)]
    [InlineData("=0^0", FormulaErrorKind.Num)]
    [InlineData("=(-8)^(1/3)", FormulaErrorKind.Num)]
    [InlineData("=10^40", FormulaErrorKind.Num)]
    [InlineData("=0^-1", FormulaErrorKind.DivideByZero)]
    public void ImpossibleArithmetic_IsAnError(string input, FormulaErrorKind error) => Assert.Equal(E(error), Evaluate(input));

    /// <summary>Operands are coerced as Excel does: numeric text and booleans to numbers, an empty cell to 0; other text is #VALUE!.</summary>
    [Fact]
    public void Operands_AreCoerced()
    {
        Assert.Equal(N(4), Evaluate("=\"3\"+1"));
        Assert.Equal(N(2), Evaluate("=TRUE+1"));
        Assert.Equal(N(1), Evaluate("=B9+1"));
        Assert.Equal(N(-5), Evaluate("=-B2", new Dictionary<string, FormulaValue> { ["B2"] = T("5") }));
        Assert.Equal(E(FormulaErrorKind.Value), Evaluate("=\"a\"+1"));
        Assert.Equal(E(FormulaErrorKind.Value), Evaluate("=-\"a\""));
        Assert.Equal(E(FormulaErrorKind.Value), Evaluate("=\"a\"%"));
    }

    /// <summary>Concatenation turns values into text: a number without trailing zeros, booleans as TRUE / FALSE, empty as "".</summary>
    [Fact]
    public void Concatenation_MakesText()
    {
        Assert.Equal(T("a1.5TRUE"), Evaluate("=\"a\"&1.50&TRUE"));
        Assert.Equal(T("x"), Evaluate("=\"x\"&B9"));
        Assert.Equal(T("-0.25FALSE"), Evaluate("=-0.25&FALSE"));
    }

    /// <summary>Comparisons: same types by value (text ignoring case), numbers below text below booleans, empty as 0 or "".</summary>
    [Theory]
    [InlineData("=1<2", true)]
    [InlineData("=2<=2", true)]
    [InlineData("=3>=4", false)]
    [InlineData("=1<>1", false)]
    [InlineData("=\"a\"=\"A\"", true)]
    [InlineData("=\"b\">\"A\"", true)]
    [InlineData("=1<\"a\"", true)]
    [InlineData("=\"a\"<TRUE", true)]
    [InlineData("=FALSE<TRUE", true)]
    [InlineData("=B9=0", true)]
    [InlineData("=B9=\"\"", true)]
    [InlineData("=B9=FALSE", true)]
    [InlineData("=TRUE>1", true)]
    public void Comparisons_AreExcels(string input, bool expected) => Assert.Equal(B(expected), Evaluate(input));

    /// <summary>An error operand wins, the left one first.</summary>
    [Fact]
    public void Errors_Propagate()
    {
        Assert.Equal(E(FormulaErrorKind.Ref), Evaluate("=#REF!+1/0"));
        Assert.Equal(E(FormulaErrorKind.DivideByZero), Evaluate("=1/0&\"a\""));
        Assert.Equal(E(FormulaErrorKind.NotAvailable), Evaluate("=#N/A=1"));
        Assert.Equal(E(FormulaErrorKind.Cycle), Evaluate("=B2*2", new Dictionary<string, FormulaValue> { ["B2"] = E(FormulaErrorKind.Cycle) }));
    }

    /// <summary>References read cell values; a range where one value is needed is #VALUE!; an out-of-range reference is #REF!.</summary>
    [Fact]
    public void References_ReadCellValues()
    {
        var cells = new Dictionary<string, FormulaValue> { ["B2"] = N(5), ["T!C3"] = T("x") };

        Assert.Equal(N(5), Evaluate("=B2", cells));
        Assert.Equal(T("x"), Evaluate("=T!C3", cells));
        Assert.Equal(EmptyValue.Instance, Evaluate("=B3", cells));
        Assert.Equal(E(FormulaErrorKind.Value), Evaluate("=B1:B2", cells));
        Assert.Equal(E(FormulaErrorKind.Ref), Evaluate("=B11+1", cells));
        Assert.Equal(E(FormulaErrorKind.Ref), Evaluate("=SUM(Other!B1)", cells));
    }

    /// <summary>Unknown functions and names are #NAME?.</summary>
    [Fact]
    public void UnknownNames_AreNameErrors()
    {
        Assert.Equal(E(FormulaErrorKind.Name), Evaluate("=FOO(1)"));
        Assert.Equal(E(FormulaErrorKind.Name), Evaluate("=rate*2"));
    }

    /// <summary>SUM adds numbers: in ranges only numbers count (text, booleans, empty are skipped); direct arguments are coerced.</summary>
    [Fact]
    public void SUM_AddsNumbers()
    {
        var cells = new Dictionary<string, FormulaValue> { ["B1"] = N(1), ["B2"] = T("2"), ["B3"] = B(true), ["B4"] = N(4) };

        Assert.Equal(N(5), Evaluate("=SUM(B1:B5)", cells));
        Assert.Equal(N(10), Evaluate("=SUM(B1:B5, \"3\", TRUE, 1)", cells));
        Assert.Equal(E(FormulaErrorKind.Value), Evaluate("=SUM(\"a\")"));
        Assert.Equal(E(FormulaErrorKind.DivideByZero), Evaluate("=SUM(B1:B2)", new Dictionary<string, FormulaValue> { ["B1"] = E(FormulaErrorKind.DivideByZero) }));
        Assert.Equal(E(FormulaErrorKind.Num), Evaluate("=SUM(7.9E28, 7.9E28)"));
        Assert.Equal(N(0), Evaluate("=SUM(B9)"));
    }

    /// <summary>AVERAGE, MIN and MAX over the numbers; AVERAGE of nothing is #DIV/0!, MIN / MAX of nothing 0.</summary>
    [Fact]
    public void AVERAGE_MIN_MAX()
    {
        var cells = new Dictionary<string, FormulaValue> { ["B1"] = N(2), ["B2"] = N(6), ["B3"] = T("x") };

        Assert.Equal(N(4), Evaluate("=AVERAGE(B1:B3)", cells));
        Assert.Equal(N(2), Evaluate("=MIN(B1:B3)", cells));
        Assert.Equal(N(9), Evaluate("=MAX(B1:B3, 9)", cells));
        Assert.Equal(E(FormulaErrorKind.DivideByZero), Evaluate("=AVERAGE(C1:C3)"));
        Assert.Equal(N(0), Evaluate("=MIN(C1:C3)"));
        Assert.Equal(N(0), Evaluate("=MAX(C1:C3)"));
    }

    /// <summary>COUNT counts numbers: in ranges only numbers, among direct arguments also numeric text and booleans; errors never.</summary>
    [Fact]
    public void COUNT_CountsNumbers()
    {
        var cells = new Dictionary<string, FormulaValue> { ["B1"] = N(2), ["B2"] = T("3"), ["B3"] = E(FormulaErrorKind.Value), ["B4"] = B(true) };

        Assert.Equal(N(1), Evaluate("=COUNT(B1:B5)", cells));
        Assert.Equal(N(3), Evaluate("=COUNT(\"3\", TRUE, 1, \"a\", 1/0)", cells));
    }

    /// <summary>ROUND rounds half away from zero; ROUNDUP away from zero; ROUNDDOWN toward zero; digits may be negative and are truncated.</summary>
    [Theory]
    [InlineData("=ROUND(2.5,0)", 3)]
    [InlineData("=ROUND(-2.5,0)", -3)]
    [InlineData("=ROUND(1234,-2)", 1200)]
    [InlineData("=ROUND(1.25,1.9)", 1.3)]
    [InlineData("=ROUND(1.5,)", 2)]
    [InlineData("=ROUNDUP(1.21,1)", 1.3)]
    [InlineData("=ROUNDUP(-1.21,1)", -1.3)]
    [InlineData("=ROUNDUP(1201,-2)", 1300)]
    [InlineData("=ROUNDDOWN(1.29,1)", 1.2)]
    [InlineData("=ROUNDDOWN(-1.29,1)", -1.2)]
    [InlineData("=ROUNDDOWN(1299,-2)", 1200)]
    [InlineData("=ABS(-3)", 3)]
    [InlineData("=ROUND(5,-40)", 0)]
    [InlineData("=ROUND(1.23456,40)", 1.23456)]
    public void Rounding_IsExcels(string input, double expected) => Assert.Equal(N((decimal)expected), Evaluate(input));

    /// <summary>A rounding argument that is not a number is #VALUE!; an error is passed on.</summary>
    [Fact]
    public void RoundingArguments_AreChecked()
    {
        Assert.Equal(E(FormulaErrorKind.Value), Evaluate("=ROUND(\"a\",1)"));
        Assert.Equal(E(FormulaErrorKind.Value), Evaluate("=ROUND(1,\"a\")"));
        Assert.Equal(E(FormulaErrorKind.DivideByZero), Evaluate("=ABS(1/0)"));
        Assert.Equal(E(FormulaErrorKind.Value), Evaluate("=ABS(B1:B2)"));
    }

    /// <summary>IF evaluates only the branch it takes; a missing "else" is FALSE; a condition that is not logical is #VALUE!.</summary>
    [Fact]
    public void IF_ChoosesABranch()
    {
        Assert.Equal(N(1), Evaluate("=IF(TRUE,1,1/0)"));
        Assert.Equal(N(2), Evaluate("=IF(0,1/0,2)"));
        Assert.Equal(B(false), Evaluate("=IF(B9,1)"));
        Assert.Equal(N(1), Evaluate("=IF(\"true\",1,2)"));
        Assert.Equal(E(FormulaErrorKind.Value), Evaluate("=IF(\"a\",1,2)"));
        Assert.Equal(E(FormulaErrorKind.DivideByZero), Evaluate("=IF(1/0,1,2)"));
        Assert.Equal(EmptyValue.Instance, Evaluate("=IF(TRUE,,2)"));
    }

    /// <summary>IFERROR replaces any error with its second argument.</summary>
    [Fact]
    public void IFERROR_CatchesErrors()
    {
        Assert.Equal(T("x"), Evaluate("=IFERROR(1/0,\"x\")"));
        Assert.Equal(N(5), Evaluate("=IFERROR(5,\"x\")"));
    }

    /// <summary>AND / OR over logical values (numbers count, text and empty cells in ranges are skipped); NOT inverts.</summary>
    [Fact]
    public void Logic_IsExcels()
    {
        var cells = new Dictionary<string, FormulaValue> { ["B1"] = B(true), ["B2"] = T("x"), ["B3"] = N(0) };

        Assert.Equal(B(true), Evaluate("=AND(TRUE,1)"));
        Assert.Equal(B(false), Evaluate("=AND(B1:B3)", cells));
        Assert.Equal(B(true), Evaluate("=OR(B1:B3)", cells));
        Assert.Equal(B(false), Evaluate("=OR(FALSE,0)"));
        Assert.Equal(E(FormulaErrorKind.Value), Evaluate("=AND(B2)", cells));
        Assert.Equal(E(FormulaErrorKind.Value), Evaluate("=OR(\"x\")"));
        Assert.Equal(E(FormulaErrorKind.DivideByZero), Evaluate("=AND(1/0)"));
        Assert.Equal(B(true), Evaluate("=NOT(0)"));
        Assert.Equal(E(FormulaErrorKind.Value), Evaluate("=NOT(\"x\")"));
    }

    /// <summary>ACTUAL reads the actual total of the budget cells it names; a range sums them.</summary>
    [Fact]
    public void ACTUAL_ReadsTheActualTotal()
    {
        var actuals = new Dictionary<string, decimal> { ["B1"] = 100m, ["C1"] = 50m };

        Assert.Equal(N(100), Evaluate("=ACTUAL(B1)", actuals: actuals));
        Assert.Equal(N(150), Evaluate("=ACTUAL(B1:C1)", actuals: actuals));
    }

    /// <summary>ACTUAL of something that is not a budget cell, or not a reference at all, is #VALUE!; of a #REF! reference, #REF!.</summary>
    [Fact]
    public void ACTUAL_NeedsBudgetCells()
    {
        var actuals = new Dictionary<string, decimal> { ["B1"] = 100m };

        Assert.Equal(E(FormulaErrorKind.Value), Evaluate("=ACTUAL(B2)", actuals: actuals));
        Assert.Equal(E(FormulaErrorKind.Value), Evaluate("=ACTUAL(B1:B2)", actuals: actuals));
        Assert.Equal(E(FormulaErrorKind.Value), Evaluate("=ACTUAL(1)", actuals: actuals));
        Assert.Equal(E(FormulaErrorKind.Ref), Evaluate("=ACTUAL(B11)", actuals: actuals));
    }

    /// <summary>An error literal is its error; text and booleans are themselves.</summary>
    [Fact]
    public void Literals_AreThemselves()
    {
        Assert.Equal(E(FormulaErrorKind.NotAvailable), Evaluate("=#N/A"));
        Assert.Equal(T("a"), Evaluate("=\"a\""));
        Assert.Equal(B(true), Evaluate("=TRUE"));
    }

    /// <summary>A formula with a syntax error evaluates to #VALUE! (it is never saved; this is only the preview).</summary>
    [Fact]
    public void ASyntaxError_IsAValueError() => Assert.Equal(E(FormulaErrorKind.Value), Evaluate("=1+"));

    /// <summary>A formula with a wrong argument count evaluates to #VALUE! (only the preview sees it).</summary>
    [Fact]
    public void AWrongArgumentCount_IsAValueError() => Assert.Equal(E(FormulaErrorKind.Value), Evaluate("=ROUND(1)"));
}

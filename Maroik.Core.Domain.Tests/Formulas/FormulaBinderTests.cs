using Maroik.Core.Domain.Formulas;
using static Maroik.Core.Domain.Tests.Fixtures.FormulaFixture;

namespace Maroik.Core.Domain.Tests.Formulas;

/// <summary>
/// Unit tests for <see cref="FormulaBinder"/>: references resolved to cell addresses (sheet, row key, column), functions checked
/// against the catalog, and the problems split into errors (the formula cannot be saved, as Excel refuses it) and warnings
/// (it is saved and shows #NAME? / #REF!, as in Excel).
/// </summary>
public class FormulaBinderTests
{
    /// <summary>A reference resolves to its sheet's row key at that row number.</summary>
    [Fact]
    public void AReference_ResolvesToItsAddress()
    {
        BoundFormula bound = Bind("=B2+T!C3+t!$D$4");

        Assert.Equal([At("B2"), At("T!C3"), At("T!D4")], bound.Precedents);
        Assert.Empty(bound.Errors);
        Assert.Empty(bound.Warnings);
        Assert.True(bound.CanSave);
    }

    /// <summary>A range is every cell between its corners, row by row, whichever corner is written first.</summary>
    [Fact]
    public void ARange_ExpandsRowByRow()
    {
        Assert.Equal([At("B1"), At("C1"), At("B2"), At("C2")], Bind("=SUM(B1:C2)").Precedents);
        Assert.Equal([At("B1"), At("C1"), At("B2"), At("C2")], Bind("=SUM(C2:B1)").Precedents);
        Assert.Equal([At("T!B1"), At("T!B2")], Bind("=SUM(T!B1:B2)").Precedents);
    }

    /// <summary>The same cell named twice is one precedent.</summary>
    [Fact]
    public void Precedents_AreDistinct() => Assert.Equal([At("B2")], Bind("=B2+B2*2").Precedents);

    /// <summary>ACTUAL reads a cell's identity, not its value, so its reference is not a precedent.</summary>
    [Fact]
    public void ACTUALsArgument_IsNotAPrecedent() => Assert.Equal([At("C1")], Bind("=ACTUAL(B1:B2)+C1").Precedents);

    /// <summary>A row past the sheet, a column past Z or an unknown sheet is #REF!: a warning, the formula is still saved.</summary>
    [Theory]
    [InlineData("=B11", "Formula.RowOutOfRange")]
    [InlineData("=AA1", "Formula.ColumnOutOfRange")]
    [InlineData("=Other!B1", "Formula.UnknownSheet")]
    [InlineData("=SUM(B1:B11)", "Formula.RowOutOfRange")]
    public void AReferenceOutsideTheWorkbook_IsAWarning(string input, string code)
    {
        BoundFormula bound = Bind(input);

        FormulaDiagnostic warning = Assert.Single(bound.Warnings);
        Assert.Equal(code, warning.Error.Code);
        Assert.Empty(bound.Precedents);
        Assert.True(bound.CanSave);
    }

    /// <summary>The warning's span is the reference's, so the editor underlines exactly it.</summary>
    [Fact]
    public void AWarning_IsAtTheReferencesSpan()
    {
        FormulaDiagnostic warning = Bind("=1+Other!B1").Warnings.Single();

        Assert.Equal((3, 8), (warning.Span.Start, warning.Span.Length));
    }

    /// <summary>An unknown function or name is #NAME?: a warning at its span.</summary>
    [Fact]
    public void AnUnknownFunctionOrName_IsAWarning()
    {
        BoundFormula bound = Bind("=FOO(1)+rate");

        Assert.Equal(["Formula.UnknownFunction", "Formula.UnknownName"], bound.Warnings.Select(w => w.Error.Code));
        Assert.Equal((1, 3), (bound.Warnings[0].Span.Start, bound.Warnings[0].Span.Length));
        Assert.True(bound.CanSave);
    }

    /// <summary>A wrong number of arguments is an error (Excel refuses such a formula), naming the function and its counts.</summary>
    [Theory]
    [InlineData("=ROUND(1)", "ROUND", 2, 2)]
    [InlineData("=ABS(1,2)", "ABS", 1, 1)]
    [InlineData("=IF(1)", "IF", 2, 3)]
    [InlineData("=SUM()", "SUM", 1, 255)]
    public void AWrongArgumentCount_IsAnError(string input, string name, int min, int max)
    {
        BoundFormula bound = Bind(input);

        FormulaDiagnostic error = Assert.Single(bound.Errors);
        Assert.Equal("Formula.ArgumentCount", error.Error.Code);
        Assert.Equal("{0} takes {1} to {2} arguments.", error.Error.Metadata![Maroik.Core.Domain.Errors.DomainError.MessageTemplateMetadataKey]);
        Assert.Equal(new object[] { name, min, max }, (object[])error.Error.Metadata![Maroik.Core.Domain.Errors.DomainError.MessageArgsMetadataKey]);
        Assert.False(bound.CanSave);
    }

    /// <summary>Syntax problems are errors too.</summary>
    [Fact]
    public void SyntaxProblems_AreErrors()
    {
        BoundFormula bound = Bind("=1+");

        Assert.Equal("Formula.OperandExpected", Assert.Single(bound.Errors).Error.Code);
        Assert.False(bound.CanSave);
    }

    /// <summary>The stored form binds by row key; a key the sheet no longer has is #REF!.</summary>
    [Fact]
    public void TheCanonicalForm_BindsByKey()
    {
        BoundFormula bound = FormulaBinder.Bind(FormulaParser.Parse("=B{R3}+T!C{Gone}", canonical: true), Scope);

        Assert.Equal([At("B3")], bound.Precedents);
        Assert.Equal("Formula.RowOutOfRange", Assert.Single(bound.Warnings).Error.Code);
    }

    /// <summary>The catalog lists every function with its argument hint, for the editor's autocomplete.</summary>
    [Fact]
    public void TheCatalog_ListsTheFunctions()
    {
        Assert.Equal(["SUM", "AVERAGE", "MIN", "MAX", "COUNT", "ROUND", "ROUNDUP", "ROUNDDOWN", "ABS", "IF", "IFERROR", "AND", "OR", "NOT", "ACTUAL"],
            FormulaFunctions.All.Select(f => f.Name));
        Assert.Equal("ROUND(number, num_digits)", FormulaFunctions.Find("round")!.Signature);
        Assert.Null(FormulaFunctions.Find("FOO"));
    }
}

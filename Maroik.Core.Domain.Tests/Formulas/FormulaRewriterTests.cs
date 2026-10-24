using Maroik.Core.Domain.Formulas;
using static Maroik.Core.Domain.Tests.Fixtures.FormulaFixture;

namespace Maroik.Core.Domain.Tests.Formulas;

/// <summary>
/// Unit tests for <see cref="FormulaRewriter"/>: a formula between the A1 form the user types and the stored form with stable row keys,
/// and the shift of relative references when a formula is copied (Excel's paste / fill). Only references change: spacing and the
/// rest of the text stay as typed, function names are upper-cased (as Excel does).
/// </summary>
public class FormulaRewriterTests
{
    /// <summary>Rows become keys on the way in and numbers on the way out; the rest of the text is kept as typed.</summary>
    [Fact]
    public void A1_AndCanonical_RoundTrip()
    {
        string canonical = FormulaRewriter.ToCanonical("=sum( B2:$C$3 ) + T!D4*2", Scope);

        Assert.Equal("=SUM( B{R2}:$C${R3} ) + T!D{R4}*2", canonical);
        Assert.Equal("=SUM( B2:$C$3 ) + T!D4*2", FormulaRewriter.ToA1(canonical, Scope));
    }

    /// <summary>Text inside quotes is never rewritten.</summary>
    [Fact]
    public void TextLiterals_AreLeftAlone() =>
        Assert.Equal("=\"B2\"&B{R2}", FormulaRewriter.ToCanonical("=\"B2\"&B2", Scope));

    /// <summary>A reference outside the workbook is stored as #REF! (Excel writes #REF! into the formula), the whole range or sheet reference.</summary>
    [Theory]
    [InlineData("=B11+1", "=#REF!+1")]
    [InlineData("=SUM(B1:B11)", "=SUM(#REF!)")]
    [InlineData("=Other!B1", "=#REF!")]
    [InlineData("=AA1", "=#REF!")]
    public void AReferenceOutsideTheWorkbook_IsStoredAsRef(string a1, string canonical) =>
        Assert.Equal(canonical, FormulaRewriter.ToCanonical(a1, Scope));

    /// <summary>A key the sheet no longer has (its row was removed) is shown as #REF!.</summary>
    [Fact]
    public void AMissingKey_IsShownAsRef() =>
        Assert.Equal("=#REF!*2+B3", FormulaRewriter.ToA1("=B{Gone}*2+B{R3}", Scope));

    /// <summary>Input that is not a formula is returned as it is.</summary>
    [Theory]
    [InlineData("1234")]
    [InlineData("hello B2")]
    [InlineData("")]
    public void NonFormulas_AreUnchanged(string input)
    {
        Assert.Equal(input, FormulaRewriter.ToCanonical(input, Scope));
        Assert.Equal(input, FormulaRewriter.ToA1(input, Scope));
    }

    /// <summary>Copying moves relative parts by the offset and keeps the absolute ones (Excel's paste).</summary>
    [Theory]
    [InlineData("=B{R2}", 1, 1, "=C{R3}")]
    [InlineData("=$B{R2}", 1, 1, "=$B{R3}")]
    [InlineData("=B${R2}", 1, 1, "=C${R2}")]
    [InlineData("=$B${R2}", 3, 3, "=$B${R2}")]
    [InlineData("=SUM(B{R2}:C{R3})*2", 0, 2, "=SUM(D{R2}:E{R3})*2")]
    [InlineData("=T!B{R1}", 2, 0, "=T!B{R3}")]
    public void Shift_MovesRelativeParts(string canonical, int rows, int columns, string shifted) =>
        Assert.Equal(shifted, FormulaRewriter.Shift(canonical, Scope, rows, columns));

    /// <summary>A reference moved off the sheet becomes #REF!, as in Excel.</summary>
    [Theory]
    [InlineData("=B{R1}+1", -1, 0, "=#REF!+1")]
    [InlineData("=B{R10}", 1, 0, "=#REF!")]
    [InlineData("=A{R1}", 0, -1, "=#REF!")]
    [InlineData("=Z{R1}", 0, 1, "=#REF!")]
    [InlineData("=SUM(B{R9}:B{R10})", 1, 0, "=SUM(#REF!)")]
    [InlineData("=B{Gone}", 1, 0, "=#REF!")]
    public void Shift_OffTheSheet_IsRef(string canonical, int rows, int columns, string shifted) =>
        Assert.Equal(shifted, FormulaRewriter.Shift(canonical, Scope, rows, columns));

    /// <summary>A value (not a formula) is copied as it is.</summary>
    [Fact]
    public void Shift_LeavesValuesAlone() => Assert.Equal("B2", FormulaRewriter.Shift("B2", Scope, 1, 1));

    /// <summary>A sheet name that does not resolve stays #REF! when shifted.</summary>
    [Fact]
    public void Shift_OfAnUnknownSheet_IsRef() => Assert.Equal("=#REF!", FormulaRewriter.Shift("=Other!B{R1}", Scope, 1, 0));
}

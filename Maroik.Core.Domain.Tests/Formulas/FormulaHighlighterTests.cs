using Maroik.Core.Domain.Formulas;
using static Maroik.Core.Domain.Tests.Fixtures.FormulaFixture;

namespace Maroik.Core.Domain.Tests.Formulas;

/// <summary>
/// Unit tests for <see cref="FormulaHighlighter"/>: what the editor colours while a formula is typed — every token's class, each
/// distinct reference's colour index and the cells it outlines, the partner of each parenthesis, and the problems with their spans.
/// </summary>
public class FormulaHighlighterTests
{
    /// <summary>The classes of the tokens, the "=" included.</summary>
    [Fact]
    public void Tokens_AreClassified()
    {
        FormulaHighlight highlight = FormulaHighlighter.Highlight("=SUM(B2, \"a\", TRUE, 1) & FOO(x) + #N/A ?", Scope);

        Assert.Equal(["operator", "function", "paren", "reference", "comma", "string", "comma", "boolean", "comma", "number", "paren", "operator",
            "unknown-function", "paren", "name", "paren", "operator", "error-literal", "error"], highlight.Tokens.Select(t => t.Class));
    }

    /// <summary>Each distinct reference gets the next colour; the same reference written again keeps its colour; a range is one reference.</summary>
    [Fact]
    public void References_GetColoursInOrder()
    {
        FormulaHighlight highlight = FormulaHighlighter.Highlight("=B2+C3:D4*b2+T!B2", Scope);

        Assert.Equal([0, 1, 1, 0, 2, 2], highlight.Tokens.Where(t => t.Class is "reference" or "sheet").Select(t => t.Reference));
        Assert.Equal(3, highlight.References.Count);
    }

    /// <summary>A reference to the home sheet outlines its cells (as A1 corners); one to another sheet does not.</summary>
    [Fact]
    public void HomeSheetReferences_OutlineTheirCells()
    {
        FormulaHighlight highlight = FormulaHighlighter.Highlight("=SUM(C3:B2)+T!B2+B99", Scope);

        Assert.Equal(("B2", "C3"), (highlight.References[0].TopLeft, highlight.References[0].BottomRight));
        Assert.Null(highlight.References[1].TopLeft);
        Assert.Null(highlight.References[2].TopLeft);
    }

    /// <summary>Each parenthesis knows its partner's token index; an unmatched one has none.</summary>
    [Fact]
    public void Parentheses_KnowTheirPartners()
    {
        FormulaHighlight highlight = FormulaHighlighter.Highlight("=((1)+2", Scope);

        Assert.Equal([null, null, 4, null, 2, null, null], highlight.Tokens.Select(t => t.Partner));
    }

    /// <summary>Problems carry their span and whether they block saving.</summary>
    [Fact]
    public void Problems_AreListed()
    {
        FormulaHighlight highlight = FormulaHighlighter.Highlight("=ROUND(1)+FOO(2)", Scope);

        Assert.Equal([("Formula.ArgumentCount", true), ("Formula.UnknownFunction", false)], highlight.Problems.Select(p => (p.Diagnostic.Error.Code, p.Blocking)));
        Assert.False(highlight.CanSave);
    }

    /// <summary>Text that is not a formula has no tokens.</summary>
    [Fact]
    public void AValue_HasNoTokens()
    {
        FormulaHighlight highlight = FormulaHighlighter.Highlight("1234", Scope);

        Assert.Empty(highlight.Tokens);
        Assert.True(highlight.CanSave);
    }
}

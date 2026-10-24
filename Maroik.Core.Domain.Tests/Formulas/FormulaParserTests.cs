using Maroik.Core.Domain.Formulas;

namespace Maroik.Core.Domain.Tests.Formulas;

/// <summary>
/// Unit tests for <see cref="FormulaParser"/>: tokens to a syntax tree with Excel's operator precedence, and diagnostics with exact
/// spans for a formula that is still being typed. The parser never throws.
/// </summary>
public class FormulaParserTests
{
    /// <summary>Parses the formula text after its "=" (A1 form).</summary>
    private static FormulaSyntax Parse(string formula) => FormulaParser.Parse("=" + formula, canonical: false);

    /// <summary>A compact rendering of a tree, fully parenthesised, to check the shape and precedence.</summary>
    private static string Show(FormulaNode node) => node switch
    {
        NumberNode n => n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        TextNode t => $"\"{t.Value}\"",
        BooleanNode b => b.Value ? "TRUE" : "FALSE",
        ErrorLiteralNode e => $"#{e.Error}",
        ReferenceNode r => (r.Sheet is null ? "" : r.Sheet + "!") + r.Cell.Column + r.Cell.RowNumber,
        RangeNode g => Show(g.Start) + ":" + Show(g.End),
        UnaryNode u => $"({u.Operator}{Show(u.Operand)})",
        PercentNode p => $"({Show(p.Operand)}%)",
        BinaryNode b => $"({Show(b.Left)}{b.Operator}{Show(b.Right)})",
        CallNode c => $"{c.Name}[{string.Join(";", c.Arguments.Select(Show))}]",
        NameNode n => $"name:{n.Name}",
        MissingNode => "_",
        InvalidNode => "?",
        _ => throw new ArgumentOutOfRangeException(nameof(node)),
    };

    /// <summary>The tree of a formula that parses without diagnostics.</summary>
    private static string Tree(string formula)
    {
        FormulaSyntax syntax = Parse(formula);
        Assert.Empty(syntax.Diagnostics);
        return Show(syntax.Root);
    }

    /// <summary>Excel's precedence: negation, percent, power, multiply / divide, add / subtract, concatenate, compare.</summary>
    [Theory]
    [InlineData("1+2*3", "(1+(2*3))")]
    [InlineData("(1+2)*3", "((1+2)*3)")]
    [InlineData("-2^2", "((-2)^2)")]
    [InlineData("2^3^2", "((2^3)^2)")]
    [InlineData("2^-1", "(2^(-1))")]
    [InlineData("50%*2", "((50%)*2)")]
    [InlineData("-50%", "(-(50%))")]
    [InlineData("1+2&3", "((1+2)&3)")]
    [InlineData("1&2=3", "((1&2)=3)")]
    [InlineData("1<2<>TRUE", "((1<2)<>TRUE)")]
    [InlineData("10-4-3", "((10-4)-3)")]
    [InlineData("8/4/2", "((8/4)/2)")]
    [InlineData("+3", "(+3)")]
    [InlineData("--3", "(-(-3))")]
    public void Operators_FollowExcelsPrecedence(string formula, string tree) => Assert.Equal(tree, Tree(formula));

    /// <summary>Literals, references, ranges, sheet references and error literals.</summary>
    [Theory]
    [InlineData("\"a\"", "\"a\"")]
    [InlineData("TRUE", "TRUE")]
    [InlineData("1.5E2", "150")]
    [InlineData("B2", "B2")]
    [InlineData("B2:M2", "B2:M2")]
    [InlineData("Income2025!N5", "Income2025!N5")]
    [InlineData("Expenditure!B2:B9", "Expenditure!B2:B9")]
    [InlineData("#REF!+1", "(#Ref+1)")]
    [InlineData("rate*2", "(name:RATE*2)")]
    public void Operands_AreRead(string formula, string tree) => Assert.Equal(tree, Tree(formula));

    /// <summary>A range's sheet comes from its start; the end has no sheet of its own.</summary>
    [Fact]
    public void ARangesSheet_IsItsStartsSheet()
    {
        RangeNode range = Assert.IsType<RangeNode>(Parse("Income2025!B2:C3").Root);

        Assert.Equal("Income2025", range.Start.Sheet);
        Assert.Null(range.End.Sheet);
    }

    /// <summary>Function calls with any number of arguments, nested; an empty argument is a missing node.</summary>
    [Theory]
    [InlineData("SUM(B2:M2)", "SUM[B2:M2]")]
    [InlineData("round(1.25,1)", "ROUND[1.25;1]")]
    [InlineData("IF(A1>0,SUM(B1,2),\"no\")", "IF[(A1>0);SUM[B1;2];\"no\"]")]
    [InlineData("ROUND(1,)", "ROUND[1;_]")]
    [InlineData("SUM()", "SUM[]")]
    public void Calls_AreRead(string formula, string tree) => Assert.Equal(tree, Tree(formula));

    /// <summary>Every node's span covers its text in the cell input (the "=" is index 0).</summary>
    [Fact]
    public void Spans_CoverTheNodesText()
    {
        BinaryNode root = Assert.IsType<BinaryNode>(Parse("SUM(B2:C3) * 2").Root);
        CallNode call = Assert.IsType<CallNode>(root.Left);

        Assert.Equal((1, 14), (root.Span.Start, root.Span.Length));
        Assert.Equal((1, 10), (call.Span.Start, call.Span.Length));
        Assert.Equal((1, 3), (call.NameSpan.Start, call.NameSpan.Length));
        Assert.Equal((5, 5), (call.Arguments[0].Span.Start, call.Arguments[0].Span.Length));
    }

    /// <summary>The tokens of the formula (for the editor's colouring) include the leading "=".</summary>
    [Fact]
    public void TheTokens_IncludeTheEqualsSign()
    {
        FormulaSyntax syntax = Parse("1+B2");

        Assert.Equal(["=", "1", "+", "B2"], syntax.Tokens.Select(t => t.Text));
        Assert.Equal(FormulaTokenKind.Operator, syntax.Tokens[0].Kind);
    }

    /// <summary>A formula still being typed is reported at the exact place, and the parser still builds a tree.</summary>
    [Theory]
    [InlineData("1+", "Formula.OperandExpected", 3, 0)]
    [InlineData("SUM(B2,", "Formula.CloseParenExpected", 8, 0)]
    [InlineData("(1+2", "Formula.CloseParenExpected", 5, 0)]
    [InlineData("1 2", "Formula.UnexpectedToken", 3, 1)]
    [InlineData("1)", "Formula.UnexpectedToken", 2, 1)]
    [InlineData("*2", "Formula.OperandExpected", 1, 1)]
    [InlineData("", "Formula.Empty", 1, 0)]
    [InlineData("B2:3", "Formula.RangeEndExpected", 4, 1)]
    [InlineData("1 ? 2", "Formula.UnexpectedCharacter", 3, 1)]
    public void Problems_AreDiagnosedAtTheirSpan(string formula, string code, int start, int length)
    {
        FormulaSyntax syntax = Parse(formula);

        FormulaDiagnostic diagnostic = syntax.Diagnostics[0];
        Assert.Equal(code, diagnostic.Error.Code);
        Assert.Equal((start, length), (diagnostic.Span.Start, diagnostic.Span.Length));
        Assert.NotNull(syntax.Root);
    }

    /// <summary>A sheet prefix with no reference after it is diagnosed.</summary>
    [Fact]
    public void ASheetPrefixWithoutAReference_IsDiagnosed() =>
        Assert.Equal("Formula.ReferenceExpected", Parse("Income!+1").Diagnostics[0].Error.Code);

    /// <summary>A number too large for a decimal is diagnosed instead of throwing.</summary>
    [Fact]
    public void ANumberTooLarge_IsDiagnosed() =>
        Assert.Equal("Formula.NumberTooLarge", Parse("1E40").Diagnostics[0].Error.Code);

    /// <summary>Nesting deeper than the limit is diagnosed (the work per formula is bounded).</summary>
    [Fact]
    public void NestingTooDeep_IsDiagnosed()
    {
        string formula = new string('(', FormulaParser.MaxDepth + 1) + "1" + new string(')', FormulaParser.MaxDepth + 1);

        Assert.Contains(Parse(formula).Diagnostics, d => d.Error.Code == "Formula.TooDeep");
        Assert.Empty(Parse(new string('(', FormulaParser.MaxDepth - 1) + "1" + new string(')', FormulaParser.MaxDepth - 1)).Diagnostics);
    }

    /// <summary>More arguments than Excel allows are diagnosed.</summary>
    [Fact]
    public void TooManyArguments_AreDiagnosed() =>
        Assert.Contains(Parse("SUM(" + string.Join(",", Enumerable.Repeat("1", FormulaParser.MaxArguments + 1)) + ")").Diagnostics,
            d => d.Error.Code == "Formula.TooManyArguments");

    /// <summary>The stored (canonical) form parses its row keys.</summary>
    [Fact]
    public void TheCanonicalForm_IsParsed()
    {
        FormulaSyntax syntax = FormulaParser.Parse("=SUM(B{F.1}:C{F.2})", canonical: true);

        RangeNode range = Assert.IsType<RangeNode>(Assert.IsType<CallNode>(syntax.Root).Arguments[0]);
        Assert.Equal(("F.1", "F.2"), (range.Start.Cell.RowKey, range.End.Cell.RowKey));
    }
}

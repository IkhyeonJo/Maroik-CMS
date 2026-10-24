using Maroik.Core.Domain.Formulas;

namespace Maroik.Core.Domain.Tests.Formulas;

/// <summary>
/// Unit tests for <see cref="FormulaLexer"/>: Excel formula text to tokens with their spans. The lexer never throws; text it cannot
/// read becomes an error token at the exact span, so the editor can colour and underline a formula that is still being typed.
/// </summary>
public class FormulaLexerTests
{
    /// <summary>The kinds of the tokens of <paramref name="text"/> (A1 mode, from index 0).</summary>
    private static FormulaTokenKind[] Kinds(string text) => [.. FormulaLexer.Tokenize(text, 0, canonical: false).Select(t => t.Kind)];

    /// <summary>Numbers, text and booleans are literals; a doubled quote inside text is one quote.</summary>
    [Fact]
    public void Literals_AreRead()
    {
        var tokens = FormulaLexer.Tokenize("12.5 + \"a\"\"b\" & TRUE & false", 0, canonical: false);

        Assert.Equal([FormulaTokenKind.Number, FormulaTokenKind.Operator, FormulaTokenKind.String, FormulaTokenKind.Operator,
            FormulaTokenKind.Boolean, FormulaTokenKind.Operator, FormulaTokenKind.Boolean], tokens.Select(t => t.Kind));
        Assert.Equal("12.5", tokens[0].Text);
        Assert.Equal("a\"b", tokens[2].Value);
        Assert.Equal("\"a\"\"b\"", tokens[2].Text);
    }

    /// <summary>A number may have a decimal part and an exponent (as Excel reads 1E3).</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("1234")]
    [InlineData(".5")]
    [InlineData("1.25")]
    [InlineData("1E3")]
    [InlineData("2.5e-2")]
    public void Numbers_AreOneToken(string text)
    {
        FormulaToken token = Assert.Single(FormulaLexer.Tokenize(text, 0, canonical: false));

        Assert.Equal(FormulaTokenKind.Number, token.Kind);
        Assert.Equal(text, token.Text);
    }

    /// <summary>Every span is absolute in the input, so a formula after its "=" starts at index 1.</summary>
    [Fact]
    public void Spans_AreAbsolute_AndSkipWhitespace()
    {
        var tokens = FormulaLexer.Tokenize("=SUM( B2 ,3)", 1, canonical: false);

        Assert.Equal([(1, 3), (4, 1), (6, 2), (9, 1), (10, 1), (11, 1)], tokens.Select(t => (t.Span.Start, t.Span.Length)));
        Assert.Equal([FormulaTokenKind.Function, FormulaTokenKind.OpenParen, FormulaTokenKind.Reference, FormulaTokenKind.Comma,
            FormulaTokenKind.Number, FormulaTokenKind.CloseParen], tokens.Select(t => t.Kind));
    }

    /// <summary>Every Excel operator, the two-character ones included.</summary>
    [Fact]
    public void Operators_AreRead()
    {
        var tokens = FormulaLexer.Tokenize("+-*/^&=<><=>=<>%", 0, canonical: false);

        Assert.All(tokens, t => Assert.Equal(FormulaTokenKind.Operator, t.Kind));
        Assert.Equal(["+", "-", "*", "/", "^", "&", "=", "<>", "<=", ">=", "<>", "%"], tokens.Select(t => t.Text));
    }

    /// <summary>A cell reference carries its column, row and the absolute markers; the column is upper-cased.</summary>
    [Fact]
    public void References_CarryTheirParts()
    {
        var tokens = FormulaLexer.Tokenize("b2 $C$10 D$3 $e4", 0, canonical: false);

        Assert.Equal([("B", false, 2, false), ("C", true, 10, true), ("D", false, 3, true), ("E", true, 4, false)],
            tokens.Select(t => (t.Reference!.Column, t.Reference.ColumnAbsolute, t.Reference.RowNumber!.Value, t.Reference.RowAbsolute)));
        Assert.All(tokens, t => Assert.Null(t.Reference!.RowKey));
    }

    /// <summary>A range is two references around the colon operator.</summary>
    [Fact]
    public void ARange_IsTwoReferencesAndAColon() =>
        Assert.Equal([FormulaTokenKind.Reference, FormulaTokenKind.Colon, FormulaTokenKind.Reference], Kinds("B2:M2"));

    /// <summary>A name followed by "!" is a sheet prefix; the reference after it is a separate token.</summary>
    [Fact]
    public void ASheetPrefix_IsItsOwnToken()
    {
        var tokens = FormulaLexer.Tokenize("Income2025!N5+expenditure!B2", 0, canonical: false);

        Assert.Equal([FormulaTokenKind.SheetPrefix, FormulaTokenKind.Reference, FormulaTokenKind.Operator, FormulaTokenKind.SheetPrefix,
            FormulaTokenKind.Reference], tokens.Select(t => t.Kind));
        Assert.Equal("Income2025", tokens[0].Value);
        Assert.Equal((0, 11), (tokens[0].Span.Start, tokens[0].Span.Length));
        Assert.Equal("expenditure", tokens[3].Value);
    }

    /// <summary>A name followed by "(" is a function (digits allowed, as LOG10); the name is upper-cased in the value.</summary>
    [Fact]
    public void ANameBeforeAParenthesis_IsAFunction()
    {
        var tokens = FormulaLexer.Tokenize("round(LOG10(", 0, canonical: false);

        Assert.Equal([FormulaTokenKind.Function, FormulaTokenKind.OpenParen, FormulaTokenKind.Function, FormulaTokenKind.OpenParen], tokens.Select(t => t.Kind));
        Assert.Equal(["ROUND", "LOG10"], new[] { tokens[0].Value, tokens[2].Value });
    }

    /// <summary>Any other name (not a reference, boolean or function) is a name token, which the binder reports as #NAME?.</summary>
    [Theory]
    [InlineData("rate")]
    [InlineData("tax_rate")]
    [InlineData("A")]
    public void AnUnknownWord_IsAName(string text) =>
        Assert.Equal([FormulaTokenKind.Name], Kinds(text));

    /// <summary>Excel's error literals are tokens of their own (a formula can contain #REF! after a removed row).</summary>
    [Theory]
    [InlineData("#REF!", FormulaErrorKind.Ref)]
    [InlineData("#DIV/0!", FormulaErrorKind.DivideByZero)]
    [InlineData("#VALUE!", FormulaErrorKind.Value)]
    [InlineData("#NAME?", FormulaErrorKind.Name)]
    [InlineData("#NUM!", FormulaErrorKind.Num)]
    [InlineData("#N/A", FormulaErrorKind.NotAvailable)]
    [InlineData("#cycle!", FormulaErrorKind.Cycle)]
    public void ErrorLiterals_AreRead(string text, FormulaErrorKind kind)
    {
        FormulaToken token = Assert.Single(FormulaLexer.Tokenize(text, 0, canonical: false));

        Assert.Equal(FormulaTokenKind.ErrorLiteral, token.Kind);
        Assert.Equal(kind, token.Value);
    }

    /// <summary>In canonical (stored) form a row is a stable key in braces instead of a number.</summary>
    [Fact]
    public void CanonicalReferences_CarryARowKey()
    {
        FormulaToken token = Assert.Single(FormulaLexer.Tokenize("$B{L.ConsumerSpending.PinMoney.4B5257}", 0, canonical: true));

        Assert.Equal(FormulaTokenKind.Reference, token.Kind);
        Assert.Equal(("B", true, "L.ConsumerSpending.PinMoney.4B5257", false), (token.Reference!.Column, token.Reference.ColumnAbsolute, token.Reference.RowKey, token.Reference.RowAbsolute));
        Assert.Null(token.Reference.RowNumber);
    }

    /// <summary>A row key in braces is not something a user can type: in A1 mode the brace is an error token.</summary>
    [Fact]
    public void ABrace_IsAnError_InA1Mode()
    {
        var tokens = FormulaLexer.Tokenize("B{F.1}", 0, canonical: false);

        Assert.Contains(tokens, t => t.Kind == FormulaTokenKind.Error);
    }

    /// <summary>Text the lexer cannot read becomes an error token with its span and a reason, and reading goes on after it.</summary>
    [Fact]
    public void UnreadableText_IsAnErrorToken_AndReadingGoesOn()
    {
        var tokens = FormulaLexer.Tokenize("1 ? 2", 0, canonical: false);

        Assert.Equal([FormulaTokenKind.Number, FormulaTokenKind.Error, FormulaTokenKind.Number], tokens.Select(t => t.Kind));
        Assert.Equal((2, 1), (tokens[1].Span.Start, tokens[1].Span.Length));
        Assert.Equal("Formula.UnexpectedCharacter", tokens[1].Problem!.Value.Code);
    }

    /// <summary>Text that is not closed runs to the end and is an error token.</summary>
    [Fact]
    public void UnclosedText_IsAnErrorToken()
    {
        FormulaToken token = Assert.Single(FormulaLexer.Tokenize("\"abc", 0, canonical: false));

        Assert.Equal(FormulaTokenKind.Error, token.Kind);
        Assert.Equal((0, 4), (token.Span.Start, token.Span.Length));
        Assert.Equal("Formula.UnclosedText", token.Problem!.Value.Code);
    }

    /// <summary>A "#" that starts no known error literal is an error token.</summary>
    [Fact]
    public void AnUnknownHash_IsAnErrorToken() =>
        Assert.Equal(FormulaTokenKind.Error, FormulaLexer.Tokenize("#OOPS!", 0, canonical: false)[0].Kind);

    /// <summary>A "$" not followed by a reference is an error token.</summary>
    [Fact]
    public void ALoneDollar_IsAnErrorToken() =>
        Assert.Equal(FormulaTokenKind.Error, FormulaLexer.Tokenize("$+1", 0, canonical: false)[0].Kind);

    /// <summary>Empty text has no tokens.</summary>
    [Fact]
    public void EmptyText_HasNoTokens() => Assert.Empty(FormulaLexer.Tokenize("   ", 0, canonical: false));

    /// <summary>Words that only look like references are names: too many column letters, a row of 0 or with a leading zero, a row too long.</summary>
    [Theory]
    [InlineData("ABCD1")]
    [InlineData("B0")]
    [InlineData("B01")]
    [InlineData("B12345678")]
    [InlineData("B2X")]
    public void ReferenceLookalikes_AreNames(string text) =>
        Assert.Equal([FormulaTokenKind.Name], Kinds(text));

    /// <summary>A reference-shaped word before "(" or "!" is a function or a sheet prefix, not a reference.</summary>
    [Fact]
    public void AReferenceShapedWord_BeforeAParenthesisOrBang_IsAFunctionOrSheet()
    {
        Assert.Equal([FormulaTokenKind.Function, FormulaTokenKind.OpenParen], Kinds("LOG10("));
        Assert.Equal([FormulaTokenKind.SheetPrefix, FormulaTokenKind.Reference], Kinds("A1!B2"));
    }

    /// <summary>A malformed canonical key is not a reference: unclosed, empty, or with characters keys never have.</summary>
    [Theory]
    [InlineData("B{F.1")]
    [InlineData("B{}")]
    [InlineData("B{F-1}")]
    public void AMalformedRowKey_IsNotAReference(string text) =>
        Assert.DoesNotContain(FormulaLexer.Tokenize(text, 0, canonical: true), t => t.Kind == FormulaTokenKind.Reference);

    /// <summary>An "E" with no digits after it is not an exponent: the number ends before it.</summary>
    [Fact]
    public void AnExponentWithoutDigits_EndsTheNumber()
    {
        var tokens = FormulaLexer.Tokenize("1E+", 0, canonical: false);

        Assert.Equal("1", tokens[0].Text);
        Assert.Equal([FormulaTokenKind.Number, FormulaTokenKind.Name, FormulaTokenKind.Operator], tokens.Select(t => t.Kind));
    }

    /// <summary>A lone "&lt;" or "&gt;" is a one-character operator.</summary>
    [Fact]
    public void ASingleComparison_IsOneCharacter() =>
        Assert.Equal(["<", "1", ">", "2"], FormulaLexer.Tokenize("<1>2", 0, canonical: false).Select(t => t.Text));

    /// <summary>An error literal keeps the text as typed (lower case included).</summary>
    [Fact]
    public void AnErrorLiteral_KeepsItsText() =>
        Assert.Equal("#ref!", FormulaLexer.Tokenize("#ref!", 0, canonical: false)[0].Text);

    /// <summary>Every error value has the text Excel shows for it, the one the lexer reads back.</summary>
    [Fact]
    public void ErrorText_IsTheLiteral()
    {
        Assert.Equal("#DIV/0!", FormulaLexer.ErrorText(FormulaErrorKind.DivideByZero));
        Assert.All(Enum.GetValues<FormulaErrorKind>(), kind =>
            Assert.Equal(kind, FormulaLexer.Tokenize(FormulaLexer.ErrorText(kind), 0, canonical: false).Single().Value));
    }
}

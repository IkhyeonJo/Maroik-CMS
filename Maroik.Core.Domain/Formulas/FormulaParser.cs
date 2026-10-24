using System.Globalization;
using Maroik.Core.Domain.Errors;

namespace Maroik.Core.Domain.Formulas;

/// <summary>
/// Parses a formula (the cell input with its leading "=") into a syntax tree, by recursive descent with Excel's precedence,
/// tightest first: range ":", prefix "-" / "+", postfix "%", "^", "*" "/", "+" "-", "&amp;", then the comparisons. Like Excel,
/// negation binds tighter than "^" (-2^2 = 4) and "^" groups from the left (2^3^2 = 64).
/// <para>
/// It never throws. A problem becomes a <see cref="FormulaDiagnostic"/> at the exact span (a zero-length span at the end for
/// "something is missing here") and an <see cref="InvalidNode"/> in the tree, and parsing goes on, so a formula that is still
/// being typed is coloured and underlined as far as it goes. The lexer's error tokens are reported first and then skipped.
/// </para>
/// </summary>
public static class FormulaParser
{
    /// <summary>Deepest nesting of parentheses, calls and operators a formula may have (the work per formula is bounded).</summary>
    public const int MaxDepth = 64;

    /// <summary>Most arguments a function call may have (Excel's limit).</summary>
    public const int MaxArguments = 255;

    /// <summary>
    /// Parses <paramref name="input"/>, which starts with "=". <paramref name="canonical"/> reads the stored form (row keys in
    /// braces) instead of the A1 form.
    /// </summary>
    public static FormulaSyntax Parse(string input, bool canonical)
    {
        List<FormulaToken> tokens = [new FormulaToken(FormulaTokenKind.Operator, new TextSpan(0, 1), "=")];
        tokens.AddRange(FormulaLexer.Tokenize(input, 1, canonical));
        var parser = new State(input.Length, [.. tokens.Skip(1).Where(t => t.Kind != FormulaTokenKind.Error)]);
        foreach (FormulaToken error in tokens.Where(t => t.Kind == FormulaTokenKind.Error))
            parser.Diagnostics.Add(new FormulaDiagnostic(error.Span, error.Problem!.Value));

        FormulaNode root;
        if (parser.AtEnd)
        {
            parser.Report(new TextSpan(input.Length, 0), "Formula.Empty", "The formula is empty.");
            root = new InvalidNode(new TextSpan(input.Length, 0));
        }
        else
        {
            root = parser.Expression();
            while (!parser.AtEnd)
            {
                FormulaToken extra = parser.Next();
                parser.Report(extra.Span, "Formula.UnexpectedToken", "'{0}' is not expected here.", extra.Text);
            }
        }

        return new FormulaSyntax(root, tokens, [.. parser.Diagnostics.OrderBy(d => d.Span.Start)]);
    }

    /// <summary>The parser's position in the token list and what it has found so far.</summary>
    private sealed class State(int inputLength, List<FormulaToken> tokens)
    {
        /// <summary>The next token's index.</summary>
        private int _position;

        /// <summary>The current nesting depth.</summary>
        private int _depth;

        /// <summary>True once the depth limit has been reported (reported once only).</summary>
        private bool _tooDeepReported;

        /// <summary>The problems found.</summary>
        public List<FormulaDiagnostic> Diagnostics { get; } = [];

        /// <summary>True when every token has been read.</summary>
        public bool AtEnd => _position >= tokens.Count;

        /// <summary>The next token, or null at the end.</summary>
        private FormulaToken? Peek => AtEnd ? null : tokens[_position];

        /// <summary>The zero-length span just after the last token read (where something missing would go).</summary>
        private TextSpan Here => AtEnd ? new TextSpan(inputLength, 0) : new TextSpan(tokens[_position].Span.Start, 0);

        /// <summary>Reads the next token.</summary>
        public FormulaToken Next() => tokens[_position++];

        /// <summary>Records a problem.</summary>
        public void Report(TextSpan span, string code, string template, params object[] args) =>
            Diagnostics.Add(new FormulaDiagnostic(span, DomainError.Validation(code, template, args)));

        /// <summary>True (and reads it) when the next token is the operator <paramref name="op"/>.</summary>
        private bool TakeOperator(string op, out FormulaToken token)
        {
            token = Peek!;
            if (Peek is { Kind: FormulaTokenKind.Operator } t && t.Text == op)
            {
                _position++;
                return true;
            }

            return false;
        }

        /// <summary>The next operator token when it is one of <paramref name="ops"/>.</summary>
        private string? PeekOperator(params string[] ops) =>
            Peek is { Kind: FormulaTokenKind.Operator } t && ops.Contains(t.Text) ? t.Text : null;

        /// <summary>A whole expression (the comparison level).</summary>
        public FormulaNode Expression()
        {
            if (++_depth > MaxDepth)
            {
                if (!_tooDeepReported)
                {
                    _tooDeepReported = true;
                    Report(Peek?.Span ?? Here, "Formula.TooDeep", "The formula is nested more than {0} levels deep.", MaxDepth);
                }

                // Skip the rest: going deeper is exactly what is refused.
                TextSpan start = Peek?.Span ?? Here;
                _position = tokens.Count;
                _depth--;
                return new InvalidNode(start);
            }

            FormulaNode node = LeftAssociative(Concatenation, "=", "<>", "<", ">", "<=", ">=");
            _depth--;
            return node;
        }

        /// <summary>The "&amp;" level.</summary>
        private FormulaNode Concatenation() => LeftAssociative(Additive, "&");

        /// <summary>The "+" "-" level.</summary>
        private FormulaNode Additive() => LeftAssociative(Multiplicative, "+", "-");

        /// <summary>The "*" "/" level.</summary>
        private FormulaNode Multiplicative() => LeftAssociative(Power, "*", "/");

        /// <summary>The "^" level (left-associative, as Excel).</summary>
        private FormulaNode Power() => LeftAssociative(Unary, "^");

        /// <summary>One left-associative binary level.</summary>
        private FormulaNode LeftAssociative(Func<FormulaNode> operand, params string[] ops)
        {
            FormulaNode left = operand();
            while (PeekOperator(ops) is { } op)
            {
                Next();
                FormulaNode right = operand();
                left = new BinaryNode(TextSpan.Covering(left.Span, right.Span), op, left, right);
            }

            return left;
        }

        /// <summary>Prefix "-" / "+", then postfix "%".</summary>
        private FormulaNode Unary()
        {
            if (PeekOperator("-", "+") is { } op)
            {
                FormulaToken sign = Next();
                FormulaNode operand = Unary();
                return new UnaryNode(TextSpan.Covering(sign.Span, operand.Span), op, operand);
            }

            FormulaNode node = Primary();
            while (TakeOperator("%", out FormulaToken percent))
                node = new PercentNode(TextSpan.Covering(node.Span, percent.Span), node);
            return node;
        }

        /// <summary>A literal, reference, range, call, name or parenthesised expression.</summary>
        private FormulaNode Primary()
        {
            if (Peek is not { } token)
            {
                Report(Here, "Formula.OperandExpected", "A value or reference is expected here.");
                return new InvalidNode(Here);
            }

            switch (token.Kind)
            {
                case FormulaTokenKind.Number:
                    Next();
                    if (decimal.TryParse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number))
                        return new NumberNode(token.Span, number);
                    Report(token.Span, "Formula.NumberTooLarge", "The number {0} is too large.", token.Text);
                    return new InvalidNode(token.Span);
                case FormulaTokenKind.String:
                    Next();
                    return new TextNode(token.Span, (string)token.Value!);
                case FormulaTokenKind.Boolean:
                    Next();
                    return new BooleanNode(token.Span, (bool)token.Value!);
                case FormulaTokenKind.ErrorLiteral:
                    Next();
                    return new ErrorLiteralNode(token.Span, (FormulaErrorKind)token.Value!);
                case FormulaTokenKind.Name:
                    Next();
                    return new NameNode(token.Span, (string)token.Value!);
                case FormulaTokenKind.Reference:
                case FormulaTokenKind.SheetPrefix:
                    return ReferenceOrRange();
                case FormulaTokenKind.Function:
                    return Call();
                case FormulaTokenKind.OpenParen:
                {
                    Next();
                    FormulaNode inner = Expression();
                    TextSpan end = ExpectCloseParen();
                    return inner with { Span = TextSpan.Covering(token.Span, end) };
                }
                default:
                    Report(token.Span, "Formula.OperandExpected", "A value or reference is expected here.");
                    if (token.Kind != FormulaTokenKind.CloseParen && token.Kind != FormulaTokenKind.Comma) Next();
                    return new InvalidNode(token.Span);
            }
        }

        /// <summary>Reads ")" or reports it missing; returns its span (zero-length when missing).</summary>
        private TextSpan ExpectCloseParen()
        {
            if (Peek is { Kind: FormulaTokenKind.CloseParen } close)
            {
                Next();
                return close.Span;
            }

            Report(Here, "Formula.CloseParenExpected", "')' is expected here.");
            return Here;
        }

        /// <summary>[sheet!]reference [":" reference].</summary>
        private FormulaNode ReferenceOrRange()
        {
            FormulaNode start = Reference();
            if (start is not ReferenceNode first || Peek is not { Kind: FormulaTokenKind.Colon })
                return start;

            Next();
            if (Peek is { Kind: FormulaTokenKind.Reference } endToken)
            {
                Next();
                var end = new ReferenceNode(endToken.Span, null, endToken.Reference!);
                return new RangeNode(TextSpan.Covering(first.Span, end.Span), first, end);
            }

            TextSpan where = Peek?.Span ?? Here;
            Report(where, "Formula.RangeEndExpected", "A cell reference is expected after ':'.");
            if (!AtEnd) Next();
            return new InvalidNode(TextSpan.Covering(first.Span, where));
        }

        /// <summary>[sheet!]reference.</summary>
        private FormulaNode Reference()
        {
            FormulaToken first = Next();
            string? sheet = null;
            if (first.Kind == FormulaTokenKind.SheetPrefix)
            {
                sheet = (string)first.Value!;
                if (Peek is not { Kind: FormulaTokenKind.Reference })
                {
                    Report(Peek?.Span ?? Here, "Formula.ReferenceExpected", "A cell reference is expected after '{0}'.", first.Text);
                    return new InvalidNode(first.Span);
                }

                FormulaToken cell = Next();
                return new ReferenceNode(TextSpan.Covering(first.Span, cell.Span), sheet, cell.Reference!);
            }

            return new ReferenceNode(first.Span, sheet, first.Reference!);
        }

        /// <summary>NAME "(" [argument ("," argument)*] ")"; an empty argument is a <see cref="MissingNode"/>.</summary>
        private FormulaNode Call()
        {
            FormulaToken name = Next();
            Next(); // "(" — the lexer makes a function token only right before one.
            List<FormulaNode> arguments = [];
            if (Peek is { Kind: FormulaTokenKind.CloseParen } emptyClose)
            {
                Next();
                return new CallNode(TextSpan.Covering(name.Span, emptyClose.Span), (string)name.Value!, name.Span, arguments);
            }

            while (true)
            {
                arguments.Add(Peek is { Kind: FormulaTokenKind.Comma or FormulaTokenKind.CloseParen } || AtEnd
                    ? new MissingNode(Here)
                    : Expression());
                if (Peek is { Kind: FormulaTokenKind.Comma })
                {
                    Next();
                    continue;
                }

                break;
            }

            if (arguments.Count > MaxArguments)
                Report(name.Span, "Formula.TooManyArguments", "A function can take at most {0} arguments.", MaxArguments);

            TextSpan end = ExpectCloseParen();
            return new CallNode(TextSpan.Covering(name.Span, end), (string)name.Value!, name.Span, arguments);
        }
    }
}

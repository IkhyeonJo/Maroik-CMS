using Maroik.Core.Domain.Errors;

namespace Maroik.Core.Domain.Formulas;

/// <summary>
/// Turns Excel formula text into tokens with their spans. It never throws: text it cannot read becomes an
/// <see cref="FormulaTokenKind.Error"/> token at the exact span with the reason, and reading goes on after it, so the editor can
/// colour and underline a formula that is still being typed.
/// <para>
/// A word is classified by what follows it, as Excel does: before "(" it is a function, before "!" a sheet prefix; otherwise a
/// cell reference when it has the shape of one (letters then digits, with optional "$"), TRUE / FALSE, or a plain name. In the
/// canonical (stored) form a reference's row is a stable key in braces — <c>B{L.ConsumerSpending.PinMoney.4B5257}</c> — which
/// a user cannot type (a brace is an error in the A1 form).
/// </para>
/// </summary>
public static class FormulaLexer
{
    /// <summary>The error literals, longest first so a prefix never wins.</summary>
    private static readonly (string Text, FormulaErrorKind Kind)[] ErrorLiterals =
    [
        ("#DIV/0!", FormulaErrorKind.DivideByZero), ("#VALUE!", FormulaErrorKind.Value), ("#CYCLE!", FormulaErrorKind.Cycle),
        ("#NAME?", FormulaErrorKind.Name), ("#REF!", FormulaErrorKind.Ref), ("#NUM!", FormulaErrorKind.Num), ("#N/A", FormulaErrorKind.NotAvailable),
    ];

    /// <summary>The text Excel shows for an error value (#REF!, #DIV/0!, ...).</summary>
    public static string ErrorText(FormulaErrorKind kind) => ErrorLiterals.First(e => e.Kind == kind).Text;

    /// <summary>
    /// The tokens of <paramref name="input"/> from index <paramref name="start"/> (1 for a formula after its "="); spans are absolute
    /// in <paramref name="input"/>. <paramref name="canonical"/> reads row keys in braces instead of row numbers.
    /// </summary>
    public static IReadOnlyList<FormulaToken> Tokenize(string input, int start, bool canonical)
    {
        List<FormulaToken> tokens = [];
        int i = start;
        while (i < input.Length)
        {
            char c = input[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            int begin = i;
            FormulaToken token = c switch
            {
                '"' => ReadString(input, ref i),
                '#' => ReadErrorLiteral(input, ref i),
                '(' => Single(FormulaTokenKind.OpenParen, input, ref i),
                ')' => Single(FormulaTokenKind.CloseParen, input, ref i),
                ',' => Single(FormulaTokenKind.Comma, input, ref i),
                ':' => Single(FormulaTokenKind.Colon, input, ref i),
                '+' or '-' or '*' or '/' or '^' or '&' or '=' or '%' => Single(FormulaTokenKind.Operator, input, ref i),
                '<' or '>' => ReadComparison(input, ref i),
                _ when char.IsAsciiDigit(c) || (c == '.' && i + 1 < input.Length && char.IsAsciiDigit(input[i + 1])) => ReadNumber(input, ref i),
                _ when IsWordStart(c) || c == '$' => ReadWord(input, ref i, canonical),
                _ => Problem(input, ref i, i + 1, "Formula.UnexpectedCharacter", "'{0}' cannot be used in a formula.", c.ToString()),
            };
            tokens.Add(token with { Span = new TextSpan(begin, i - begin) });
        }

        return tokens;
    }

    /// <summary>A one-character token.</summary>
    private static FormulaToken Single(FormulaTokenKind kind, string input, ref int i)
    {
        string text = input[i].ToString();
        i++;
        return new FormulaToken(kind, default, text);
    }

    /// <summary>"&lt;", "&gt;", "&lt;=", "&gt;=" or "&lt;&gt;".</summary>
    private static FormulaToken ReadComparison(string input, ref int i)
    {
        int length = i + 1 < input.Length && (input[i + 1] == '=' || (input[i] == '<' && input[i + 1] == '>')) ? 2 : 1;
        string text = input.Substring(i, length);
        i += length;
        return new FormulaToken(FormulaTokenKind.Operator, default, text);
    }

    /// <summary>A text literal; a doubled quote is one quote. Unclosed text runs to the end and is an error.</summary>
    private static FormulaToken ReadString(string input, ref int i)
    {
        int begin = i;
        var value = new System.Text.StringBuilder();
        i++;
        while (i < input.Length)
        {
            if (input[i] == '"')
            {
                if (i + 1 < input.Length && input[i + 1] == '"')
                {
                    value.Append('"');
                    i += 2;
                    continue;
                }

                i++;
                return new FormulaToken(FormulaTokenKind.String, default, input[begin..i], value.ToString());
            }

            value.Append(input[i]);
            i++;
        }

        i = begin;
        return Problem(input, ref i, input.Length, "Formula.UnclosedText", "The text is not closed with a double quote.");
    }

    /// <summary>An error literal such as #REF!; an unknown "#" word is an error token.</summary>
    private static FormulaToken ReadErrorLiteral(string input, ref int i)
    {
        foreach (var (text, kind) in ErrorLiterals)
        {
            if (string.Compare(input, i, text, 0, text.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                string typed = input.Substring(i, text.Length);
                i += text.Length;
                return new FormulaToken(FormulaTokenKind.ErrorLiteral, default, typed, kind);
            }
        }

        int end = i + 1;
        while (end < input.Length && (IsWordPart(input[end]) || input[end] is '!' or '?' or '/'))
            end++;
        return Problem(input, ref i, end, "Formula.UnknownError", "'{0}' is not an error value.", input[i..end]);
    }

    /// <summary>A number: digits, an optional decimal part and an optional exponent.</summary>
    private static FormulaToken ReadNumber(string input, ref int i)
    {
        int begin = i;
        while (i < input.Length && char.IsAsciiDigit(input[i])) i++;
        if (i < input.Length && input[i] == '.')
        {
            i++;
            while (i < input.Length && char.IsAsciiDigit(input[i])) i++;
        }

        if (i < input.Length && input[i] is 'e' or 'E')
        {
            int exponent = i + 1;
            if (exponent < input.Length && input[exponent] is '+' or '-') exponent++;
            if (exponent < input.Length && char.IsAsciiDigit(input[exponent]))
            {
                i = exponent;
                while (i < input.Length && char.IsAsciiDigit(input[i])) i++;
            }
        }

        return new FormulaToken(FormulaTokenKind.Number, default, input[begin..i]);
    }

    /// <summary>A word: a function, sheet prefix, reference, boolean or name, decided by its shape and what follows it.</summary>
    private static FormulaToken ReadWord(string input, ref int i, bool canonical)
    {
        int begin = i;
        if (TryReadReference(input, ref i, canonical, out CellReference? reference))
        {
            // A reference-shaped word followed by more word characters ("B2X") or by "(" / "!" is not a reference.
            if (i >= input.Length || !(IsWordPart(input[i]) || input[i] is '(' or '!'))
                return new FormulaToken(FormulaTokenKind.Reference, default, input[begin..i], Reference: reference);
            i = begin;
        }

        if (input[i] == '$')
            return Problem(input, ref i, i + 1, "Formula.MisplacedDollar", "'$' must be followed by a cell reference.");

        while (i < input.Length && IsWordPart(input[i])) i++;
        string word = input[begin..i];

        if (i < input.Length && input[i] == '!')
        {
            i++;
            return new FormulaToken(FormulaTokenKind.SheetPrefix, default, input[begin..i], word);
        }

        if (i < input.Length && input[i] == '(')
            return new FormulaToken(FormulaTokenKind.Function, default, word, word.ToUpperInvariant());

        if (word.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || word.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
            return new FormulaToken(FormulaTokenKind.Boolean, default, word, word.Equals("TRUE", StringComparison.OrdinalIgnoreCase));

        return new FormulaToken(FormulaTokenKind.Name, default, word, word.ToUpperInvariant());
    }

    /// <summary>Reads [$]letters[$](digits | {key}) at <paramref name="i"/>; leaves <paramref name="i"/> past it when it matches.</summary>
    private static bool TryReadReference(string input, ref int i, bool canonical, out CellReference? reference)
    {
        reference = null;
        int p = i;
        bool columnAbsolute = p < input.Length && input[p] == '$';
        if (columnAbsolute) p++;
        int letters = p;
        while (p < input.Length && char.IsAsciiLetter(input[p])) p++;
        if (p == letters || p - letters > 3) return false;
        string column = input[letters..p].ToUpperInvariant();

        bool rowAbsolute = p < input.Length && input[p] == '$';
        if (rowAbsolute) p++;

        if (canonical && p < input.Length && input[p] == '{')
        {
            int close = input.IndexOf('}', p);
            if (close < 0) return false;
            string key = input[(p + 1)..close];
            if (key.Length == 0 || !key.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '.')) return false;
            reference = new CellReference(column, columnAbsolute, null, key, rowAbsolute);
            i = close + 1;
            return true;
        }

        int digits = p;
        while (p < input.Length && char.IsAsciiDigit(input[p])) p++;
        if (p == digits || p - digits > 7 || input[digits] == '0') return false;
        reference = new CellReference(column, columnAbsolute, int.Parse(input[digits..p], System.Globalization.CultureInfo.InvariantCulture), null, rowAbsolute);
        i = p;
        return true;
    }

    /// <summary>An error token covering [<paramref name="i"/>, <paramref name="end"/>); moves <paramref name="i"/> to <paramref name="end"/>.</summary>
    private static FormulaToken Problem(string input, ref int i, int end, string code, string template, params object[] args)
    {
        string text = input[i..end];
        i = end;
        return new FormulaToken(FormulaTokenKind.Error, default, text, Problem: DomainError.Validation(code, template, args));
    }

    /// <summary>A character that can start a word.</summary>
    private static bool IsWordStart(char c) => char.IsAsciiLetter(c) || c == '_';

    /// <summary>A character that can continue a word.</summary>
    private static bool IsWordPart(char c) => char.IsAsciiLetterOrDigit(c) || c is '_' or '.';
}

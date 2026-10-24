namespace Maroik.Core.Domain.Formulas;

/// <summary>What a token of a formula is; the editor colours tokens by kind.</summary>
public enum FormulaTokenKind
{
    /// <summary>A number literal (12, 1.5, 1E3).</summary>
    Number,

    /// <summary>A text literal in double quotes.</summary>
    String,

    /// <summary>TRUE or FALSE.</summary>
    Boolean,

    /// <summary>An error literal (#REF!, #DIV/0!, ...).</summary>
    ErrorLiteral,

    /// <summary>A cell reference (B2, $B$2; or B{key} in the stored form).</summary>
    Reference,

    /// <summary>A sheet name with its "!" (Income2025!).</summary>
    SheetPrefix,

    /// <summary>A function name (the name just before "(").</summary>
    Function,

    /// <summary>Any other word; the binder reports it as #NAME?.</summary>
    Name,

    /// <summary>An operator (+ - * / ^ &amp; = &lt;&gt; &lt; &gt; &lt;= &gt;= %).</summary>
    Operator,

    /// <summary>"(".</summary>
    OpenParen,

    /// <summary>")".</summary>
    CloseParen,

    /// <summary>",", between function arguments.</summary>
    Comma,

    /// <summary>":", the range operator.</summary>
    Colon,

    /// <summary>Text the lexer could not read (see <see cref="FormulaToken.Problem"/>).</summary>
    Error,
}

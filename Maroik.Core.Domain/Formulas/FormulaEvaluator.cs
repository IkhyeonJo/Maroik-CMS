using System.Globalization;

namespace Maroik.Core.Domain.Formulas;

/// <summary>
/// Evaluates a bound formula with Excel's value rules, in decimals: operands are coerced as Excel coerces them (numeric text and
/// booleans to numbers, an empty cell to 0 / "" / FALSE), comparisons order numbers below text below booleans (text ignoring case),
/// and the first error met — the left operand first — is the result. An overflow is #NUM!, a division by zero #DIV/0!.
/// A tree walk over a closed set of node types and the <see cref="FormulaFunctions"/> catalog: user text never becomes code.
/// </summary>
public static class FormulaEvaluator
{
    /// <summary>The value of <paramref name="formula"/>; a formula that cannot be saved (syntax, argument counts) is #VALUE!.</summary>
    public static FormulaValue Evaluate(BoundFormula formula, FormulaContext context) =>
        formula.CanSave ? new Run(formula, context).Scalar(formula.Syntax.Root) : new ErrorValue(FormulaErrorKind.Value);

    /// <summary>#VALUE!.</summary>
    private static readonly ErrorValue ValueError = new(FormulaErrorKind.Value);

    /// <summary>#NUM!.</summary>
    private static readonly ErrorValue NumError = new(FormulaErrorKind.Num);

    /// <summary>#DIV/0!.</summary>
    private static readonly ErrorValue DivideError = new(FormulaErrorKind.DivideByZero);

    /// <summary>A value as a number, or the error it becomes (Excel's coercion).</summary>
    private static FormulaValue ToNumber(FormulaValue value) => value switch
    {
        NumberValue => value,
        BooleanValue b => new NumberValue(b.Value ? 1 : 0),
        EmptyValue => new NumberValue(0),
        TextValue t => decimal.TryParse(t.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal n) ? new NumberValue(n) : ValueError,
        _ => value,
    };

    /// <summary>A value as a boolean, or the error it becomes.</summary>
    private static FormulaValue ToBoolean(FormulaValue value) => value switch
    {
        BooleanValue => value,
        NumberValue n => new BooleanValue(n.Value != 0),
        EmptyValue => new BooleanValue(false),
        TextValue t when t.Value.Equals("TRUE", StringComparison.OrdinalIgnoreCase) => new BooleanValue(true),
        TextValue t when t.Value.Equals("FALSE", StringComparison.OrdinalIgnoreCase) => new BooleanValue(false),
        TextValue => ValueError,
        _ => value,
    };

    /// <summary>A value as text (for "&amp;"), or the error it is.</summary>
    private static FormulaValue ToText(FormulaValue value) => value switch
    {
        TextValue => value,
        NumberValue n => new TextValue(FormatNumber(n.Value)),
        BooleanValue b => new TextValue(b.Value ? "TRUE" : "FALSE"),
        EmptyValue => new TextValue(""),
        _ => value,
    };

    /// <summary>A number as Excel's general format writes it: no trailing zeros, invariant digits.</summary>
    public static string FormatNumber(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);

    /// <summary>Runs an arithmetic step, turning a decimal overflow into #NUM!.</summary>
    private static FormulaValue Checked(Func<decimal> step)
    {
        try
        {
            return new NumberValue(step());
        }
        catch (OverflowException)
        {
            // A result too large for a decimal is Excel's #NUM!: a value of the cell, not a failure.
            return NumError;
        }
    }

    /// <summary>One evaluation: the formula, what it reads, and the walk.</summary>
    private sealed class Run(BoundFormula formula, FormulaContext context)
    {
        /// <summary>The value of a node where one value is needed (a range there is #VALUE!).</summary>
        public FormulaValue Scalar(FormulaNode node) => node switch
        {
            NumberNode n => new NumberValue(n.Value),
            TextNode t => new TextValue(t.Value),
            BooleanNode b => new BooleanValue(b.Value),
            ErrorLiteralNode e => new ErrorValue(e.Error),
            MissingNode => EmptyValue.Instance,
            NameNode => new ErrorValue(FormulaErrorKind.Name),
            ReferenceNode or RangeNode => Single(node),
            UnaryNode u => Unary(u),
            PercentNode p => ToNumber(Scalar(p.Operand)) is NumberValue v ? new NumberValue(v.Value / 100) : ToNumber(Scalar(p.Operand)),
            BinaryNode b => Binary(b),
            CallNode c => Call(c),
            _ => ValueError,
        };

        /// <summary>A reference's one value (#REF! when it points outside, #VALUE! for a range).</summary>
        private FormulaValue Single(FormulaNode node)
        {
            BoundTarget target = formula.Targets[node];
            if (target.Error is { } error) return new ErrorValue(error);
            return target.IsRange ? ValueError : context.ValueOf(target.Cells[0]);
        }

        /// <summary>Prefix "-" negates a number; prefix "+" leaves the value as it is (as Excel).</summary>
        private FormulaValue Unary(UnaryNode node)
        {
            FormulaValue operand = Scalar(node.Operand);
            if (node.Operator == "+") return operand;
            return ToNumber(operand) is NumberValue n ? new NumberValue(-n.Value) : ToNumber(operand);
        }

        /// <summary>A binary operation.</summary>
        private FormulaValue Binary(BinaryNode node)
        {
            FormulaValue left = Scalar(node.Left), right = Scalar(node.Right);
            switch (node.Operator)
            {
                case "&":
                {
                    FormulaValue l = ToText(left), r = ToText(right);
                    if (l is ErrorValue) return l;
                    if (r is ErrorValue) return r;
                    return new TextValue(((TextValue)l).Value + ((TextValue)r).Value);
                }
                case "=" or "<>" or "<" or ">" or "<=" or ">=":
                    return Compare(node.Operator, left, right);
                default:
                {
                    FormulaValue l = ToNumber(left), r = ToNumber(right);
                    if (l is not NumberValue a) return l;
                    if (r is not NumberValue b) return r;
                    return node.Operator switch
                    {
                        "+" => Checked(() => a.Value + b.Value),
                        "-" => Checked(() => a.Value - b.Value),
                        "*" => Checked(() => a.Value * b.Value),
                        "/" => b.Value == 0 ? DivideError : Checked(() => a.Value / b.Value),
                        _ => Power(a.Value, b.Value),
                    };
                }
            }
        }

        /// <summary>"^": exact for whole exponents, double precision otherwise; 0^0 and a negative base with a fraction are #NUM!.</summary>
        private static FormulaValue Power(decimal number, decimal exponent)
        {
            if (number == 0 && exponent == 0) return NumError;
            if (number == 0 && exponent < 0) return DivideError;
            if (exponent == decimal.Truncate(exponent) && Math.Abs(exponent) <= 1000)
            {
                FormulaValue power = Checked(() =>
                {
                    decimal result = 1, factor = number;
                    for (long e = (long)Math.Abs(exponent); e > 0; e >>= 1)
                    {
                        if ((e & 1) == 1) result *= factor;
                        if (e > 1) factor *= factor;
                    }

                    return result;
                });
                return exponent >= 0 || power is not NumberValue p ? power : Checked(() => 1 / p.Value);
            }

            if (number < 0) return NumError;
            double value = Math.Pow((double)number, (double)exponent);
            return double.IsFinite(value) && Math.Abs(value) < (double)decimal.MaxValue ? new NumberValue((decimal)value) : NumError;
        }

        /// <summary>A comparison with Excel's ordering: number &lt; text &lt; boolean; an empty side takes the other side's type.</summary>
        private static FormulaValue Compare(string op, FormulaValue left, FormulaValue right)
        {
            if (left is ErrorValue) return left;
            if (right is ErrorValue) return right;
            left = left is EmptyValue ? EmptyLike(right) : left;
            right = right is EmptyValue ? EmptyLike(left) : right;

            int order = (left, right) switch
            {
                (NumberValue a, NumberValue b) => a.Value.CompareTo(b.Value),
                (TextValue a, TextValue b) => Math.Sign(string.Compare(a.Value, b.Value, StringComparison.OrdinalIgnoreCase)),
                (BooleanValue a, BooleanValue b) => a.Value.CompareTo(b.Value),
                _ => Rank(left).CompareTo(Rank(right)),
            };

            return new BooleanValue(op switch
            {
                "=" => order == 0,
                "<>" => order != 0,
                "<" => order < 0,
                ">" => order > 0,
                "<=" => order <= 0,
                _ => order >= 0,
            });
        }

        /// <summary>What an empty cell compares as next to <paramref name="other"/>: 0, "" or FALSE.</summary>
        private static FormulaValue EmptyLike(FormulaValue other) => other switch
        {
            TextValue => new TextValue(""),
            BooleanValue => new BooleanValue(false),
            _ => new NumberValue(0),
        };

        /// <summary>The type order of mixed comparisons.</summary>
        private static int Rank(FormulaValue value) => value switch
        {
            NumberValue => 0,
            TextValue => 1,
            _ => 2,
        };

        /// <summary>A function call.</summary>
        private FormulaValue Call(CallNode call)
        {
            IReadOnlyList<FormulaNode> args = call.Arguments;
            switch (call.Name)
            {
                case "SUM":
                {
                    var (error, numbers) = Numbers(args);
                    return (FormulaValue?)error ?? Checked(() => numbers.Aggregate(0m, (a, b) => a + b));
                }
                case "AVERAGE":
                {
                    var (error, numbers) = Numbers(args);
                    if (error is not null) return error;
                    return numbers.Count == 0 ? DivideError : Checked(() => numbers.Aggregate(0m, (a, b) => a + b) / numbers.Count);
                }
                case "MIN":
                {
                    var (error, numbers) = Numbers(args);
                    return (FormulaValue?)error ?? new NumberValue(numbers.Count == 0 ? 0 : numbers.Min());
                }
                case "MAX":
                {
                    var (error, numbers) = Numbers(args);
                    return (FormulaValue?)error ?? new NumberValue(numbers.Count == 0 ? 0 : numbers.Max());
                }
                case "COUNT": return new NumberValue(Count(args));
                case "ROUND": return Round(args, (v, d) => Math.Round(v, d, MidpointRounding.AwayFromZero));
                case "ROUNDUP": return Round(args, (v, d) => Math.Round(v, d, v >= 0 ? MidpointRounding.ToPositiveInfinity : MidpointRounding.ToNegativeInfinity));
                case "ROUNDDOWN": return Round(args, (v, d) => Math.Round(v, d, MidpointRounding.ToZero));
                case "ABS": return ToNumber(Scalar(args[0])) is NumberValue n ? new NumberValue(Math.Abs(n.Value)) : ToNumber(Scalar(args[0]));
                case "IF":
                {
                    FormulaValue condition = ToBoolean(Scalar(args[0]));
                    if (condition is not BooleanValue test) return condition;
                    if (test.Value) return Scalar(args[1]);
                    return args.Count > 2 ? Scalar(args[2]) : new BooleanValue(false);
                }
                case "IFERROR": return Scalar(args[0]) is ErrorValue ? Scalar(args[1]) : Scalar(args[0]);
                case "AND": return Logical(args, all: true);
                case "OR": return Logical(args, all: false);
                case "NOT": return ToBoolean(Scalar(args[0])) is BooleanValue b ? new BooleanValue(!b.Value) : ToBoolean(Scalar(args[0]));
                case "ACTUAL": return Actual(args[0]);
                default: return new ErrorValue(FormulaErrorKind.Name);
            }
        }

        /// <summary>The cells of an argument written as a reference or range, or null for any other expression.</summary>
        private BoundTarget? TargetOf(FormulaNode node) => node is ReferenceNode or RangeNode ? formula.Targets[node] : null;

        /// <summary>
        /// The numbers of aggregate arguments (SUM, AVERAGE, MIN, MAX): in references only number cells count (text, booleans and
        /// empty cells are skipped); a direct argument is coerced (numeric text, booleans). The first error met is returned.
        /// </summary>
        private (ErrorValue? Error, List<decimal> Numbers) Numbers(IReadOnlyList<FormulaNode> args)
        {
            List<decimal> numbers = [];
            foreach (FormulaNode arg in args)
            {
                if (TargetOf(arg) is { } target)
                {
                    if (target.Error is { } refError) return (new ErrorValue(refError), numbers);
                    foreach (FormulaValue value in target.Cells.Select(context.ValueOf))
                    {
                        if (value is ErrorValue error) return (error, numbers);
                        if (value is NumberValue n) numbers.Add(n.Value);
                    }

                    continue;
                }

                FormulaValue coerced = ToNumber(Scalar(arg));
                if (coerced is ErrorValue e) return (e, numbers);
                numbers.Add(((NumberValue)coerced).Value);
            }

            return (null, numbers);
        }

        /// <summary>COUNT: number cells in references; numbers, numeric text and booleans among direct arguments; errors never.</summary>
        private int Count(IReadOnlyList<FormulaNode> args)
        {
            int count = 0;
            foreach (FormulaNode arg in args)
            {
                if (TargetOf(arg) is { } target)
                    count += target.Cells.Select(context.ValueOf).Count(v => v is NumberValue);
                else if (Scalar(arg) is NumberValue or BooleanValue || Scalar(arg) is TextValue && ToNumber(Scalar(arg)) is NumberValue)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// ROUND / ROUNDUP / ROUNDDOWN: <paramref name="round"/> at 0..28 decimal places; negative digits round to tens, hundreds, ...;
        /// the digits are truncated to a whole number, more than 28 leave the number as it is, fewer than -28 give 0.
        /// </summary>
        private FormulaValue Round(IReadOnlyList<FormulaNode> args, Func<decimal, int, decimal> round)
        {
            FormulaValue number = ToNumber(Scalar(args[0])), digits = ToNumber(Scalar(args[1]));
            if (number is not NumberValue n) return number;
            if (digits is not NumberValue d) return digits;
            decimal places = decimal.Truncate(d.Value);
            if (places > 28) return n;
            if (places < -28) return new NumberValue(0);
            if (places >= 0) return new NumberValue(round(n.Value, (int)places));

            decimal factor = 1;
            for (int i = 0; i < -places; i++) factor *= 10;
            return Checked(() => round(n.Value / factor, 0) * factor);
        }

        /// <summary>AND / OR: logical and number cells in references (text and empty skipped), coerced direct arguments; none is #VALUE!.</summary>
        private FormulaValue Logical(IReadOnlyList<FormulaNode> args, bool all)
        {
            List<bool> values = [];
            foreach (FormulaNode arg in args)
            {
                if (TargetOf(arg) is { } target)
                {
                    if (target.Error is { } refError) return new ErrorValue(refError);
                    foreach (FormulaValue value in target.Cells.Select(context.ValueOf))
                    {
                        if (value is ErrorValue) return value;
                        if (value is BooleanValue b) values.Add(b.Value);
                        if (value is NumberValue n) values.Add(n.Value != 0);
                    }

                    continue;
                }

                FormulaValue coerced = ToBoolean(Scalar(arg));
                if (coerced is not BooleanValue direct) return coerced;
                values.Add(direct.Value);
            }

            if (values.Count == 0) return ValueError;
            return new BooleanValue(all ? values.All(v => v) : values.Any(v => v));
        }

        /// <summary>ACTUAL: the sum of the actual totals behind the budget cells the argument names.</summary>
        private FormulaValue Actual(FormulaNode arg)
        {
            if (TargetOf(arg) is not { } target) return ValueError;
            if (target.Error is { } refError) return new ErrorValue(refError);
            decimal total = 0;
            foreach (CellAddress cell in target.Cells)
            {
                if (context.ActualOf(cell) is not { } actual) return ValueError;
                total += actual;
            }

            return new NumberValue(total);
        }
    }
}

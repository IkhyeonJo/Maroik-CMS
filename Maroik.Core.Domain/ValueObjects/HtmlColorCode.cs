using System.Text.RegularExpressions;
using ErrorOr;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.ValueObjects;

/// <summary>
/// Value object that represents a validated HTML hex color code (e.g. "#FF5733").
/// The stored value is always upper-cased. Only the 6-digit form is accepted — the 3-digit
/// shorthand (e.g. "#F53") would pass CSS but not <c>Calendar_HtmlColorCode_check</c>, the DB CHECK
/// constraint this value is ultimately persisted against, which only allows
/// <c>^#[0-9A-Fa-f]{6}$</c>.
/// </summary>
public sealed partial class HtmlColorCode : ValueObject
{
    /// <summary>Cached instance of the source-generated 6-digit hex pattern (<see cref="MyRegex"/>).</summary>
    private static readonly Regex _colorRegex = MyRegex();

    /// <summary>The validated HTML hex color string (upper-case).</summary>
    public string Value { get; }

    /// <summary>Wraps an already-validated, upper-cased color value; reached only through the factories.</summary>
    private HtmlColorCode(string value) => Value = value;

    /// <summary>
    /// Re-creates an <see cref="HtmlColorCode"/> from a value already stored in the database.
    /// Skips validation — only call this when the source is trusted (repository layer).
    /// </summary>
    internal static HtmlColorCode FromTrustedSource(string value) => new(value.ToUpperInvariant());

    /// <summary>
    /// Creates an <see cref="HtmlColorCode"/>, validating the hex format.
    /// Returns a validation error when the value is null, empty, or not a valid hex color.
    /// </summary>
    public static ErrorOr<HtmlColorCode> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return LocalizableError.Validation("HtmlColorCode.Empty", "HTML color code cannot be empty.");

        if (!_colorRegex.IsMatch(value))
            return LocalizableError.Validation("HtmlColorCode.Invalid", "'{0}' is not a valid HTML hex color code (e.g. #FF5733).", value);

        return new HtmlColorCode(value.ToUpperInvariant());
    }

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetAtomicValues() { yield return Value; }

    /// <inheritdoc/>
    public override string ToString() => Value;
    // \z, not $: in .NET "$" also matches before a trailing "\n", so "#FF5733\n" would pass here and then
    // be rejected by Calendar_HtmlColorCode_check (Postgres "$" only matches the true end of the
    // string) as a raw DB error instead of this clean validation error.
    [GeneratedRegex(@"^#[A-Fa-f0-9]{6}\z", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}

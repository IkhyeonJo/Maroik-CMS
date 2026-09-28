using ErrorOr;
using Maroik.Core.Domain.Localization;
namespace Maroik.Core.Domain.Tests.Boundaries;

/// <summary>
/// Asserts a domain error's full contract: its code, its type, and — because the Website resolves the
/// translation from the message TEMPLATE (see <see cref="LocalizableError"/>) — the exact template and the
/// arguments that fill it. A changed template silently loses its ko-KR translation, so the text is part of
/// the behavior, not decoration.
/// </summary>
internal static class ErrorAssert
{
    /// <summary>Asserts that <paramref name="result"/> failed with the given code, type, message template and template arguments, and that the description is the formatted template.</summary>
    public static void Is<T>(ErrorOr<T> result, string code, ErrorType type, string template, params object[] args)
    {
        Assert.True(result.IsError, $"expected error '{code}' but the call succeeded");
        Error error = result.FirstError;
        Assert.Equal(code, error.Code);
        Assert.Equal(type, error.Type);
        Assert.Equal(template, error.Metadata![LocalizableError.ResourceKeyMetadataKey]);
        Assert.Equal(args, (object[])error.Metadata![LocalizableError.ResourceArgsMetadataKey]);
        Assert.Equal(string.Format(template, args), error.Description);
    }

    /// <summary>Shorthand for <see cref="Is{T}"/> with <see cref="ErrorType.Validation"/>.</summary>
    public static void Validation<T>(ErrorOr<T> result, string code, string template, params object[] args) =>
        Is(result, code, ErrorType.Validation, template, args);
}

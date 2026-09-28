using ErrorOr;
using Maroik.Core.Domain.Localization;

namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Shared helper (internal to this assembly) that turns a domain <see cref="Error"/> into a
/// (localization key, format args) pair for the result DTOs below. When the error carries a
/// composite-format template in its <see cref="Error.Metadata"/> (see <see cref="LocalizableError"/>),
/// returns that template and its args so a caller can pass them straight to an
/// <c>IHtmlLocalizer</c> indexer; otherwise falls back to the error's already-formatted
/// <see cref="Error.Description"/> with no args.
/// </summary>
internal static class ErrorMetadataExtensions
{
    /// <summary>Splits <paramref name="error"/> into a localizable (key, args) pair.</summary>
    public static (string Key, object[] Args) ToLocalizationKeyAndArgs(this Error error)
    {
        if (error.Metadata is { } metadata
            && metadata.TryGetValue(LocalizableError.ResourceKeyMetadataKey, out object? keyObj) && keyObj is string key
            && metadata.TryGetValue(LocalizableError.ResourceArgsMetadataKey, out object? argsObj) && argsObj is object[] args)
        {
            return (key, args);
        }

        return (error.Description, []);
    }
}

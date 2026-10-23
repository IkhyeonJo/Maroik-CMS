using System.Text;
using ErrorOr;
using Maroik.Core.Domain.Errors;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.FileManagers;

/// <summary>
/// The name rule for a File Manager file or folder (S4). A name is judged — and stored — in its NFKC form, so a full-width "／" or
/// "Ａ" cannot slip past the rule or sit beside its plain twin. Refused: an empty name, more than
/// <see cref="ShortTextPolicy.MaxLength"/> characters, a control character, any of <c>/ \ : * ? " &lt; &gt; |</c>, the names
/// <c>.</c> and <c>..</c>, and a trailing dot or space (the Windows rules, so a downloaded file keeps its name on every system).
/// A name already used in the same folder is refused by the service and the database's unique index, not here.
/// </summary>
public static class FileManagerNamePolicy
{
    /// <summary>The characters Windows refuses in a file name.</summary>
    private const string ForbiddenCharacters = "/\\:*?\"<>|";

    /// <summary>Returns the NFKC form of <paramref name="name"/> when it is a valid name, or the reason it is not.</summary>
    public static ErrorOr<string> Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return DomainError.Validation("FileManager.NameEmpty", "Please enter a name.");

        string normalized = name.Normalize(NormalizationForm.FormKC);

        if (normalized.Length > ShortTextPolicy.MaxLength)
            return DomainError.Validation("FileManager.NameTooLong", "A name must be {0} characters or fewer.", ShortTextPolicy.MaxLength);

        if (normalized.Any(c => char.IsControl(c) || ForbiddenCharacters.Contains(c, StringComparison.Ordinal)))
            return DomainError.Validation("FileManager.NameInvalidCharacter", "A name cannot contain control characters, a backslash or any of / : * ? \" < > |.");

        if (normalized is "." or "..")
            return DomainError.Validation("FileManager.NameReserved", "\".\" and \"..\" cannot be used as a name.");

        if (normalized.EndsWith('.') || normalized.EndsWith(' '))
            return DomainError.Validation("FileManager.NameTrailingDotOrSpace", "A name cannot end with a dot or a space.");

        return normalized;
    }

    /// <summary>
    /// The name a "Keep both" upload gets when <paramref name="name"/> is taken (F2-C): the first of "name (1).ext", "name (2).ext", …
    /// that is not in <paramref name="taken"/>, the number placed before the last extension as Windows Explorer does. A name with no
    /// stem (".bashrc") or no extension gets the number at its end. The stem is shortened when needed so the result stays within
    /// <see cref="ShortTextPolicy.MaxLength"/>.
    /// </summary>
    public static string KeepBothName(string name, IReadOnlyCollection<string> taken)
    {
        int dot = name.LastIndexOf('.');
        (string stem, string extension) = dot > 0 ? (name[..dot], name[dot..]) : (name, "");

        for (int n = 1; ; n++)
        {
            string suffix = $" ({n})";
            int room = ShortTextPolicy.MaxLength - suffix.Length - extension.Length;
            string candidate = $"{(stem.Length > room ? stem[..room] : stem)}{suffix}{extension}";
            if (!taken.Contains(candidate))
                return candidate;
        }
    }
}

using System.Globalization;
using System.Text;
using ErrorOr;
using Maroik.Core.Domain.Errors;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.Account;

/// <summary>
/// Domain rules for an account nickname: how it is normalized, which characters it may contain, and
/// which names are reserved. A post or comment is attributed to (and owned by) its author's nickname
/// as a plain string, so a nickname that merely <em>looks</em> like someone else's — different case,
/// stray or invisible characters, full-width letters, a Cyrillic "а" for a Latin "a" — would let a
/// user pass as that person (or as the site's staff). The rules below close those gaps at the two
/// places a nickname is set (<see cref="Account.Create"/> and, before confirmation,
/// <see cref="Account.ReplaceUnconfirmedRegistration"/>).
/// <para>
/// There is no client-side mirror of these rules; the server is the only place they are enforced.
/// Accounts that already exist are not re-validated: this policy applies to newly created ones.
/// </para>
/// </summary>
public static class NicknamePolicy
{
    /// <summary>
    /// Names an ordinary user may not register: staff / system roles, the placeholder shown to
    /// signed-out visitors (<c>Login</c>), and the site name. Matched on the <em>canonical</em> form
    /// (see <see cref="CanonicalKey"/>), so case, spacing, punctuation and full-width letters cannot
    /// be used to slip past — <c>"A d m i n"</c>, <c>"ADMIN"</c> and <c>"Ａｄｍｉｎ"</c> are all reserved.
    /// </summary>
    public static readonly IReadOnlyList<string> ReservedNicknames =
    [
        "admin", "administrator", "sysadmin", "root", "system", "moderator", "operator", "staff",
        "support", "official", "anonymous", "guest", "login", "null", "undefined", "maroik",
        "관리자", "운영자", "운영진", "어드민", "시스템", "익명", "게스트"
    ];

    /// <summary><see cref="ReservedNicknames"/> reduced to their <see cref="CanonicalKey"/> forms, for O(1) lookup.</summary>
    private static readonly HashSet<string> _reservedCanonical =
        [.. ReservedNicknames.Select(CanonicalKey)];

    /// <summary>
    /// Returns <paramref name="nickname"/> in its stored form: Unicode NFKC (full-width → ASCII,
    /// compatibility forms folded), every run of whitespace collapsed to a single space, and no
    /// leading or trailing space. Case is preserved (a nickname keeps its display casing).
    /// Throws <see cref="ArgumentException"/> for text that is not valid Unicode (a lone surrogate).
    /// </summary>
    public static string Normalize(string? nickname)
    {
        if (string.IsNullOrEmpty(nickname))
            return "";

        string composed = nickname.Normalize(NormalizationForm.FormKC);

        var builder = new StringBuilder(composed.Length);
        bool pendingSpace = false;
        foreach (char c in composed)
        {
            if (char.IsWhiteSpace(c))
            {
                // Defer the space: leading whitespace is dropped, and a run collapses to one.
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }
            builder.Append(c);
        }

        // A trailing run of whitespace never flushed its pending space, so no trim is needed.
        return builder.ToString();
    }

    /// <summary>
    /// Reduces a nickname to the form used only for comparison: NFKC, lower-cased, and stripped of
    /// everything that is not a letter or digit (spaces, punctuation, symbols, invisible characters).
    /// Never stored or shown.
    /// </summary>
    public static string CanonicalKey(string? nickname)
    {
        if (string.IsNullOrEmpty(nickname))
            return "";

        string composed;
        try { composed = nickname.Normalize(NormalizationForm.FormKC); }
        // Not valid Unicode (a lone surrogate): no canonical form, so it can collide with nothing.
        catch (ArgumentException) { return ""; }

        return string.Concat(composed.ToLowerInvariant().Where(char.IsLetterOrDigit));
    }

    /// <summary>Whether <paramref name="nickname"/> collides with a <see cref="ReservedNicknames"/> entry.</summary>
    public static bool IsReserved(string? nickname)
    {
        string key = CanonicalKey(nickname);
        return key.Length > 0 && _reservedCanonical.Contains(key);
    }

    /// <summary>
    /// Validates <paramref name="nickname"/> and returns its normalized (stored) form.
    /// </summary>
    /// <param name="nickname">The nickname as entered.</param>
    /// <param name="allowReserved">
    /// <see langword="true"/> for an administrator creating an account, who may deliberately use a
    /// <see cref="ReservedNicknames"/> entry (e.g. a second staff account). Self-registration passes
    /// <see langword="false"/>. Every other rule still applies.
    /// </param>
    public static ErrorOr<string> Validate(string? nickname, bool allowReserved = false)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return DomainError.Validation("Account.NicknameEmpty", "Nickname cannot be empty.");

        string normalized;
        try { normalized = Normalize(nickname); }
        catch (ArgumentException)
        {
            return DomainError.Validation("Account.NicknameInvalidCharacters", "Nickname contains characters that are not allowed.");
        }

        if (normalized.Length > ShortTextPolicy.MaxLength)
            return DomainError.Validation("Account.NicknameTooLong", "Nickname must be {0} characters or fewer.", ShortTextPolicy.MaxLength);

        if (ContainsDisallowedCharacter(normalized) || MixesLookalikeScripts(normalized))
            return DomainError.Validation("Account.NicknameInvalidCharacters", "Nickname contains characters that are not allowed.");

        if (!allowReserved && IsReserved(normalized))
            return DomainError.Validation("Account.NicknameReserved", "'{0}' cannot be used as a Nickname.", normalized);

        return normalized;
    }

    /// <summary>
    /// Characters that carry meaning in HTML markup or as an escape (<c>&lt; &gt; " ` \</c>). A nickname
    /// is a display name that is echoed into pages, mails and other names (an account's default
    /// calendar is named after it), so it never legitimately contains them. <c>&amp;</c> and
    /// <c>'</c> stay allowed ("R&amp;D팀", "O'Brien"); every render site still encodes.
    /// </summary>
    private static readonly HashSet<char> _markupAndEscapeCharacters = ['<', '>', '"', '`', '\\'];

    /// <summary>
    /// Markup / escape characters (see <see cref="_markupAndEscapeCharacters"/>), plus control, format
    /// (zero-width / bidirectional-override), private-use, unassigned and separator characters: the
    /// latter are not visible as a letter, so they can only be used to fake a lookalike.
    /// </summary>
    private static bool ContainsDisallowedCharacter(string value)
    {
        foreach (Rune rune in value.EnumerateRunes())
        {
            if (rune == Rune.ReplacementChar)
                return true;

            if (rune.IsBmp && _markupAndEscapeCharacters.Contains((char)rune.Value))
                return true;

            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format
                or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned or UnicodeCategory.Surrogate
                or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
                return true;
        }
        return false;
    }

    /// <summary>
    /// The classic homoglyph trick: Latin letters mixed with Cyrillic or Greek ones that render
    /// identically (<c>"аdmin"</c> with a Cyrillic а). A nickname is rejected when it combines Latin
    /// letters with letters from either of those scripts.
    /// </summary>
    private static bool MixesLookalikeScripts(string value)
    {
        bool latin = false, lookalike = false;
        foreach (char c in value.Where(char.IsLetter))
        {
            switch (c)
            {
                case (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= 'À' and <= 'ɏ'):
                    latin = true;
                    break;
                case (>= 'Ͱ' and <= 'Ͽ') or (>= 'Ѐ' and <= 'ԯ'):
                    lookalike = true;
                    break;
            }

            if (latin && lookalike)
                return true;
        }
        return false;
    }
}

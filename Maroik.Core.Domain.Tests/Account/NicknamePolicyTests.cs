using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Errors;
namespace Maroik.Core.Domain.Tests.Account;

/// <summary>
/// Unit tests for <see cref="NicknamePolicy"/>: normalization, character rules, lookalike-script
/// rejection and the reserved-name list.
/// </summary>
public class NicknamePolicyTests
{
    // -- Normalize ------------------------------------------------------------

    /// <summary>Normalize trims and collapses whitespace but keeps the display casing.</summary>
    [Theory]
    [InlineData("  Bob  ", "Bob")]
    [InlineData("Bob   Smith", "Bob Smith")]
    [InlineData("\tBob\u00A0\u3000Smith\n", "Bob Smith")]
    [InlineData("MiXeD", "MiXeD")]
    public void Normalize_TrimsAndCollapsesWhitespace_PreservingCase(string input, string expected)
        => Assert.Equal(expected, NicknamePolicy.Normalize(input));

    /// <summary>Normalize folds full-width letters to ASCII (NFKC).</summary>
    [Fact]
    public void Normalize_FoldsFullWidthLetters()
        => Assert.Equal("Bob", NicknamePolicy.Normalize("Ｂｏｂ"));

    /// <summary>Normalize returns empty for null / empty input.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Normalize_ReturnsEmpty_ForNullOrEmpty(string? input)
        => Assert.Equal("", NicknamePolicy.Normalize(input));

    // -- Validate: success ----------------------------------------------------

    /// <summary>Validate returns the normalized (stored) form.</summary>
    [Fact]
    public void Validate_ReturnsNormalizedNickname()
    {
        var result = NicknamePolicy.Validate("  Ｂｏｂ   Smith ");

        Assert.False(result.IsError);
        Assert.Equal("Bob Smith", result.Value);
    }

    /// <summary>Ordinary Latin, Hangul, digit and emoji nicknames are accepted.</summary>
    [Theory]
    [InlineData("Bob")]
    [InlineData("홍길동")]
    [InlineData("user_123")]
    [InlineData("Bob 🙂")]
    [InlineData("Admin1")]
    [InlineData("Administrators of fun")]
    [InlineData("O'Brien")]        // apostrophe stays allowed
    [InlineData("R&D팀")]          // ampersand stays allowed
    [InlineData("Kim & Lee")]
    public void Validate_AcceptsOrdinaryNicknames(string nickname)
        => Assert.False(NicknamePolicy.Validate(nickname).IsError);

    /// <summary>A nickname exactly at the 255-character limit is accepted.</summary>
    [Fact]
    public void Validate_AcceptsNicknameAtMaxLength()
        => Assert.False(NicknamePolicy.Validate(new string('n', 255)).IsError);

    // -- Validate: failures ---------------------------------------------------

    /// <summary>Empty / whitespace-only nicknames are rejected as empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Validate_RejectsEmpty(string? nickname)
    {
        var result = NicknamePolicy.Validate(nickname);

        Assert.True(result.IsError);
        Assert.Equal("Account.NicknameEmpty", result.FirstError.Code);
    }

    /// <summary>A nickname over 255 characters is rejected.</summary>
    [Fact]
    public void Validate_RejectsTooLong()
    {
        var result = NicknamePolicy.Validate(new string('n', 256));

        Assert.True(result.IsError);
        Assert.Equal("Account.NicknameTooLong", result.FirstError.Code);
    }

    /// <summary>Invisible / control / bidi-override characters are rejected.</summary>
    [Theory]
    [InlineData("Bo\u200Bb")]      // zero-width space
    [InlineData("Bo\u200Db")]      // zero-width joiner
    [InlineData("Bo\uFEFFb")]      // BOM / zero-width no-break space
    [InlineData("Bo\u202Eb")]      // right-to-left override
    // ReSharper disable once CanSimplifyStringEscapeSequence
    [InlineData("Bo\u0007b")]      // bell (control)
    [InlineData("Bo\uE000b")]      // private use
    [InlineData("Bo\uD800b")]      // lone surrogate
    public void Validate_RejectsInvisibleAndControlCharacters(string nickname)
    {
        var result = NicknamePolicy.Validate(nickname);

        Assert.True(result.IsError);
        Assert.Equal("Account.NicknameInvalidCharacters", result.FirstError.Code);
    }

    /// <summary>
    /// HTML markup and escape characters (<c>&lt; &gt; " ` \</c>) are rejected — including the full-width
    /// forms, which NFKC folds to the ASCII ones before the check — for self-registration and for an
    /// administrator creating an account alike.
    /// </summary>
    [Theory]
    [InlineData("a<b")]
    [InlineData("<b>x</b>")]
    [InlineData("a>b")]
    [InlineData("say \"hi\"")]
    [InlineData("a`b")]
    [InlineData("a\\b")]
    [InlineData("a＜b")]       // full-width less-than sign
    [InlineData("a＞b")]       // full-width greater-than sign
    public void Validate_RejectsMarkupAndEscapeCharacters(string nickname)
    {
        foreach (bool allowReserved in new[] { false, true })
        {
            var result = NicknamePolicy.Validate(nickname, allowReserved);

            Assert.True(result.IsError);
            Assert.Equal("Account.NicknameInvalidCharacters", result.FirstError.Code);
        }
    }

    /// <summary>Latin letters mixed with Cyrillic or Greek lookalikes are rejected.</summary>
    [Theory]
    [InlineData("\u0430dmin")]     // Cyrillic а + "dmin"
    [InlineData("Bob\u03BF")]      // Latin + Greek omicron
    [InlineData("pa\u0443pal")]    // Latin + Cyrillic у
    public void Validate_RejectsMixedLookalikeScripts(string nickname)
    {
        var result = NicknamePolicy.Validate(nickname, allowReserved: true);

        Assert.True(result.IsError);
        Assert.Equal("Account.NicknameInvalidCharacters", result.FirstError.Code);
    }

    /// <summary>A nickname entirely in one non-Latin script is fine.</summary>
    [Theory]
    [InlineData("Иван")]
    [InlineData("Ελένη")]
    public void Validate_AcceptsSingleNonLatinScript(string nickname)
        => Assert.False(NicknamePolicy.Validate(nickname).IsError);

    // -- Reserved names -------------------------------------------------------

    /// <summary>Every reserved name is rejected for self-registration, however it is dressed up.</summary>
    [Theory]
    [InlineData("admin")]
    [InlineData("Admin")]
    [InlineData("ADMIN")]
    [InlineData("  admin  ")]
    [InlineData("A d m i n")]
    [InlineData("a.d.m.i.n")]
    [InlineData("admin_")]
    [InlineData("Ａｄｍｉｎ")]
    [InlineData("Login")]
    [InlineData("Maroik")]
    [InlineData("관리자")]
    [InlineData("관 리 자")]
    [InlineData("Anonymous")]
    public void Validate_RejectsReservedNames(string nickname)
    {
        var result = NicknamePolicy.Validate(nickname);

        Assert.True(result.IsError);
        Assert.Equal("Account.NicknameReserved", result.FirstError.Code);
    }

    /// <summary>The reserved-name error carries the normalized nickname as its argument.</summary>
    [Fact]
    public void Validate_ReservedError_CarriesNormalizedNickname()
    {
        var result = NicknamePolicy.Validate("  ADMIN ");

        Assert.Equal("Account.NicknameReserved", result.FirstError.Code);
        Assert.Equal("'{0}' cannot be used as a Nickname.", result.FirstError.Metadata![DomainError.MessageTemplateMetadataKey]);
        Assert.Equal(["ADMIN"], (object[])result.FirstError.Metadata[DomainError.MessageArgsMetadataKey]);
    }

    /// <summary>An administrator may deliberately use a reserved name.</summary>
    [Theory]
    [InlineData("Admin")]
    [InlineData("관리자")]
    public void Validate_AllowsReservedNames_WhenAllowReservedIsTrue(string nickname)
        => Assert.False(NicknamePolicy.Validate(nickname, allowReserved: true).IsError);

    /// <summary><see cref="NicknamePolicy.IsReserved"/> reports the reserved names and nothing else.</summary>
    [Theory]
    [InlineData("admin", true)]
    [InlineData("ＡＤＭＩＮ", true)]
    [InlineData("Administrator", true)]
    [InlineData("Admin2", false)]
    [InlineData("Bob", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("___", false)]
    public void IsReserved_MatchesOnlyReservedNames(string? nickname, bool expected)
        => Assert.Equal(expected, NicknamePolicy.IsReserved(nickname));

    /// <summary>The anonymous placeholder nickname shown to signed-out visitors can never be registered.</summary>
    [Fact]
    public void ReservedNicknames_IncludeTheAnonymousPlaceholder()
        => Assert.True(NicknamePolicy.IsReserved("Login"));

    // -- Text that cannot be normalized (a lone surrogate) -----------------------------

    /// <summary>A lone surrogate cannot be normalized: the canonical key is empty (it never collides with anything).</summary>
    [Fact]
    public void CanonicalKey_IsEmpty_ForALoneSurrogate()
        => Assert.Equal("", NicknamePolicy.CanonicalKey("bad\uD800name"));

    /// <summary>A nickname with a lone surrogate is refused as containing characters that are not allowed.</summary>
    [Fact]
    public void Validate_RejectsALoneSurrogate()
    {
        var result = NicknamePolicy.Validate("bad\uD800name");

        Assert.True(result.IsError);
        Assert.Equal("Account.NicknameInvalidCharacters", result.FirstError.Code);
    }
}

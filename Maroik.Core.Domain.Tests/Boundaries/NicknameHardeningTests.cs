using Maroik.Core.Domain.Account;
namespace Maroik.Core.Domain.Tests.Boundaries;

/// <summary>
/// The parts of <see cref="NicknamePolicy"/> a reviewer cannot see from the happy path: the complete
/// reserved-name list, the exact edges of the Latin / Greek / Cyrillic ranges the homoglyph rule uses, and
/// the error contract. The expected lists are spelled out here independently of the code under test.
/// </summary>
public class NicknameHardeningTests
{
    /// <summary>The complete reserved-name list, spelled out independently of <see cref="NicknamePolicy.ReservedNicknames"/>.</summary>
    public static TheoryData<string> AllReservedNames =>
    [
        "admin", "administrator", "sysadmin", "root", "system", "moderator", "operator", "staff",
        "support", "official", "anonymous", "guest", "login", "null", "undefined", "maroik",
        "관리자", "운영자", "운영진", "어드민", "시스템", "익명", "게스트",
    ];

    /// <summary>Verifies that each reserved name is refused for self-registration but allowed when <c>allowReserved</c> is set.</summary>
    [Theory]
    [MemberData(nameof(AllReservedNames))]
    public void EveryReservedName_IsReserved_ForSelfRegistration_AndAllowedForAnAdministrator(string name)
    {
        Assert.True(NicknamePolicy.IsReserved(name));
        ErrorAssert.Validation(NicknamePolicy.Validate(name), "Account.NicknameReserved", "'{0}' cannot be used as a Nickname.", name);
        Assert.False(NicknamePolicy.Validate(name, allowReserved: true).IsError);
    }

    /// <summary>Verifies that the policy's reserved list matches <see cref="AllReservedNames"/> exactly (no extra, no missing names).</summary>
    [Fact]
    public void TheReservedListHasExactlyTheDocumentedNames()
    {
        Assert.Equal(AllReservedNames.Select(row => row.Data).Order().ToArray(), NicknamePolicy.ReservedNicknames.Order().ToArray());
    }

    // ---- range edges of the homoglyph rule ---------------------------------------------------------

    /// <summary>A Cyrillic letter, mixed with Latin letters to form a homoglyph nickname.</summary>
    private const string Cyrillic = "д";      // U+0434
    /// <summary>A plain Latin letter.</summary>
    private const string LatinLetter = "a";

    /// <summary>Verifies that Latin letters at both edges and the middle of each Latin range are refused next to a Cyrillic letter.</summary>
    [Theory]
    [InlineData("A")] [InlineData("M")] [InlineData("Z")]           // upper-case ASCII, both edges and the middle
    [InlineData("a")] [InlineData("m")] [InlineData("z")]           // lower-case ASCII
    [InlineData("À")] [InlineData("ÿ")] [InlineData("ɏ")]   // Latin-1 supplement / Extended-A/B: first, middle, last
    public void ALatinLetterMixedWithCyrillic_IsRefused(string latin)
    {
        ErrorAssert.Validation(NicknamePolicy.Validate(latin + Cyrillic), "Account.NicknameInvalidCharacters",
            "Nickname contains characters that are not allowed.");
    }

    /// <summary>Verifies that a letter just above the Latin range is not treated as Latin.</summary>
    [Theory]
    // (ª and º, the only letters below the range, NFKC-fold to the ASCII a / o, so they are Latin anyway.)
    [InlineData("ɐ")]   // ɐ  just above the Latin range (IPA extensions)
    public void ALetterJustOutsideTheLatinRange_MayBeMixedWithCyrillic(string outside)
    {
        Assert.False(NicknamePolicy.Validate(outside + Cyrillic).IsError);
    }

    /// <summary>Verifies that Greek and Cyrillic letters at the edges of their ranges are refused next to a Latin letter.</summary>
    [Theory]
    [InlineData("Ͱ")] [InlineData("α")] [InlineData("Ͽ")]   // Greek and Coptic: first, alpha, last
    [InlineData("Ѐ")] [InlineData("д")] [InlineData("ԯ")]   // Cyrillic and Supplement: first, middle, last
    public void ALookalikeScriptLetterMixedWithLatin_IsRefused(string lookalike)
    {
        ErrorAssert.Validation(NicknamePolicy.Validate(LatinLetter + lookalike), "Account.NicknameInvalidCharacters",
            "Nickname contains characters that are not allowed.");
    }

    /// <summary>Verifies that letters just outside the Greek / Cyrillic ranges may be mixed with Latin.</summary>
    [Theory]
    [InlineData("ʰ")]   // ʰ  just below Greek (modifier letter)
    [InlineData("Ա")]   // Ա  just above the Cyrillic supplement (Armenian)
    public void ALetterJustOutsideTheLookalikeRanges_MayBeMixedWithLatin(string outside)
    {
        Assert.False(NicknamePolicy.Validate(LatinLetter + outside).IsError);
    }

    /// <summary>Verifies that a symbol inside the Latin-1 block does not make a name count as Latin.</summary>
    [Fact]
    public void ASymbolInsideTheLatinBlock_IsNotALetter_SoItDoesNotCountAsLatin()
    {
        // U+00D7 MULTIPLICATION SIGN sits inside the Latin-1 letter block but is a symbol: it must not make the
        // name "Latin", or "×д" would be refused as a Latin/Cyrillic mix.
        Assert.False(NicknamePolicy.Validate("×" + Cyrillic).IsError);
    }

    // ---- error contract and canonical key --------------------------------------------------------

    /// <summary>Verifies the empty, invalid-character and too-long nickname errors.</summary>
    [Fact]
    public void ErrorContract()
    {
        ErrorAssert.Validation(NicknamePolicy.Validate(""), "Account.NicknameEmpty", "Nickname cannot be empty.");
        ErrorAssert.Validation(NicknamePolicy.Validate("ab\uD800"), "Account.NicknameInvalidCharacters", "Nickname contains characters that are not allowed.");
        ErrorAssert.Validation(NicknamePolicy.Validate("a<b"), "Account.NicknameInvalidCharacters", "Nickname contains characters that are not allowed.");
        ErrorAssert.Validation(NicknamePolicy.Validate(new string('x', 256)), "Account.NicknameTooLong", "Nickname must be {0} characters or fewer.", 255);
    }

    /// <summary>Verifies that a null or empty input has an empty canonical key and is not reserved.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CanonicalKey_OfNothing_IsEmpty(string? input)
    {
        Assert.Equal("", NicknamePolicy.CanonicalKey(input));
        Assert.False(NicknamePolicy.IsReserved(input));
    }

    /// <summary>Verifies that a string with a lone surrogate has an empty canonical key.</summary>
    [Fact]
    public void CanonicalKey_OfAMalformedString_IsEmpty()
    {
        Assert.Equal("", NicknamePolicy.CanonicalKey("bad\uD800"));
    }
}

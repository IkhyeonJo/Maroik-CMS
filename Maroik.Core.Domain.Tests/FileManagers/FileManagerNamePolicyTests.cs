using Maroik.Core.Domain.Errors;
using Maroik.Core.Domain.FileManagers;
namespace Maroik.Core.Domain.Tests.FileManagers;

/// <summary>
/// Unit tests for <see cref="FileManagerNamePolicy"/>: the file and folder name rule (S4) applied after NFKC normalization, and the
/// "Keep both" name a conflicting upload gets (F2-C, "a (1).txt").
/// </summary>
public class FileManagerNamePolicyTests
{
    /// <summary>An ordinary name is accepted as typed, including spaces, dots inside and non-ASCII text.</summary>
    [Theory]
    [InlineData("report.txt")]
    [InlineData("내 문서")]
    [InlineData("archive.tar.gz")]
    [InlineData(".bashrc")]
    [InlineData("a b  c")]
    public void Validate_AcceptsAnOrdinaryName(string name)
    {
        var result = FileManagerNamePolicy.Validate(name);

        Assert.False(result.IsError);
        Assert.Equal(name, result.Value);
    }

    /// <summary>The stored name is the NFKC form: a full-width letter becomes its ASCII form.</summary>
    [Fact]
    public void Validate_ReturnsTheNfkcForm()
    {
        Assert.Equal("ABC.txt", FileManagerNamePolicy.Validate("ＡＢＣ.txt").Value);
    }

    /// <summary>An empty or blank name is refused.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RefusesAnEmptyName(string? name)
    {
        Assert.Equal("FileManager.NameEmpty", FileManagerNamePolicy.Validate(name).FirstError.Code);
    }

    /// <summary>A name of 255 characters is accepted; 256 is refused with the limit as its value.</summary>
    [Fact]
    public void Validate_RefusesANameLongerThan255Characters()
    {
        var atLimit = FileManagerNamePolicy.Validate(new string('a', 255));
        var overLimit = FileManagerNamePolicy.Validate(new string('a', 256));

        Assert.False(atLimit.IsError);
        Assert.Equal("FileManager.NameTooLong", overLimit.FirstError.Code);
        Assert.Equal([255], (object[])overLimit.FirstError.Metadata![DomainError.MessageArgsMetadataKey]);
    }

    /// <summary>The length limit is judged on the normalized name (a ligature expands under NFKC).</summary>
    [Fact]
    public void Validate_JudgesTheLengthAfterNormalization()
    {
        // U+FB03 "ﬃ" is one character that NFKC turns into three ("ffi").
        Assert.Equal("FileManager.NameTooLong", FileManagerNamePolicy.Validate(new string('ﬃ', 100)).FirstError.Code);
    }

    /// <summary>Each character Windows refuses in a name is refused, and so is a control character.</summary>
    [Theory]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    [InlineData("a*b")]
    [InlineData("a?b")]
    [InlineData("a\"b")]
    [InlineData("a<b")]
    [InlineData("a>b")]
    [InlineData("a|b")]
    [InlineData("a\tb")]
    [InlineData("a\nb")]
    [InlineData("a\u0000b")]
    [InlineData("a\u007Fb")]
    public void Validate_RefusesAForbiddenCharacter(string name)
    {
        Assert.Equal("FileManager.NameInvalidCharacter", FileManagerNamePolicy.Validate(name).FirstError.Code);
    }

    /// <summary>A full-width slash normalizes to "/" and is refused like it.</summary>
    [Fact]
    public void Validate_RefusesAForbiddenCharacter_ThatAppearsOnlyAfterNormalization()
    {
        Assert.Equal("FileManager.NameInvalidCharacter", FileManagerNamePolicy.Validate("a／b").FirstError.Code);
    }

    /// <summary>"." and ".." are refused.</summary>
    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public void Validate_RefusesTheDotNames(string name)
    {
        Assert.Equal("FileManager.NameReserved", FileManagerNamePolicy.Validate(name).FirstError.Code);
    }

    /// <summary>A trailing dot or space is refused; a leading space or dot is not.</summary>
    [Theory]
    [InlineData("name.")]
    [InlineData("name ")]
    public void Validate_RefusesATrailingDotOrSpace(string name)
    {
        Assert.Equal("FileManager.NameTrailingDotOrSpace", FileManagerNamePolicy.Validate(name).FirstError.Code);
    }

    // -- KeepBothName -------------------------------------------------------------

    /// <summary>The first free "(n)" suffix goes before the extension.</summary>
    [Fact]
    public void KeepBothName_AddsTheFirstFreeNumber_BeforeTheExtension()
    {
        Assert.Equal("a (1).txt", FileManagerNamePolicy.KeepBothName("a.txt", ["a.txt"]));
        Assert.Equal("a (3).txt", FileManagerNamePolicy.KeepBothName("a.txt", ["a.txt", "a (1).txt", "a (2).txt"]));
    }

    /// <summary>A name without an extension, or one that is all extension (".bashrc"), gets the suffix at its end.</summary>
    [Theory]
    [InlineData("Design", "Design (1)")]
    [InlineData(".bashrc", ".bashrc (1)")]
    public void KeepBothName_AddsTheSuffixAtTheEnd_WithoutAStem(string name, string expected)
    {
        Assert.Equal(expected, FileManagerNamePolicy.KeepBothName(name, [name]));
    }

    /// <summary>Only the last extension is kept apart ("archive.tar (1).gz", as Windows names it).</summary>
    [Fact]
    public void KeepBothName_KeepsOnlyTheLastExtension()
    {
        Assert.Equal("archive.tar (1).gz", FileManagerNamePolicy.KeepBothName("archive.tar.gz", ["archive.tar.gz"]));
    }

    /// <summary>A long name is shortened so the result still fits the 255-character limit.</summary>
    [Fact]
    public void KeepBothName_StaysWithinTheLengthLimit()
    {
        string name = new string('a', 251) + ".txt";

        string result = FileManagerNamePolicy.KeepBothName(name, [name]);

        Assert.Equal(255, result.Length);
        Assert.EndsWith(" (1).txt", result, StringComparison.Ordinal);
        Assert.False(FileManagerNamePolicy.Validate(result).IsError);
    }
}

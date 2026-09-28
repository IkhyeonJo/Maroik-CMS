using Maroik.Core.Domain.ValueObjects;
namespace Maroik.Core.Domain.Tests.ValueObjects;

/// <summary>
/// Unit tests for <see cref="Maroik.Core.Domain.ValueObjects.Email"/>.
/// Covers creation with valid inputs, empty/invalid format rejections, lower-case normalization, and equality.
/// </summary>
public class EmailTests
{
    /// <summary>Create returns email, when valid.</summary>
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("USER@EXAMPLE.COM")]
    [InlineData("user.name+tag@sub.domain.org")]
    public void Create_ReturnsEmail_WhenValid(string input)
    {
        var result = Email.Create(input);

        Assert.False(result.IsError);
        Assert.Equal(input.ToLowerInvariant(), result.Value.Value);
    }

    /// <summary>An address exactly at the persisted 255-character limit is accepted.</summary>
    [Fact]
    public void Create_ReturnsEmail_WhenAtTheMaximumLength()
    {
        string address = new string('a', Email.MaxLength - "@example.com".Length) + "@example.com";
        Assert.Equal(255, address.Length);

        Assert.False(Email.Create(address).IsError);
    }

    /// <summary>An address over 255 characters is a clean validation error (the column would reject it with SQLSTATE 22001).</summary>
    [Fact]
    public void Create_ReturnsError_WhenLongerThanTheColumn()
    {
        string address = new string('a', Email.MaxLength - "@example.com".Length + 1) + "@example.com";
        Assert.Equal(256, address.Length);

        var result = Email.Create(address);

        Assert.True(result.IsError);
        Assert.Equal("Email.TooLong", result.FirstError.Code);
    }

    /// <summary>Create returns error, when empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ReturnsError_WhenEmpty(string? input)
    {
        var result = Email.Create(input);

        Assert.True(result.IsError);
        Assert.Equal("Email.Empty", result.FirstError.Code);
    }

    /// <summary>Create returns error, when invalid format.</summary>
    [Theory]
    [InlineData("notanemail")]
    [InlineData("@nodomain")]
    [InlineData("noatsign.com")]
    [InlineData("missing@tld")]
    public void Create_ReturnsError_WhenInvalidFormat(string input)
    {
        var result = Email.Create(input);

        Assert.True(result.IsError);
        Assert.Equal("Email.Invalid", result.FirstError.Code);
    }

    /// <summary>
    /// The "invalid format" error is built via <c>LocalizableError</c>: its Metadata carries the
    /// composite-format resource template and the raw offending value separately, so
    /// <c>ServiceResult.FromError</c> can hand the UI something resx-localizable instead of the
    /// already-baked, value-embedding sentence in <see cref="ErrorOr.Error.Description"/>.
    /// </summary>
    [Fact]
    public void Create_ReturnsError_WithLocalizableMetadata_WhenInvalidFormat()
    {
        var result = Email.Create("notanemail");

        Assert.Equal("'notanemail' is not a valid email address.", result.FirstError.Description);
        Assert.Equal("'{0}' is not a valid email address.", result.FirstError.Metadata!["ResourceKey"]);
        Assert.Equal(["notanemail"], (object[])result.FirstError.Metadata["ResourceArgs"]);
    }

    /// <summary>Create normalizes to lower case.</summary>
    [Fact]
    public void Create_NormalizesToLowerCase()
    {
        var result = Email.Create("User@Example.COM");

        Assert.False(result.IsError);
        Assert.Equal("user@example.com", result.Value.Value);
    }

    /// <summary>Equality same value are equal.</summary>
    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = Email.Create("user@example.com").Value;
        var b = Email.Create("user@example.com").Value;

        Assert.Equal(a, b);
    }

    /// <summary>Equality different values are not equal.</summary>
    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = Email.Create("a@example.com").Value;
        var b = Email.Create("b@example.com").Value;

        Assert.NotEqual(a, b);
    }

    // -- Create: the address shapes MailAddress accepts but the value object refuses ----------------

    /// <summary>The display-name form ("Jane &lt;jane@example.com&gt;") is not a bare address.</summary>
    [Fact]
    public void Create_RejectsTheDisplayNameForm()
    {
        var result = Email.Create("Jane <jane@example.com>");

        Assert.True(result.IsError);
        Assert.Equal("Email.Invalid", result.FirstError.Code);
    }

    /// <summary>A host with an empty label, a one-letter top-level domain, or a quoted local part with a space is refused.</summary>
    [Theory]
    [InlineData("user@example.com.")]
    [InlineData("user@example.c")]
    [InlineData("\"a b\"@example.com")]
    [InlineData("user..name@example.com")]
    [InlineData(".user@example.com")]
    public void Create_RejectsMalformedHostsAndLocalParts(string input)
    {
        var result = Email.Create(input);

        Assert.True(result.IsError);
        Assert.Equal("Email.Invalid", result.FirstError.Code);
    }

    /// <summary>ToString is the normalized (lower-case) address.</summary>
    [Fact]
    public void ToString_IsTheNormalizedAddress()
    {
        Assert.Equal("user@example.com", Email.Create("USER@Example.com").Value.ToString());
    }
}

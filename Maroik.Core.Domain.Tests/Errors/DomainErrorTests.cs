using ErrorOr;
using Maroik.Core.Domain.Errors;
// ReSharper disable InvalidXmlDocComment
namespace Maroik.Core.Domain.Tests.Errors;

/// <summary>Unit tests for <see cref="DomainError"/>.</summary>
public class DomainErrorTests
{
    /// <summary>
    /// <see cref="Error.Description"/> is the template with the args already substituted in --
    /// unchanged from what a plain <c>Error.Validation</c> call would have produced --
    /// so logs and any non-localizing caller still see a normal, readable message.
    /// </summary>
    [Fact]
    public void Validation_DescriptionIsTheFormattedMessage()
    {
        Error error = DomainError.Validation("TimeZoneId.Invalid", "'{0}' is not a recognised time-zone ID.", "bogus/zone");

        Assert.Equal("TimeZoneId.Invalid", error.Code);
        Assert.Equal("'bogus/zone' is not a recognised time-zone ID.", error.Description);
        Assert.Equal(ErrorType.Validation, error.Type);
    }

    /// <summary>The unformatted template and the original args survive separately in Metadata.</summary>
    [Fact]
    public void Validation_MetadataCarriesTemplateAndArgsSeparately()
    {
        Error error = DomainError.Validation("FixedIncome.DepositDay", "Deposit day must be between 1 and {0} for month {1}.", 28, 2);

        Assert.NotNull(error.Metadata);
        Assert.Equal("Deposit day must be between 1 and {0} for month {1}.", error.Metadata![DomainError.MessageTemplateMetadataKey]);
        Assert.Equal([28, 2], (object[])error.Metadata[DomainError.MessageArgsMetadataKey]);
    }

    /// <summary>A template with no placeholders (no args supplied) still round-trips cleanly.</summary>
    [Fact]
    public void Validation_WithNoArgs_LeavesTemplateUnformatted()
    {
        Error error = DomainError.Validation("Some.Code", "A static message.");

        Assert.Equal("A static message.", error.Description);
        Assert.Equal("A static message.", error.Metadata![DomainError.MessageTemplateMetadataKey]);
        Assert.Empty((object[])error.Metadata[DomainError.MessageArgsMetadataKey]);
    }

    /// <summary>
    /// <see cref="DomainError.Conflict"/> follows the same code/description/metadata/type
    /// pattern as <see cref="DomainError.Validation"/>, just with <see cref="ErrorType.Conflict"/>.
    /// </summary>
    [Fact]
    public void Conflict_BuildsAConflictError_WithTemplateAndArgsInMetadata()
    {
        Error error = DomainError.Conflict("Board.Deleted", "Cannot add a comment to a deleted post.");

        Assert.Equal("Board.Deleted", error.Code);
        Assert.Equal("Cannot add a comment to a deleted post.", error.Description);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal("Cannot add a comment to a deleted post.", error.Metadata![DomainError.MessageTemplateMetadataKey]);
        Assert.Empty((object[])error.Metadata[DomainError.MessageArgsMetadataKey]);
    }

    /// <summary>
    /// <see cref="DomainError.Failure"/> follows the same code/description/metadata/type
    /// pattern as <see cref="DomainError.Validation"/>, just with <see cref="ErrorType.Failure"/>.
    /// </summary>
    [Fact]
    public void Failure_BuildsAFailureError_WithTemplateAndArgsInMetadata()
    {
        Error error = DomainError.Failure("Account.NotConfirmed", "Email must be confirmed before resetting the password.");

        Assert.Equal("Account.NotConfirmed", error.Code);
        Assert.Equal("Email must be confirmed before resetting the password.", error.Description);
        Assert.Equal(ErrorType.Failure, error.Type);
        Assert.Equal("Email must be confirmed before resetting the password.", error.Metadata![DomainError.MessageTemplateMetadataKey]);
        Assert.Empty((object[])error.Metadata[DomainError.MessageArgsMetadataKey]);
    }
}

using ErrorOr;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Localization;

namespace Maroik.Core.Contract.Tests.Dtos;

/// <summary>
/// Unit tests for <see cref="ServiceResult"/> — the factory methods and the
/// <see cref="ServiceResult.FromError"/> mapping that lets callers react to the category of a
/// failure (validation / not-found / conflict / unexpected) rather than only "it failed".
/// </summary>
public class ServiceResultTests
{
    /// <summary>Ok produces a success result with no error metadata.</summary>
    [Fact]
    public void Ok_IsSuccess_WithNoError()
    {
        var result = ServiceResult.Ok();

        Assert.True(result.Success);
        Assert.Equal(ServiceErrorType.None, result.ErrorType);
        Assert.Equal("", result.ErrorKey);
        Assert.Equal("", result.ErrorCode);
    }

    /// <summary>The legacy single-argument Fail keeps working and defaults to the Validation category.</summary>
    [Fact]
    public void Fail_SingleArg_DefaultsToValidation()
    {
        var result = ServiceResult.Fail("Input is invalid");

        Assert.False(result.Success);
        Assert.Equal("Input is invalid", result.ErrorKey);
        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    }

    /// <summary>NotFound / Conflict / Failure carry their category and both code and message.</summary>
    [Theory]
    [InlineData(nameof(ServiceResult.NotFound), ServiceErrorType.NotFound)]
    [InlineData(nameof(ServiceResult.Conflict), ServiceErrorType.Conflict)]
    [InlineData(nameof(ServiceResult.Failure), ServiceErrorType.Failure)]
    public void CategoryFactories_SetTypeCodeAndMessage(string factory, ServiceErrorType expected)
    {
        ServiceResult result = factory switch
        {
            nameof(ServiceResult.NotFound) => ServiceResult.NotFound("X.NotFound", "not here"),
            nameof(ServiceResult.Conflict) => ServiceResult.Conflict("X.Conflict", "already there"),
            _ => ServiceResult.Failure("X.Boom", "boom"),
        };

        Assert.False(result.Success);
        Assert.Equal(expected, result.ErrorType);
        Assert.StartsWith("X.", result.ErrorCode);
        Assert.NotEqual("", result.ErrorKey);
    }

    /// <summary>FromError maps each domain <see cref="ErrorType"/> the app uses to the matching category.</summary>
    [Theory]
    [InlineData(ErrorType.Validation, ServiceErrorType.Validation)]
    [InlineData(ErrorType.NotFound, ServiceErrorType.NotFound)]
    [InlineData(ErrorType.Conflict, ServiceErrorType.Conflict)]
    [InlineData(ErrorType.Failure, ServiceErrorType.Failure)]
    [InlineData(ErrorType.Unexpected, ServiceErrorType.Failure)]
    public void FromError_MapsErrorTypeToCategory(ErrorType errorType, ServiceErrorType expected)
    {
        Error error = errorType switch
        {
            ErrorType.Validation => Error.Validation("E.Code", "desc"),
            ErrorType.NotFound => Error.NotFound("E.Code", "desc"),
            ErrorType.Conflict => Error.Conflict("E.Code", "desc"),
            ErrorType.Failure => Error.Failure("E.Code", "desc"),
            _ => Error.Unexpected("E.Code", "desc"),
        };

        var result = ServiceResult.FromError(error);

        Assert.False(result.Success);
        Assert.Equal(expected, result.ErrorType);
        Assert.Equal("E.Code", result.ErrorCode);
        Assert.Equal("desc", result.ErrorKey);
        Assert.Empty(result.ErrorArgs);
    }

    /// <summary>The category factories accept optional composite-format args and carry them through unchanged.</summary>
    [Fact]
    public void Validation_WithArgs_PopulatesErrorArgs()
    {
        var result = ServiceResult.Validation("X.TooLarge", "File Size must be smaller than {0}MB.", 10);

        Assert.False(result.Success);
        Assert.Equal("File Size must be smaller than {0}MB.", result.ErrorKey);
        Assert.Equal([10], result.ErrorArgs);
    }

    /// <summary>
    /// FromError on a plain domain <see cref="Error"/> (no <see cref="LocalizableError"/> metadata) falls
    /// back to the already-formatted <see cref="Error.Description"/> with no args -- the pre-existing
    /// behavior every other caller of FromError still relies on.
    /// </summary>
    [Fact]
    public void FromError_WithoutLocalizationMetadata_HasEmptyErrorArgs()
    {
        var result = ServiceResult.FromError(Error.Validation("Plain.Code", "already-formatted message"));

        Assert.Equal("already-formatted message", result.ErrorKey);
        Assert.Empty(result.ErrorArgs);
    }

    /// <summary>
    /// FromError on an <see cref="Error"/> built via <see cref="LocalizableError.Validation"/> surfaces
    /// the composite-format resource template in <see cref="ServiceResult.ErrorKey"/> and the original
    /// (unformatted) values in <see cref="ServiceResult.ErrorArgs"/> -- not the already-baked sentence --
    /// so the caller can hand both straight to an <c>IHtmlLocalizer</c> indexer.
    /// </summary>
    [Fact]
    public void FromError_WithLocalizationMetadata_SurfacesTemplateAndArgs()
    {
        Error error = LocalizableError.Validation("TimeZoneId.Invalid", "'{0}' is not a recognised time-zone ID.", "Mars/OlympusMons");

        var result = ServiceResult.FromError(error);

        Assert.Equal("TimeZoneId.Invalid", result.ErrorCode);
        Assert.Equal("'{0}' is not a recognised time-zone ID.", result.ErrorKey);
        Assert.Equal(["Mars/OlympusMons"], result.ErrorArgs);
    }
}

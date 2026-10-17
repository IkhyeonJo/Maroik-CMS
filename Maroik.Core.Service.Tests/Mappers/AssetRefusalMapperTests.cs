using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="AssetRefusalMapper"/>.</summary>
public class AssetRefusalMapperTests
{
    /// <summary>Each refusal maps to its own code (prefixed by the record type), message key and error type.</summary>
    [Theory]
    [InlineData(AssetRefusal.NotFound, "Expenditure.AssetNotFound", ServiceErrorKeys.SelectedAssetNotFound, ServiceErrorType.NotFound)]
    [InlineData(AssetRefusal.Deleted, "Expenditure.AssetDeleted", ServiceErrorKeys.AssetAlreadyDeleted, ServiceErrorType.Conflict)]
    [InlineData(AssetRefusal.CurrencyMismatch, "Expenditure.CurrencyMismatch", ServiceErrorKeys.CurrencyMismatch, ServiceErrorType.Validation)]
    public void ToResult_MapsEachRefusal(AssetRefusal refusal, string code, string key, ServiceErrorType type)
    {
        ServiceResult result = AssetRefusalMapper.ToResult("Expenditure", refusal);

        Assert.False(result.Success);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(key, result.ErrorKey);
        Assert.Equal(type, result.ErrorType);
    }

    /// <summary>The code names the record type it is given.</summary>
    [Fact]
    public void ToResult_PrefixesTheCodeWithTheRecordType()
        => Assert.Equal("FixedIncome.AssetDeleted", AssetRefusalMapper.ToResult("FixedIncome", AssetRefusal.Deleted).ErrorCode);

    /// <summary>
    /// Every member of <see cref="AssetRefusal"/> has a mapping, so a refusal added to the domain fails here until it
    /// gets one instead of reaching the throw at run time.
    /// </summary>
    [Fact]
    public void ToResult_MapsEveryRefusal()
    {
        foreach (AssetRefusal refusal in Enum.GetValues<AssetRefusal>())
            Assert.False(AssetRefusalMapper.ToResult("Income", refusal).Success);
    }

    /// <summary>A value outside the enum is a programming error and throws rather than guessing an answer.</summary>
    [Fact]
    public void ToResult_Throws_ForAValueOutsideTheEnum()
        => Assert.Throws<ArgumentOutOfRangeException>(() => AssetRefusalMapper.ToResult("Income", (AssetRefusal)99));
}

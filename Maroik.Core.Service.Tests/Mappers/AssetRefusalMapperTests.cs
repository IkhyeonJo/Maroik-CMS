using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Finances;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="AssetRefusalMapper"/>.</summary>
public class AssetRefusalMapperTests
{
    /// <summary>Each refusal maps to the record type's code for it, its message key and error type.</summary>
    [Theory]
    [InlineData(AssetRefusal.NotFound, "Expenditure.AssetNotFound", ServiceErrorKeys.SelectedAssetNotFound, ServiceErrorType.NotFound)]
    [InlineData(AssetRefusal.Deleted, "Expenditure.AssetDeleted", ServiceErrorKeys.AssetAlreadyDeleted, ServiceErrorType.Conflict)]
    [InlineData(AssetRefusal.CurrencyMismatch, "Expenditure.CurrencyMismatch", ServiceErrorKeys.CurrencyMismatch, ServiceErrorType.Validation)]
    public void ToResult_MapsEachRefusal(AssetRefusal refusal, string code, string key, ServiceErrorType type)
    {
        ServiceResult result = AssetRefusalMapper.ToResult(AssetRefusalMapper.Expenditure, refusal);

        Assert.False(result.Success);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(key, result.ErrorKey);
        Assert.Equal(type, result.ErrorType);
    }

    /// <summary>Each record type carries its own codes, written out in full.</summary>
    [Fact]
    public void EachRecordType_HasItsOwnCodes()
    {
        Assert.Equal(new AssetRefusalMapper.Codes("Income.AssetNotFound", "Income.AssetDeleted"), AssetRefusalMapper.Income);
        Assert.Equal(new AssetRefusalMapper.Codes("Expenditure.AssetNotFound", "Expenditure.AssetDeleted", "Expenditure.CurrencyMismatch"), AssetRefusalMapper.Expenditure);
        Assert.Equal(new AssetRefusalMapper.Codes("FixedIncome.AssetNotFound", "FixedIncome.AssetDeleted"), AssetRefusalMapper.FixedIncome);
        Assert.Equal(new AssetRefusalMapper.Codes("FixedExpenditure.AssetNotFound", "FixedExpenditure.AssetDeleted", "FixedExpenditure.CurrencyMismatch"), AssetRefusalMapper.FixedExpenditure);
    }

    /// <summary>
    /// Every member of <see cref="AssetRefusal"/> has a mapping, so a refusal added to the domain fails here until it
    /// gets one instead of reaching the throw at run time.
    /// </summary>
    [Fact]
    public void ToResult_MapsEveryRefusal()
    {
        foreach (AssetRefusal refusal in Enum.GetValues<AssetRefusal>())
            Assert.False(AssetRefusalMapper.ToResult(AssetRefusalMapper.Expenditure, refusal).Success);
    }

    /// <summary>An income names a single asset, so it has no currency-mismatch code, and answering one is a programming error.</summary>
    [Fact]
    public void ToResult_Throws_ForACurrencyMismatchOfARecordTypeWithoutThatCode()
        => Assert.Throws<ArgumentOutOfRangeException>(() => AssetRefusalMapper.ToResult(AssetRefusalMapper.Income, AssetRefusal.CurrencyMismatch));

    /// <summary>
    /// No refusal (a check that passed) or a value outside the enum is a programming error and throws rather than
    /// guessing an answer.
    /// </summary>
    [Fact]
    public void ToResult_Throws_WithoutARefusal_OrForAValueOutsideTheEnum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AssetRefusalMapper.ToResult(AssetRefusalMapper.Income, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => AssetRefusalMapper.ToResult(AssetRefusalMapper.Income, (AssetRefusal)99));
    }
}

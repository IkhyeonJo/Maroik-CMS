using Maroik.Core.Domain.Finance;
namespace Maroik.Core.Domain.Tests.Finance;

/// <summary>
/// Unit tests for <see cref="FinanceTextPolicy"/> -- the <c>Content</c>/<c>Note</c> length limit
/// shared by <see cref="Income"/>, <see cref="Expenditure"/>, <see cref="FixedIncome"/>, and
/// <see cref="FixedExpenditure"/>. Mirrors the persisted <c>character varying(255)</c> columns so a
/// too-long value surfaces as a clean validation error instead of a raw database exception.
/// </summary>
public class FinanceTextPolicyTests
{
    /// <summary>Null content/note is allowed -- both fields are optional.</summary>
    [Fact]
    public void ValidateContentAndNote_ReturnsSuccess_WhenBothAreNull()
    {
        var result = FinanceTextPolicy.ValidateContentAndNote(null, null);

        Assert.False(result.IsError);
    }

    /// <summary>Content/note exactly at the limit is accepted.</summary>
    [Fact]
    public void ValidateContentAndNote_ReturnsSuccess_WhenAtMaxLength()
    {
        string atMax = new('a', FinanceTextPolicy.MaxTextLength);

        var result = FinanceTextPolicy.ValidateContentAndNote(atMax, atMax);

        Assert.False(result.IsError);
    }

    /// <summary>Content longer than the limit is rejected with the "Finance.ContentTooLong" code.</summary>
    [Fact]
    public void ValidateContentAndNote_ReturnsError_WhenContentTooLong()
    {
        string tooLong = new('a', FinanceTextPolicy.MaxTextLength + 1);

        var result = FinanceTextPolicy.ValidateContentAndNote(tooLong, null);

        Assert.True(result.IsError);
        Assert.Equal("Finance.ContentTooLong", result.FirstError.Code);
        Assert.Equal($"Content must be {FinanceTextPolicy.MaxTextLength} characters or fewer.", result.FirstError.Description);
    }

    /// <summary>Note longer than the limit is rejected with the "Finance.NoteTooLong" code.</summary>
    [Fact]
    public void ValidateContentAndNote_ReturnsError_WhenNoteTooLong()
    {
        string tooLong = new('a', FinanceTextPolicy.MaxTextLength + 1);

        var result = FinanceTextPolicy.ValidateContentAndNote(null, tooLong);

        Assert.True(result.IsError);
        Assert.Equal("Finance.NoteTooLong", result.FirstError.Code);
        Assert.Equal($"Note must be {FinanceTextPolicy.MaxTextLength} characters or fewer.", result.FirstError.Description);
    }

    /// <summary>When both are too long, Content is checked (and reported) first.</summary>
    [Fact]
    public void ValidateContentAndNote_ReportsContentFirst_WhenBothTooLong()
    {
        string tooLong = new('a', FinanceTextPolicy.MaxTextLength + 1);

        var result = FinanceTextPolicy.ValidateContentAndNote(tooLong, tooLong);

        Assert.True(result.IsError);
        Assert.Equal("Finance.ContentTooLong", result.FirstError.Code);
    }
}

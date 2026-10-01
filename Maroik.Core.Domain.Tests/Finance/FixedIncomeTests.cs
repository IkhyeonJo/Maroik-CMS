using Maroik.Core.Domain.Finance;
namespace Maroik.Core.Domain.Tests.Finance;

/// <summary>
/// Unit tests for <see cref="Maroik.Core.Domain.Finance.FixedIncome"/>.
/// Covers registration validation (deposit day/month range, main class), unpunctuality flag management, and update logic.
/// </summary>
public class FixedIncomeTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Maturity date one year from now.</summary>
    private static readonly DateTime _maturity = Now.AddYears(1);

    /// <summary>A valid KRW salary deposited into "My Bank", scheduled for month 1, day 25.</summary>
    private static FixedIncome ValidFixedIncome() =>
        FixedIncome.Register("user@example.com", "RegularIncome", "LaborIncome", "Salary",
            3000000m, "KRW", "My Bank", 1, 25, _maturity, Now).Value;

    // -- Register -------------------------------------------------------------

    /// <summary>
    /// The maturity date is checked against the time passed in, not the machine clock: a date in
    /// 2000 is "today" when the given time is in 2000, and the created/updated stamps are that time.
    /// </summary>
    [Fact]
    public void Register_ChecksTheMaturityDate_AgainstTheGivenTime()
    {
        DateTime then = new(2000, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        var accepted = FixedIncome.Register("user@example.com", "RegularIncome", "LaborIncome", "Salary",
            1m, "KRW", "My Bank", 1, 25, then.AddDays(-1), then);
        var refused = FixedIncome.Register("user@example.com", "RegularIncome", "LaborIncome", "Salary",
            1m, "KRW", "My Bank", 1, 25, then.AddDays(-2), then);

        Assert.False(accepted.IsError);
        Assert.Equal(then, accepted.Value.Created);
        Assert.Equal(then, accepted.Value.Updated);
        Assert.Equal("FixedIncome.MaturityDateInPast", refused.FirstError.Code);
    }

    /// <summary>Register returns fixed income, when valid.</summary>
    [Fact]
    public void Register_ReturnsFixedIncome_WhenValid()
    {
        var result = FixedIncome.Register("user@example.com", "RegularIncome", "LaborIncome", "Salary",
            3000000m, "KRW", "My Bank", 1, 25, _maturity, Now);

        Assert.False(result.IsError);
        Assert.Equal(25, result.Value.DepositDay);
        Assert.Equal(1, result.Value.DepositMonth);
    }

    /// <summary>Register returns error, when deposit day out of range.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void Register_ReturnsError_WhenDepositDayOutOfRange(short day)
    {
        var result = FixedIncome.Register("user@example.com", "RegularIncome", "LaborIncome", null,
            0m, "KRW", "My Bank", 1, day, _maturity, Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedIncome.InvalidDepositDay", result.FirstError.Code);
    }

    /// <summary>Register returns error, when the day doesn't exist in the given month (e.g. April 31, February 30).</summary>
    [Theory]
    [InlineData(4, 31)]
    [InlineData(6, 31)]
    [InlineData(9, 31)]
    [InlineData(11, 31)]
    [InlineData(2, 30)]
    public void Register_ReturnsError_WhenDepositDayInvalidForMonth(short month, short day)
    {
        var result = FixedIncome.Register("user@example.com", "RegularIncome", "LaborIncome", null,
            0m, "KRW", "My Bank", month, day, _maturity, Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedIncome.InvalidDepositDay", result.FirstError.Code);
    }

    /// <summary>Register returns error, when deposit month out of range.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Register_ReturnsError_WhenDepositMonthOutOfRange(short month)
    {
        var result = FixedIncome.Register("user@example.com", "RegularIncome", "LaborIncome", null,
            0m, "KRW", "My Bank", month, 1, _maturity, Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedIncome.InvalidDepositMonth", result.FirstError.Code);
    }

    /// <summary>Register returns error, when main class empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Register_ReturnsError_WhenMainClassEmpty(string? mainClass)
    {
        var result = FixedIncome.Register("user@example.com", mainClass, "LaborIncome", null,
            0m, "KRW", "My Bank", 1, 1, _maturity, Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedIncome.MainClassEmpty", result.FirstError.Code);
    }

    /// <summary>Register returns error, when the maturity date is in the past.</summary>
    [Fact]
    public void Register_ReturnsError_WhenMaturityDateInPast()
    {
        var result = FixedIncome.Register("user@example.com", "RegularIncome", "LaborIncome", "Salary",
            3000000m, "KRW", "My Bank", 1, 25, Now.AddYears(-1), Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedIncome.MaturityDateInPast", result.FirstError.Code);
    }

    /// <summary>Register succeeds with the "no maturity date" sentinel.</summary>
    [Fact]
    public void Register_Succeeds_WithNoMaturityDateSentinel()
    {
        var result = FixedIncome.Register("user@example.com", "RegularIncome", "LaborIncome", "Salary",
            3000000m, "KRW", "My Bank", 1, 25, FixedSchedulePolicy.NoMaturityDate, Now);

        Assert.False(result.IsError);
    }

    // -- MarkUnpunctual / ClearUnpunctuality -----------------------------------

    /// <summary>Mark unpunctual sets flag.</summary>
    [Fact]
    public void MarkUnpunctual_SetsFlag()
    {
        var fi = ValidFixedIncome();

        fi.MarkUnpunctual(Now);

        Assert.True(fi.Unpunctuality);
    }

    /// <summary>Clear unpunctuality clears flag.</summary>
    [Fact]
    public void ClearUnpunctuality_ClearsFlag()
    {
        var fi = ValidFixedIncome();
        fi.MarkUnpunctual(Now);

        fi.ClearUnpunctuality(Now);

        Assert.False(fi.Unpunctuality);
    }

    // -- Update ----------------------------------------------------------------

    /// <summary>Update succeeds, when valid.</summary>
    [Fact]
    public void Update_Succeeds_WhenValid()
    {
        var fi = ValidFixedIncome();

        var result = fi.Update("RegularIncome", "PensionIncome", "Pension", 500000m, "KRW", "Pension Account", 1, 10, _maturity, "note", Now);

        Assert.False(result.IsError);
        Assert.Equal("PensionIncome", fi.SubClass);
        Assert.Equal(10, fi.DepositDay);
    }

    /// <summary>Update returns error, when deposit day out of range.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void Update_ReturnsError_WhenDepositDayOutOfRange(short day)
    {
        var fi = ValidFixedIncome();

        var result = fi.Update("RegularIncome", "LaborIncome", null, 0m, "KRW", "My Bank", 1, day, _maturity, null, Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedIncome.InvalidDepositDay", result.FirstError.Code);
    }

    /// <summary>Update returns error, when the day doesn't exist in the given month (e.g. April 31).</summary>
    [Fact]
    public void Update_ReturnsError_WhenDepositDayInvalidForMonth()
    {
        var fi = ValidFixedIncome();

        var result = fi.Update("RegularIncome", "LaborIncome", null, 0m, "KRW", "My Bank", 4, 31, _maturity, null, Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedIncome.InvalidDepositDay", result.FirstError.Code);
    }

    /// <summary>Update returns error, when the maturity date is newly moved into the past.</summary>
    [Fact]
    public void Update_ReturnsError_WhenMaturityDateMovedIntoThePast()
    {
        var fi = ValidFixedIncome();

        var result = fi.Update("RegularIncome", "LaborIncome", null, 0m, "KRW", "My Bank", 1, 10,
            Now.AddYears(-1), null, Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedIncome.MaturityDateInPast", result.FirstError.Code);
    }

    /// <summary>
    /// Update succeeds when an already-past maturity date is left unchanged, so an expired
    /// schedule's other fields stay editable.
    /// </summary>
    [Fact]
    public void Update_Succeeds_WhenPastMaturityDateLeftUnchanged()
    {
        var pastMaturity = Now.AddYears(-1);
        var fi = FixedIncome.Reconstitute(1, "user@example.com", "RegularIncome", "LaborIncome", "Salary",
            3000000m, "KRW", "My Bank", 1, 25, pastMaturity, null, false, Now.AddYears(-2), Now.AddYears(-2));

        var result = fi.Update("RegularIncome", "LaborIncome", "Salary raise", 3200000m, "KRW", "My Bank", 1, 25,
            pastMaturity, null, Now);

        Assert.False(result.IsError);
        Assert.Equal("Salary raise", fi.Content);
    }

    // -- Register: the other required text fields --------------------------------------

    /// <summary>Register refuses a blank subclass.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Register_ReturnsError_WhenSubClassEmpty(string? subClass)
    {
        var result = FixedIncome.Register("user@example.com", "RegularIncome", subClass, "Salary", 1m, "KRW", "My Bank", 1, 25, _maturity, Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedIncome.SubClassEmpty", result.FirstError.Code);
    }

    /// <summary>Register refuses a blank deposit asset.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Register_ReturnsError_WhenDepositAssetEmpty(string? asset)
    {
        var result = FixedIncome.Register("user@example.com", "RegularIncome", "LaborIncome", "Salary", 1m, "KRW", asset, 1, 25, _maturity, Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedIncome.DepositAssetEmpty", result.FirstError.Code);
    }
}

using Maroik.Core.Domain.Finance;
namespace Maroik.Core.Domain.Tests.Finance;

/// <summary>
/// Unit tests for <see cref="Maroik.Core.Domain.Finance.FixedExpenditure"/>.
/// Covers registration validation (deposit day/month range, payment method), unpunctuality flag management, and update logic.
/// </summary>
public class FixedExpenditureTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Maturity date one year from now.</summary>
    private static readonly DateTime _maturity = Now.AddYears(1);

    /// <summary>A valid KRW Netflix expenditure paid from "My Card", scheduled for month 1, day 1.</summary>
    private static FixedExpenditure ValidFixedExpenditure() =>
        FixedExpenditure.Register("user@example.com", "ConsumerSpending", "LeisureOrCulture", "Netflix",
            15000m, "KRW", "My Card", null, 1, 1, _maturity, Now).Value;

    // -- Register -------------------------------------------------------------

    /// <summary>Register returns fixed expenditure, when valid.</summary>
    [Fact]
    public void Register_ReturnsFixedExpenditure_WhenValid()
    {
        var result = FixedExpenditure.Register("user@example.com", "ConsumerSpending", "LeisureOrCulture", "Netflix",
            15000m, "KRW", "My Card", null, 1, 1, _maturity, Now);

        Assert.False(result.IsError);
        Assert.Equal(1, result.Value.DepositDay);
        Assert.Equal("My Card", result.Value.PaymentMethod);
    }

    /// <summary>Register returns error, when deposit day out of range.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void Register_ReturnsError_WhenDepositDayOutOfRange(short day)
    {
        var result = FixedExpenditure.Register("user@example.com", "ConsumerSpending", "LeisureOrCulture", null,
            0m, "KRW", "My Card", null, 1, day, _maturity, Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedExpenditure.InvalidDepositDay", result.FirstError.Code);
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
        var result = FixedExpenditure.Register("user@example.com", "ConsumerSpending", "LeisureOrCulture", null,
            0m, "KRW", "My Card", null, month, day, _maturity, Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedExpenditure.InvalidDepositDay", result.FirstError.Code);
    }

    /// <summary>Register returns error, when deposit month out of range.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Register_ReturnsError_WhenDepositMonthOutOfRange(short month)
    {
        var result = FixedExpenditure.Register("user@example.com", "ConsumerSpending", "LeisureOrCulture", null,
            0m, "KRW", "My Card", null, month, 1, _maturity, Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedExpenditure.InvalidDepositMonth", result.FirstError.Code);
    }

    /// <summary>Register returns error, when payment method empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Register_ReturnsError_WhenPaymentMethodEmpty(string? method)
    {
        var result = FixedExpenditure.Register("user@example.com", "ConsumerSpending", "LeisureOrCulture", null,
            0m, "KRW", method, null, 1, 1, _maturity, Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedExpenditure.PaymentMethodEmpty", result.FirstError.Code);
    }

    /// <summary>Register returns error, when the maturity date is in the past.</summary>
    [Fact]
    public void Register_ReturnsError_WhenMaturityDateInPast()
    {
        var result = FixedExpenditure.Register("user@example.com", "ConsumerSpending", "LeisureOrCulture", "Netflix",
            15000m, "KRW", "My Card", null, 1, 1, Now.AddYears(-1), Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedExpenditure.MaturityDateInPast", result.FirstError.Code);
    }

    /// <summary>Register succeeds with the "no maturity date" sentinel.</summary>
    [Fact]
    public void Register_Succeeds_WithNoMaturityDateSentinel()
    {
        var result = FixedExpenditure.Register("user@example.com", "ConsumerSpending", "LeisureOrCulture", "Netflix",
            15000m, "KRW", "My Card", null, 1, 1, FixedSchedulePolicy.NoMaturityDate, Now);

        Assert.False(result.IsError);
    }

    // -- MarkUnpunctual / ClearUnpunctuality -----------------------------------

    /// <summary>Mark unpunctual sets flag.</summary>
    [Fact]
    public void MarkUnpunctual_SetsFlag()
    {
        var fe = ValidFixedExpenditure();

        fe.MarkUnpunctual(Now);

        Assert.True(fe.Unpunctuality);
    }

    /// <summary>Clear unpunctuality clears flag.</summary>
    [Fact]
    public void ClearUnpunctuality_ClearsFlag()
    {
        var fe = ValidFixedExpenditure();
        fe.MarkUnpunctual(Now);

        fe.ClearUnpunctuality(Now);

        Assert.False(fe.Unpunctuality);
    }

    // -- Update ----------------------------------------------------------------

    /// <summary>Update succeeds, when valid.</summary>
    [Fact]
    public void Update_Succeeds_WhenValid()
    {
        var fe = ValidFixedExpenditure();

        var result = fe.Update("ConsumerSpending", "ProtectionTypeInsurance", "Car insurance", 80000m, "KRW", "Bank Account", null, 1, 15, _maturity, "note", Now);

        Assert.False(result.IsError);
        Assert.Equal("ProtectionTypeInsurance", fe.SubClass);
        Assert.Equal(15, fe.DepositDay);
    }

    /// <summary>Update returns error, when deposit day out of range.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void Update_ReturnsError_WhenDepositDayOutOfRange(short day)
    {
        var fe = ValidFixedExpenditure();

        var result = fe.Update("ConsumerSpending", "LeisureOrCulture", null, 0m, "KRW", "My Card", null, 1, day, _maturity, null, Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedExpenditure.InvalidDepositDay", result.FirstError.Code);
    }

    /// <summary>Update returns error, when the day doesn't exist in the given month (e.g. April 31).</summary>
    [Fact]
    public void Update_ReturnsError_WhenDepositDayInvalidForMonth()
    {
        var fe = ValidFixedExpenditure();

        var result = fe.Update("ConsumerSpending", "LeisureOrCulture", null, 0m, "KRW", "My Card", null, 4, 31, _maturity, null, Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedExpenditure.InvalidDepositDay", result.FirstError.Code);
    }

    /// <summary>Update returns error, when the maturity date is newly moved into the past.</summary>
    [Fact]
    public void Update_ReturnsError_WhenMaturityDateMovedIntoThePast()
    {
        var fe = ValidFixedExpenditure();

        var result = fe.Update("ConsumerSpending", "LeisureOrCulture", null, 0m, "KRW", "My Card", null, 1, 15,
            Now.AddYears(-1), null, Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedExpenditure.MaturityDateInPast", result.FirstError.Code);
    }

    /// <summary>
    /// Update succeeds when an already-past maturity date is left unchanged, so an expired
    /// schedule's other fields stay editable.
    /// </summary>
    [Fact]
    public void Update_Succeeds_WhenPastMaturityDateLeftUnchanged()
    {
        var pastMaturity = Now.AddYears(-1);
        var fe = FixedExpenditure.Reconstitute(1, "user@example.com", "ConsumerSpending", "LeisureOrCulture", "Netflix",
            15000m, "KRW", "My Card", null, 1, 1, pastMaturity, null, false, Now.AddYears(-2), Now.AddYears(-2));

        var result = fe.Update("ConsumerSpending", "LeisureOrCulture", "Netflix Premium", 20000m, "KRW", "My Card", null, 1, 1,
            pastMaturity, null, Now);

        Assert.False(result.IsError);
        Assert.Equal("Netflix Premium", fe.Content);
    }

    // -- Register: the required text fields ----------------------------------------------

    /// <summary>Register refuses a blank main class.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Register_ReturnsError_WhenMainClassEmpty(string? mainClass)
    {
        var result = FixedExpenditure.Register("user@example.com", mainClass, "LeisureOrCulture", "Netflix", 1m, "KRW", "My Card", null, 1, 1, _maturity, Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedExpenditure.MainClassEmpty", result.FirstError.Code);
    }

    /// <summary>Register refuses a blank subclass.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Register_ReturnsError_WhenSubClassEmpty(string? subClass)
    {
        var result = FixedExpenditure.Register("user@example.com", "ConsumerSpending", subClass, "Netflix", 1m, "KRW", "My Card", null, 1, 1, _maturity, Now);

        Assert.True(result.IsError);
        Assert.Equal("FixedExpenditure.SubClassEmpty", result.FirstError.Code);
    }
}

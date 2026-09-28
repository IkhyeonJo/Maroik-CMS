using Maroik.Core.Domain.Finance;
namespace Maroik.Core.Domain.Tests.Finance;

/// <summary>
/// Unit tests for <see cref="FinanceAmountPolicy"/> -- the <c>Amount</c> validation rule shared by
/// <see cref="Income"/>, <see cref="Expenditure"/>, <see cref="FixedIncome"/>, and
/// <see cref="FixedExpenditure"/>.
/// </summary>
public class FinanceAmountPolicyTests
{
    /// <summary>Zero and positive amounts are accepted.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(1_000_000)]
    public void ValidateNonNegative_ReturnsSuccess_WhenAmountIsZeroOrPositive(decimal amount)
    {
        var result = FinanceAmountPolicy.ValidateNonNegative(amount);

        Assert.False(result.IsError);
    }

    /// <summary>A negative amount is rejected with the shared "Finance.NegativeAmount" code.</summary>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1)]
    [InlineData(-1_000_000)]
    public void ValidateNonNegative_ReturnsError_WhenAmountIsNegative(decimal amount)
    {
        var result = FinanceAmountPolicy.ValidateNonNegative(amount);

        Assert.True(result.IsError);
        Assert.Equal("Finance.NegativeAmount", result.FirstError.Code);
        Assert.Equal("Amount cannot be negative.", result.FirstError.Description);
    }

    /// <summary>The largest absolute amount is what a <c>numeric(18,2)</c> column holds: 16 integer digits and 2 decimals.</summary>
    [Fact]
    public void MaxAbsoluteAmount_IsTheLargestNumeric18_2Value()
    {
        // (a decimal constant is stored as an attribute on a static field, so also force the type's static initialization)
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(FinanceAmountPolicy).TypeHandle);

        Assert.Equal(9_999_999_999_999_999.99m, FinanceAmountPolicy.MaxAbsoluteAmount);
    }
}

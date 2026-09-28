using Maroik.Core.Domain.Finance;
namespace Maroik.Core.Domain.Tests.Finance;

/// <summary>
/// Unit tests for <see cref="FinanceBreakdownPolicy"/> -- the per-sub-class amount/percentage
/// aggregation that backs the Dashboard summary (moved here, out of the Website mapper, so the
/// presentation layer never sums or divides amounts itself).
/// </summary>
public class FinanceBreakdownPolicyTests
{
    /// <summary>Compute sums each subclass's amounts and the grand total across every item.</summary>
    [Fact]
    public void Compute_SumsAmountsPerSubClass_AndGrandTotal()
    {
        var result = FinanceBreakdownPolicy.Compute([
            ("LaborIncome", 600m),
            ("BusinessIncome", 300m),
            ("LaborIncome", 100m) // same subclass appears twice -- must accumulate, not overwrite
        ]);

        Assert.Equal(1000m, result.Total);
        Assert.Equal(700m, result.AmountBySubClass["LaborIncome"]);
        Assert.Equal(300m, result.AmountBySubClass["BusinessIncome"]);
    }

    /// <summary>Compute derives each subclass's percentage share of the total.</summary>
    [Fact]
    public void Compute_DerivesPercentageShare_OfTotal()
    {
        var result = FinanceBreakdownPolicy.Compute([
            ("LaborIncome", 60m),
            ("BusinessIncome", 40m)
        ]);

        Assert.Equal(60.0, result.PercentageBySubClass["LaborIncome"]);
        Assert.Equal(40.0, result.PercentageBySubClass["BusinessIncome"]);
    }

    /// <summary>Compute leaves the percentage map empty when the total is zero, instead of dividing by zero.</summary>
    [Fact]
    public void Compute_ReturnsEmptyPercentages_WhenTotalIsZero()
    {
        var result = FinanceBreakdownPolicy.Compute([]);

        Assert.Equal(0m, result.Total);
        Assert.Empty(result.PercentageBySubClass);
    }

    /// <summary>Compute returns empty dictionaries for no items, rather than throwing.</summary>
    [Fact]
    public void Compute_ReturnsEmptyDictionaries_WhenNoItemsGiven()
    {
        var result = FinanceBreakdownPolicy.Compute([]);

        Assert.Empty(result.AmountBySubClass);
        Assert.Empty(result.PercentageBySubClass);
    }

    /// <summary>
    /// A null subclass is grouped under the empty-string key rather than throwing or being
    /// silently dropped from the total.
    /// </summary>
    [Fact]
    public void Compute_GroupsNullSubClass_UnderEmptyStringKey()
    {
        var result = FinanceBreakdownPolicy.Compute([
            ("LaborIncome", 50m),
            (null, 50m)
        ]);

        Assert.Equal(100m, result.Total);
        Assert.Equal(50m, result.AmountBySubClass[""]);
        Assert.Equal(50.0, result.PercentageBySubClass[""]);
    }

    /// <summary>
    /// A subclass outside any known taxonomy is still counted in the total (and therefore in every
    /// other subclass's percentage share) even though it gets no dedicated entry beyond its own —
    /// this is what keeps percentages summing to 100% if a new subclass is ever added upstream
    /// without a matching dashboard property.
    /// </summary>
    [Fact]
    public void Compute_CountsUnrecognisedSubClass_InTotal()
    {
        var result = FinanceBreakdownPolicy.Compute([
            ("LaborIncome", 50m),
            ("SomeNewSubClassNotYetInAnyPolicy", 50m)
        ]);

        Assert.Equal(100m, result.Total);
        Assert.Equal(50.0, result.PercentageBySubClass["LaborIncome"]);
        Assert.Equal(50.0, result.PercentageBySubClass["SomeNewSubClassNotYetInAnyPolicy"]);
    }
}

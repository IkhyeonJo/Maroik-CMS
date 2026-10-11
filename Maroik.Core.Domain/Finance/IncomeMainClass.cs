// ReSharper disable UnusedMember.Global
namespace Maroik.Core.Domain.Finance;

/// <summary>
/// Income main classes (<see cref="Income.MainClass"/>, <see cref="FixedIncome.MainClass"/>); the taxonomy is <see cref="IncomeClassPolicy.Taxonomy"/>. The member names are the values stored in the database and the resx keys of their labels, so renaming a member
/// changes stored data; the members are declared in display order.
/// </summary>
public enum IncomeMainClass
{
    /// <summary>Income that recurs on a schedule.</summary>
    RegularIncome,

    /// <summary>One-off or occasional income.</summary>
    IrregularIncome,
}

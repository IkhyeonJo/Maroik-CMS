// ReSharper disable UnusedMember.Global
namespace Maroik.Core.Domain.Finances;

/// <summary>
/// Expenditure main classes (<see cref="Expenditure.MainClass"/>, <see cref="FixedExpenditure.MainClass"/>); the taxonomy is <see cref="ExpenditureClassPolicy.Taxonomy"/>. The member names are the values stored in the database and the resx keys of their labels, so renaming a member
/// changes stored data; the members are declared in display order.
/// </summary>
public enum ExpenditureMainClass
{
    /// <summary>Money moved into savings or investments.</summary>
    RegularSavings,

    /// <summary>Taxes, insurance, pensions, debt and transfers.</summary>
    NonConsumerSpending,

    /// <summary>Everyday consumption.</summary>
    ConsumerSpending,
}

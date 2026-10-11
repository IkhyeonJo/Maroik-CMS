// ReSharper disable UnusedMember.Global
namespace Maroik.Core.Domain.Finance;

/// <summary>
/// Income sub-classes; which ones a main class allows is <see cref="IncomeClassPolicy.Taxonomy"/>. The member names are the values stored in the database and the resx keys of their labels, so renaming a member
/// changes stored data; the members are declared in display order.
/// </summary>
public enum IncomeSubClass
{
    /// <summary>Wages and salary.</summary>
    LaborIncome,

    /// <summary>Income from running a business.</summary>
    BusinessIncome,

    /// <summary>Pension payments.</summary>
    PensionIncome,

    /// <summary>Interest and dividends.</summary>
    FinancialIncome,

    /// <summary>Rent received.</summary>
    RentalIncome,

    /// <summary>Anything else.</summary>
    OtherIncome,
}

// ReSharper disable UnusedMember.Global
namespace Maroik.Core.Domain.Finance;

/// <summary>
/// Expenditure sub-classes; which ones a main class allows is <see cref="ExpenditureClassPolicy.Taxonomy"/>. The member names are the values stored in the database and the resx keys of their labels, so renaming a member
/// changes stored data; the members are declared in display order.
/// </summary>
public enum ExpenditureSubClass
{
    /// <summary>A deposit into a savings asset.</summary>
    Deposit,

    /// <summary>An investment into an investment asset.</summary>
    Investment,

    /// <summary>Public pension contributions.</summary>
    PublicPension,

    /// <summary>Repayment of a debt.</summary>
    DebtRepayment,

    /// <summary>Taxes.</summary>
    Tax,

    /// <summary>Social insurance premiums.</summary>
    SocialInsurance,

    /// <summary>Money given to another household.</summary>
    InterHouseholdTransferExpenses,

    /// <summary>Donations to non-profit organizations.</summary>
    NonProfitOrganizationTransfer,

    /// <summary>Food and eating out.</summary>
    MealOrEatOutExpenses,

    /// <summary>Housing and household supplies.</summary>
    HousingOrSuppliesCost,

    /// <summary>Education.</summary>
    EducationExpenses,

    /// <summary>Medical care.</summary>
    MedicalExpenses,

    /// <summary>Transportation.</summary>
    TransportationCost,

    /// <summary>Phone and internet.</summary>
    CommunicationCost,

    /// <summary>Leisure and culture.</summary>
    LeisureOrCulture,

    /// <summary>Clothing and shoes.</summary>
    ClothingOrShoes,

    /// <summary>Pocket money.</summary>
    PinMoney,

    /// <summary>Protection-type insurance premiums.</summary>
    ProtectionTypeInsurance,

    /// <summary>Anything else.</summary>
    OtherExpenses,

    /// <summary>Spending whose purpose is not known.</summary>
    UnknownExpenditure,
}

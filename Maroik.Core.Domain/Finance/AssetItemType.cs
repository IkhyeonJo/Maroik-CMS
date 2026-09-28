// ReSharper disable UnusedMember.Global
namespace Maroik.Core.Domain.Finance;

/// <summary>Recognised asset category values for the <see cref="Asset"/> aggregate.</summary>
public enum AssetItemType
{
    /// <summary>Freely accessible deposit/withdrawal account (e.g. a standard bank current account).</summary>
    FreeDepositAndWithdrawal,

    /// <summary>Trust asset managed by a financial institution on the owner's behalf.</summary>
    TrustAsset,

    /// <summary>Physical cash held by the account owner.</summary>
    CashAsset,

    /// <summary>Fixed-term savings account or deposit.</summary>
    SavingsAsset,

    /// <summary>Investment asset such as stocks, bonds, or mutual funds.</summary>
    InvestmentAsset,

    /// <summary>Real estate property (land or buildings).</summary>
    RealEstate,

    /// <summary>Movable property (e.g. vehicles, equipment).</summary>
    Movables,

    /// <summary>Other physical asset not covered by the listed categories.</summary>
    OtherPhysicalAsset,

    /// <summary>Insurance policy with a cash-surrender or savings component.</summary>
    InsuranceAsset
}

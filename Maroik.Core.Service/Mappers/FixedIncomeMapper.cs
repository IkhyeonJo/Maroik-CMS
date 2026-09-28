using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Finance;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps <see cref="FixedIncome"/> domain objects to <see cref="FixedIncomeResponse"/> DTOs.</summary>
internal static class FixedIncomeMapper
{
    /// <summary>Converts a <see cref="FixedIncome"/> domain object to its corresponding <see cref="FixedIncomeResponse"/> DTO.</summary>
    internal static FixedIncomeResponse ToResponse(FixedIncome fi) => new()
    {
        Id = fi.Id,
        AccountEmail = fi.AccountEmail.Value,
        MainClass = fi.MainClass,
        SubClass = fi.SubClass,
        Content = fi.Content,
        Amount = fi.Amount.Amount,
        MonetaryUnit = fi.Amount.Currency,
        DepositMyAssetProductName = fi.DepositMyAssetProductName,
        DepositMonth = fi.DepositMonth,
        DepositDay = fi.DepositDay,
        MaturityDate = fi.MaturityDate,
        Created = fi.Created,
        Updated = fi.Updated,
        Note = fi.Note,
        Unpunctuality = fi.Unpunctuality
    };
}

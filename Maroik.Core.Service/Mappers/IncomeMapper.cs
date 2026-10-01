using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Finance;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps <see cref="Income"/> domain objects to <see cref="IncomeResponse"/> DTOs.</summary>
internal static class IncomeMapper
{
    /// <summary>Converts an <see cref="Income"/> domain object to its corresponding <see cref="IncomeResponse"/> DTO.</summary>
    internal static IncomeResponse ToResponse(Income income) => new()
    {
        Id = income.Id,
        AccountEmail = income.AccountEmail.Value,
        MainClass = income.MainClass,
        SubClass = income.SubClass,
        Content = income.Content,
        Amount = income.Amount.Amount,
        MonetaryUnit = income.Amount.Currency.Value,
        DepositMyAssetProductName = income.DepositMyAssetProductName,
        Created = income.Created,
        Updated = income.Updated,
        Note = income.Note
    };
}

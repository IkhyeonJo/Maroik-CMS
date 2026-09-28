using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Finance;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps <see cref="Expenditure"/> domain objects to <see cref="ExpenditureResponse"/> DTOs.</summary>
internal static class ExpenditureMapper
{
    /// <summary>Converts an <see cref="Expenditure"/> domain object to its corresponding <see cref="ExpenditureResponse"/> DTO.</summary>
    internal static ExpenditureResponse ToResponse(Expenditure expenditure) => new()
    {
        Id = expenditure.Id,
        AccountEmail = expenditure.AccountEmail.Value,
        MainClass = expenditure.MainClass,
        SubClass = expenditure.SubClass,
        Content = expenditure.Content,
        Amount = expenditure.Amount.Amount,
        MonetaryUnit = expenditure.Amount.Currency,
        PaymentMethod = expenditure.PaymentMethod,
        MyDepositAsset = expenditure.MyDepositAsset,
        Created = expenditure.Created,
        Updated = expenditure.Updated,
        Note = expenditure.Note
    };
}

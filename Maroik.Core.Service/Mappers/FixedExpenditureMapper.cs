using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Finance;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps <see cref="FixedExpenditure"/> domain objects to <see cref="FixedExpenditureResponse"/> DTOs.</summary>
internal static class FixedExpenditureMapper
{
    /// <summary>Converts a <see cref="FixedExpenditure"/> domain object to its corresponding <see cref="FixedExpenditureResponse"/> DTO.</summary>
    internal static FixedExpenditureResponse ToResponse(FixedExpenditure fe) => new()
    {
        Id = fe.Id,
        AccountEmail = fe.AccountEmail.Value,
        MainClass = fe.MainClass,
        SubClass = fe.SubClass,
        Content = fe.Content,
        Amount = fe.Amount.Amount,
        MonetaryUnit = fe.Amount.Currency,
        PaymentMethod = fe.PaymentMethod,
        MyDepositAsset = fe.MyDepositAsset,
        DepositMonth = fe.DepositMonth,
        DepositDay = fe.DepositDay,
        MaturityDate = fe.MaturityDate,
        Created = fe.Created,
        Updated = fe.Updated,
        Note = fe.Note,
        Unpunctuality = fe.Unpunctuality
    };
}

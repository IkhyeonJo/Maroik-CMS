using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Finance;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps <see cref="Asset"/> domain objects to <see cref="AssetResponse"/> DTOs.</summary>
internal static class AssetMapper
{
    /// <summary>Converts an <see cref="Asset"/> domain object to its corresponding <see cref="AssetResponse"/> DTO.</summary>
    internal static AssetResponse ToResponse(Asset asset) => new()
    {
        ProductName = asset.ProductName,
        AccountEmail = asset.AccountEmail.Value,
        Item = asset.Item,
        Amount = asset.Balance.Amount,
        MonetaryUnit = asset.Balance.Currency,
        Created = asset.Created,
        Updated = asset.Updated,
        Note = asset.Note,
        Deleted = asset.Deleted
    };
}

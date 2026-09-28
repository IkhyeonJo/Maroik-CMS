using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="AssetMapper"/>.</summary>
public class AssetMapperTests
{
    /// <summary>To response maps all fields.</summary>
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        var created = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        var updated = new DateTime(2026, 1, 2, 2, 0, 0, DateTimeKind.Utc);

        Asset asset = Asset.Reconstitute(
            productName: "MyBank",
            accountEmail: "bob@example.com",
            item: "Deposit",
            amount: 12345.67m,
            monetaryUnit: "KRW",
            note: "primary account",
            deleted: true,
            created: created,
            updated: updated);

        AssetResponse response = AssetMapper.ToResponse(asset);

        Assert.Equal("MyBank", response.ProductName);
        Assert.Equal("bob@example.com", response.AccountEmail);
        Assert.Equal("Deposit", response.Item);
        Assert.Equal(12345.67m, response.Amount);
        Assert.Equal("KRW", response.MonetaryUnit);
        Assert.Equal(created, response.Created);
        Assert.Equal(updated, response.Updated);
        Assert.Equal("primary account", response.Note);
        Assert.True(response.Deleted);
    }
}

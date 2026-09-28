using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="FixedIncomeMapper"/>.</summary>
public class FixedIncomeMapperTests
{
    /// <summary>To response maps all fields.</summary>
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        var maturity = new DateTime(2028, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var created = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        var updated = new DateTime(2026, 1, 2, 2, 0, 0, DateTimeKind.Utc);

        FixedIncome fi = FixedIncome.Reconstitute(
            id: 50,
            accountEmail: "frank@example.com",
            mainClass: "RegularIncome",
            subClass: "LaborIncome",
            content: "Monthly salary",
            amount: 3000000m,
            monetaryUnit: "KRW",
            depositMyAssetProductName: "MyBank",
            depositMonth: 0,
            depositDay: 25,
            maturityDate: maturity,
            note: "primary job",
            unpunctuality: false,
            created: created,
            updated: updated);

        FixedIncomeResponse response = FixedIncomeMapper.ToResponse(fi);

        Assert.Equal(50, response.Id);
        Assert.Equal("frank@example.com", response.AccountEmail);
        Assert.Equal("RegularIncome", response.MainClass);
        Assert.Equal("LaborIncome", response.SubClass);
        Assert.Equal("Monthly salary", response.Content);
        Assert.Equal(3000000m, response.Amount);
        Assert.Equal("KRW", response.MonetaryUnit);
        Assert.Equal("MyBank", response.DepositMyAssetProductName);
        Assert.Equal((short)0, response.DepositMonth);
        Assert.Equal((short)25, response.DepositDay);
        Assert.Equal(maturity, response.MaturityDate);
        Assert.Equal(created, response.Created);
        Assert.Equal(updated, response.Updated);
        Assert.Equal("primary job", response.Note);
        Assert.False(response.Unpunctuality);
    }
}

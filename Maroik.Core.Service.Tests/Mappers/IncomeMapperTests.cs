using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="IncomeMapper"/>.</summary>
public class IncomeMapperTests
{
    /// <summary>To response maps all fields.</summary>
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        var created = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        var updated = new DateTime(2026, 1, 2, 2, 0, 0, DateTimeKind.Utc);

        Income income = Income.Reconstitute(
            id: 60,
            accountEmail: "grace@example.com",
            mainClass: "IrregularIncome",
            subClass: "FinancialIncome",
            content: "Stock dividend",
            amount: 50000m,
            monetaryUnit: "KRW",
            depositMyAssetProductName: "MyBrokerage",
            note: "quarterly",
            created: created,
            updated: updated);

        IncomeResponse response = IncomeMapper.ToResponse(income);

        Assert.Equal(60, response.Id);
        Assert.Equal("grace@example.com", response.AccountEmail);
        Assert.Equal("IrregularIncome", response.MainClass);
        Assert.Equal("FinancialIncome", response.SubClass);
        Assert.Equal("Stock dividend", response.Content);
        Assert.Equal(50000m, response.Amount);
        Assert.Equal("KRW", response.MonetaryUnit);
        Assert.Equal("MyBrokerage", response.DepositMyAssetProductName);
        Assert.Equal(created, response.Created);
        Assert.Equal(updated, response.Updated);
        Assert.Equal("quarterly", response.Note);
    }
}

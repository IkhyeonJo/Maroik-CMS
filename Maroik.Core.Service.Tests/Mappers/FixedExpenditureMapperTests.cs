using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="FixedExpenditureMapper"/>.</summary>
public class FixedExpenditureMapperTests
{
    /// <summary>To response maps all fields.</summary>
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        var maturity = new DateTime(2027, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var created = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        var updated = new DateTime(2026, 1, 2, 2, 0, 0, DateTimeKind.Utc);

        FixedExpenditure fe = FixedExpenditure.Reconstitute(
            id: 40,
            accountEmail: "erin@example.com",
            mainClass: "NonConsumerSpending",
            subClass: "Tax",
            content: "Property tax",
            amount: 250000m,
            monetaryUnit: "KRW",
            paymentMethod: "MyCard",
            myDepositAsset: "MyBank",
            depositMonth: 3,
            depositDay: 15,
            maturityDate: maturity,
            note: "annual",
            unpunctuality: true,
            created: created,
            updated: updated);

        FixedExpenditureResponse response = FixedExpenditureMapper.ToResponse(fe);

        Assert.Equal(40, response.Id);
        Assert.Equal("erin@example.com", response.AccountEmail);
        Assert.Equal("NonConsumerSpending", response.MainClass);
        Assert.Equal("Tax", response.SubClass);
        Assert.Equal("Property tax", response.Content);
        Assert.Equal(250000m, response.Amount);
        Assert.Equal("KRW", response.MonetaryUnit);
        Assert.Equal("MyCard", response.PaymentMethod);
        Assert.Equal("MyBank", response.MyDepositAsset);
        Assert.Equal((short)3, response.DepositMonth);
        Assert.Equal((short)15, response.DepositDay);
        Assert.Equal(maturity, response.MaturityDate);
        Assert.Equal(created, response.Created);
        Assert.Equal(updated, response.Updated);
        Assert.Equal("annual", response.Note);
        Assert.True(response.Unpunctuality);
    }
}

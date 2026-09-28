using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="ExpenditureMapper"/>.</summary>
public class ExpenditureMapperTests
{
    /// <summary>To response maps all fields.</summary>
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        var created = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        var updated = new DateTime(2026, 1, 2, 2, 0, 0, DateTimeKind.Utc);

        Expenditure expenditure = Expenditure.Reconstitute(
            id: 30,
            accountEmail: "dan@example.com",
            mainClass: "ConsumerSpending",
            subClass: "MealOrEatOutExpenses",
            content: "Lunch",
            amount: 12000m,
            monetaryUnit: "KRW",
            paymentMethod: "MyCard",
            myDepositAsset: "MyBank",
            note: "with team",
            created: created,
            updated: updated);

        ExpenditureResponse response = ExpenditureMapper.ToResponse(expenditure);

        Assert.Equal(30, response.Id);
        Assert.Equal("dan@example.com", response.AccountEmail);
        Assert.Equal("ConsumerSpending", response.MainClass);
        Assert.Equal("MealOrEatOutExpenses", response.SubClass);
        Assert.Equal("Lunch", response.Content);
        Assert.Equal(12000m, response.Amount);
        Assert.Equal("KRW", response.MonetaryUnit);
        Assert.Equal("MyCard", response.PaymentMethod);
        Assert.Equal("MyBank", response.MyDepositAsset);
        Assert.Equal(created, response.Created);
        Assert.Equal(updated, response.Updated);
        Assert.Equal("with team", response.Note);
    }
}

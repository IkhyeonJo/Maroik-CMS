using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Menu;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="CategoryMapper"/>.</summary>
public class CategoryMapperTests
{
    /// <summary>To response maps all fields.</summary>
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        Category category = Category.Reconstitute(
            id: 1,
            name: "Dashboard",
            displayName: "Dashboard",
            iconPath: "/icons/dashboard.svg",
            controller: "Home",
            action: "Index",
            role: "User",
            order: 5);

        CategoryResponse response = CategoryMapper.ToResponse(category);

        Assert.Equal(1, response.Id);
        Assert.Equal("Dashboard", response.Name);
        Assert.Equal("Dashboard", response.DisplayName);
        Assert.Equal("/icons/dashboard.svg", response.IconPath);
        Assert.Equal("Home", response.Controller);
        Assert.Equal("Index", response.Action);
        Assert.Equal("User", response.Role);
        Assert.Equal(5, response.Order);
    }
}

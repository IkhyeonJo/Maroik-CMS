using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Menu;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="SubCategoryMapper"/>.</summary>
public class SubCategoryMapperTests
{
    /// <summary>To response maps all fields.</summary>
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        SubCategory subCategory = SubCategory.Reconstitute(
            id: 2,
            categoryId: 1,
            name: "FreeForum",
            displayName: "Free Forum",
            iconPath: "/icons/forum.svg",
            action: "Index",
            role: "User",
            order: 3);

        SubCategoryResponse response = SubCategoryMapper.ToResponse(subCategory);

        Assert.Equal(2, response.Id);
        Assert.Equal(1, response.CategoryId);
        Assert.Equal("FreeForum", response.Name);
        Assert.Equal("Free Forum", response.DisplayName);
        Assert.Equal("/icons/forum.svg", response.IconPath);
        Assert.Equal("Index", response.Action);
        Assert.Equal("User", response.Role);
        Assert.Equal(3, response.Order);
    }
}

using Maroik.Core.Domain.Board;
using Maroik.Core.Domain.Primitives;
namespace Maroik.Core.Domain.Tests.Board;

/// <summary>
/// Unit tests for <see cref="BoardTypes"/>, pinning its <see cref="StringTaxonomy"/>-backed
/// <c>All</c>/<c>IsKnown</c> surface after that refactor.
/// </summary>
public class BoardTypesTests
{
    /// <summary>All contains exactly the two known board types.</summary>
    [Fact]
    public void All_ContainsExactlyKnownTypes()
    {
        Assert.Equal(new HashSet<string> { BoardTypes.FreeForum, BoardTypes.PrivateNote }, BoardTypes.All);
    }

    /// <summary>IsKnown returns true for each known board type.</summary>
    [Theory]
    [InlineData(BoardTypes.FreeForum)]
    [InlineData(BoardTypes.PrivateNote)]
    public void IsKnown_KnownType_ReturnsTrue(string type) => Assert.True(BoardTypes.IsKnown(type));

    /// <summary>IsKnown returns false for an unrecognized type and for null.</summary>
    [Theory]
    [InlineData("NotABoardType")]
    [InlineData(null)]
    public void IsKnown_UnknownOrNull_ReturnsFalse(string? type) => Assert.False(BoardTypes.IsKnown(type));
}

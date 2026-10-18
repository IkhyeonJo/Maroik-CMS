using Maroik.Core.Domain.Boards;
namespace Maroik.Core.Domain.Tests.Boards;

/// <summary>Unit tests for <see cref="BoardComment.NextOrder"/>.</summary>
public class BoardCommentOrderTests
{
    /// <summary>An active comment at <paramref name="order"/>.</summary>
    private static BoardComment CommentAt(long order) =>
        BoardComment.Reconstitute(order + 1, 1, order, null, "Bob", "Hi", DateTime.UnixEpoch, false);

    /// <summary>The first comment on a post gets order 0.</summary>
    [Fact]
    public void NextOrder_IsZero_ForTheFirstComment()
        => Assert.Equal(0, BoardComment.NextOrder([]));

    /// <summary>The next comment continues one past the highest order, even across a gap a soft-deleted comment left.</summary>
    [Fact]
    public void NextOrder_ContinuesPastTheHighestOrder()
        => Assert.Equal(6, BoardComment.NextOrder([CommentAt(5), CommentAt(0), CommentAt(2)]));
}

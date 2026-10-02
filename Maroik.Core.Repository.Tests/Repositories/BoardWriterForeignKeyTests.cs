using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Board;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrmAccount = Maroik.Core.PostgreSQL.Models.Account;
using OrmBoard = Maroik.Core.PostgreSQL.Models.Board;
using OrmBoardComment = Maroik.Core.PostgreSQL.Models.BoardComment;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// <c>Board.Writer</c> and <c>BoardComment.Writer</c> are the author's <c>Account.Nickname</c>, held by the foreign keys
/// <c>Board_fk_0</c> / <c>BoardComment_fk_1</c> (<c>ON UPDATE CASCADE</c>) against the real PostgreSQL schema: a nickname change
/// (the replaced unconfirmed registration, persisted through <see cref="AccountRepository.UpdateEntityAsync"/>) carries the
/// author's posts and comments along, so an account that later registers the old nickname does not become their writer, and a
/// writer no account holds cannot be stored at all.
/// </summary>
public sealed class BoardWriterForeignKeyTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    /// <summary>Adds an unconfirmed account (<paramref name="email"/>, <paramref name="nickname"/>) whose registration can still be replaced.</summary>
    private async Task AddUnconfirmedAccountAsync(string email, string nickname)
    {
        Context.Accounts.Add(new OrmAccount
        {
            Email = email, Nickname = nickname, HashedPassword = "$2a$13$placeholder",
            AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg", Role = Role.User, TimeZoneIanaId = "UTC",
            Deleted = false, Locked = false, EmailConfirmed = false, AgreedServiceTerms = true, LoginAttempt = 0,
            RegistrationToken = "token", SecurityStamp = "stamp", Created = DateTime.UtcNow, Updated = DateTime.UtcNow,
        });
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    /// <summary>Adds a free-forum post by <paramref name="writer"/> with one comment by the same writer; returns the post's id.</summary>
    private async Task<long> AddPostWithCommentAsync(string writer)
    {
        var board = new OrmBoard
        {
            Type = BoardTypes.FreeForum, Title = Unique("post"), Content = "c", Writer = writer,
            Created = DateTime.UtcNow, Updated = DateTime.UtcNow, View = 0, Deleted = false, Locked = false, Noticed = false,
        };
        Context.Boards.Add(board);
        await Context.SaveChangesAsync();
        Context.BoardComments.Add(new OrmBoardComment
        {
            BoardId = board.Id, Order = 0, AvatarImagePath = "/upload/default.jpg", Writer = writer, Content = "hi", Created = DateTime.UtcNow, Deleted = false,
        });
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return board.Id;
    }

    /// <summary>Replaces the unconfirmed registration of <paramref name="email"/> with <paramref name="newNickname"/> the way AccountService does.</summary>
    private async Task RenameThroughTheRegistrationReplacementAsync(string email, string newNickname)
    {
        var repository = new AccountRepository(Context);
        Account account = (await repository.FindByEmailAsync(email, TestContext.Current.CancellationToken))!;
        Assert.False(account.ReplaceUnconfirmedRegistration("$2a$13$other", newNickname, "UTC", "token2", true, DateTime.UtcNow).IsError);
        await repository.UpdateEntityAsync(account, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();
    }

    /// <summary>The writer of post <paramref name="boardId"/> and of its comment, read with a fresh context.</summary>
    private async Task<(string Post, string Comment)> WritersOfAsync(long boardId)
    {
        await using var db = NewDbContext();
        string post = (await db.Boards.AsNoTracking().SingleAsync(b => b.Id == boardId, TestContext.Current.CancellationToken)).Writer;
        string comment = (await db.BoardComments.AsNoTracking().SingleAsync(c => c.BoardId == boardId, TestContext.Current.CancellationToken)).Writer;
        return (post, comment);
    }

    /// <summary>A nickname change carries the account's posts and comments to the new nickname.</summary>
    [Fact]
    public async Task ANicknameChange_MovesTheAccountsPostsAndComments_ToTheNewNickname()
    {
        string email = UniqueEmail("renamed");
        string oldNickname = "old" + Token;
        string newNickname = "new" + Token;
        await AddUnconfirmedAccountAsync(email, oldNickname);
        long boardId = await AddPostWithCommentAsync(oldNickname);

        await RenameThroughTheRegistrationReplacementAsync(email, newNickname);

        Assert.Equal((newNickname, newNickname), await WritersOfAsync(boardId));
    }

    /// <summary>An account that registers the released nickname afterwards is not the writer of the earlier posts and comments.</summary>
    [Fact]
    public async Task AnAccountTakingTheOldNickname_DoesNotBecomeTheWriterOfTheEarlierPosts()
    {
        string email = UniqueEmail("renamed");
        string oldNickname = "old" + Token;
        await AddUnconfirmedAccountAsync(email, oldNickname);
        long boardId = await AddPostWithCommentAsync(oldNickname);
        await RenameThroughTheRegistrationReplacementAsync(email, "new" + Token);

        string newcomer = UniqueEmail("newcomer");
        await AddUnconfirmedAccountAsync(newcomer, oldNickname);

        (string post, string comment) = await WritersOfAsync(boardId);
        Assert.NotEqual(oldNickname, post);
        Assert.NotEqual(oldNickname, comment);
        await using var db = NewDbContext();
        Assert.False(await db.Boards.AnyAsync(b => b.Id == boardId && b.Writer == oldNickname, TestContext.Current.CancellationToken));
    }

    /// <summary>A post whose writer is no account's nickname is refused by <c>Board_fk_0</c>.</summary>
    [Fact]
    public async Task APost_ByAWriterNoAccountHolds_IsRefusedByTheForeignKey()
    {
        Context.Boards.Add(new OrmBoard
        {
            Type = BoardTypes.FreeForum, Title = Unique("orphan"), Content = "c", Writer = "nobody" + Token,
            Created = DateTime.UtcNow, Updated = DateTime.UtcNow, View = 0, Deleted = false, Locked = false, Noticed = false,
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => Context.SaveChangesAsync(TestContext.Current.CancellationToken));

        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, pg.SqlState);
        Assert.Equal("Board_fk_0", pg.ConstraintName);
    }

    /// <summary>A comment whose writer is no account's nickname is refused by <c>BoardComment_fk_1</c>.</summary>
    [Fact]
    public async Task AComment_ByAWriterNoAccountHolds_IsRefusedByTheForeignKey()
    {
        string author = "author" + Token;
        await EnsureNicknamesAsync(author);
        long boardId = await AddPostWithCommentAsync(author);
        Context.BoardComments.Add(new OrmBoardComment
        {
            BoardId = boardId, Order = 1, AvatarImagePath = "/upload/default.jpg", Writer = "nobody" + Token, Content = "hi", Created = DateTime.UtcNow, Deleted = false,
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => Context.SaveChangesAsync(TestContext.Current.CancellationToken));

        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, pg.SqlState);
        Assert.Equal("BoardComment_fk_1", pg.ConstraintName);
    }
}

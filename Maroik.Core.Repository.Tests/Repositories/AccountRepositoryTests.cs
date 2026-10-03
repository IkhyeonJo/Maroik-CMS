using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OrmAccount = Maroik.Core.PostgreSQL.Models.Account;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="AccountRepository"/> against a real PostgreSQL database (Testcontainers)
/// preloaded with the <c>Init.sql</c> schema and seed data. Exercises real SQL
/// behavior the EF Core InMemory provider could not: the <c>Account_Nickname_unique</c> index,
/// the case-sensitive <c>=</c> lookup, the <c>Account_Role_check</c> constraint, column defaults
/// and <c>SELECT ... FOR UPDATE</c> row-lock semantics.
/// Each test inserts under keys unique to itself (<see cref="RepositoryTestBase.UniqueEmail"/> /
/// <see cref="RepositoryTestBase.Unique"/>) so it never collides with the seed or a sibling test.
/// </summary>
public sealed class AccountRepositoryTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    private AccountRepository Sut => new(Context);

    // -- Helpers --------------------------------------------------------------

    /// <summary>An unsaved, confirmed account row with a unique e-mail and nickname unless given.</summary>
    private OrmAccount NewAccount(
        string? email = null,
        string? nickname = null,
        bool deleted = false,
        bool locked = false,
        long loginAttempt = 0,
        string role = Role.User,
        string? registrationToken = null,
        string? resetPasswordToken = null) => new()
    {
        Email = email ?? UniqueEmail("a" + Guid.NewGuid().ToString("N")[..8]),
        Nickname = nickname ?? Unique("Nick-" + Guid.NewGuid().ToString("N")[..8]),
        HashedPassword = "$2a$13$placeholder",
        AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
        Role = role,
        TimeZoneIanaId = "UTC",
        DefaultMonetaryUnit = null,
        Deleted = deleted,
        Locked = locked,
        EmailConfirmed = true,
        AgreedServiceTerms = true,
        LoginAttempt = loginAttempt,
        RegistrationToken = registrationToken,
        ResetPasswordToken = resetPasswordToken,
        Created = DateTime.UtcNow,
        Updated = DateTime.UtcNow,
        SecurityStamp = "stamp"
    };

    /// <summary>A confirmed, unlocked domain account as if loaded from the database.</summary>
    private static Account NewDomainAccount(string email, string nickname, string role = Role.User, string timeZone = "UTC") =>
        Account.Reconstitute(
            email: email, hashedPassword: "$2a$13$hash", nickname: nickname, avatarImagePath: null,
            role: role, timeZoneIanaId: timeZone, defaultMonetaryUnit: null, locked: false, loginAttempt: 0,
            emailConfirmed: true, agreedServiceTerms: true, registrationToken: null, resetPasswordToken: null,
            created: DateTime.UtcNow, updated: DateTime.UtcNow, message: null, deleted: false,
            securityStamp: "stamp", mustChangePassword: false);

    /// <summary>Inserts <paramref name="accounts"/>, saves, and clears the change tracker so later reads hit the database.</summary>
    private async Task SeedAsync(params OrmAccount[] accounts)
    {
        await Context.Accounts.AddRangeAsync(accounts);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    // -- GetAllAsync --------------------------------------------------------------

    /// <summary>Verifies that <c>GetAllAsync</c> returns every account row this test inserted.</summary>
    [Fact]
    public async Task GetAllAsync_ReturnsInsertedAccounts()
    {
        string a = UniqueEmail("alice");
        string b = UniqueEmail("bob");
        await SeedAsync(NewAccount(a, Unique("Alice")), NewAccount(b, Unique("Bob")));

        List<Account> result = await Sut.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count(x => x.Email.Value == a || x.Email.Value == b));
    }

    /// <summary>Verifies that <c>GetAllAsync</c> also returns the accounts already present in the seed data.</summary>
    [Fact]
    public async Task GetAllAsync_IncludesSeededAccounts()
    {
        List<Account> result = await Sut.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Contains(result, x => x.Email.Value == "admin@maroik.com" && x.Role == AccountRole.Admin);
        Assert.Contains(result, x => x.Email.Value == "demo@maroik.com" && x.Role == AccountRole.User);
    }

    /// <summary>Repository applies no implicit soft-delete filter: a seeded, deleted account is still returned.</summary>
    [Fact]
    public async Task GetAllAsync_ReturnsSoftDeletedAccounts_NotFiltered()
    {
        var deleted = NewAccount(UniqueEmail("gone"), Unique("Gone"), deleted: true);
        await SeedAsync(deleted);

        List<Account> result = await Sut.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Contains(result, x => x.Email.Value == deleted.Email && x.Deleted);
    }

    // -- FindByEmailAsync -------------------------------------------------------

    /// <summary>Verifies that <c>FindByEmailAsync</c> returns the account when found.</summary>
    [Fact]
    public async Task FindByEmailAsync_ReturnsAccount_WhenFound()
    {
        string email = UniqueEmail("alice");
        await SeedAsync(NewAccount(email, Unique("Alice")));

        Account? result = await Sut.FindByEmailAsync(email, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(email, result.Email.Value);
    }

    /// <summary>Verifies that <c>FindByEmailAsync</c> returns null when not found.</summary>
    [Fact]
    public async Task FindByEmailAsync_ReturnsNull_WhenNotFound()
    {
        Account? result = await Sut.FindByEmailAsync(UniqueEmail("ghost"), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>
    /// Email is always persisted lower-case (see Email.Create), but a lookup can arrive with a
    /// different case (e.g. a login form auto-capitalized by the browser). Against PostgreSQL's
    /// case-sensitive <c>=</c> it must still match, or a user who registered as "Foo@Bar.com"
    /// (stored "foo@bar.com") is locked out re-typing the same case at login.
    /// </summary>
    [Fact]
    public async Task FindByEmailAsync_IsCaseInsensitive()
    {
        string email = UniqueEmail("alice");
        await SeedAsync(NewAccount(email, Unique("Alice")));

        Account? result = await Sut.FindByEmailAsync(email.ToUpperInvariant(), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(email, result.Email.Value);
    }

    // -- FindByRegistrationTokenAsync / FindByResetPasswordTokenAsync ----------

    /// <summary>Verifies that <c>FindByRegistrationTokenAsync</c> returns the account when the token matches.</summary>
    [Fact]
    public async Task FindByRegistrationTokenAsync_ReturnsAccount_WhenTokenMatches()
    {
        string token = Unique("reg-token");
        await SeedAsync(NewAccount(registrationToken: token));

        Account? result = await Sut.FindByRegistrationTokenAsync(token, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
    }

    /// <summary>Verifies that <c>FindByRegistrationTokenAsync</c> returns null when no token matches.</summary>
    [Fact]
    public async Task FindByRegistrationTokenAsync_ReturnsNull_WhenTokenDoesNotMatch()
    {
        Account? result = await Sut.FindByRegistrationTokenAsync(Unique("nope"), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>Verifies that <c>FindByResetPasswordTokenAsync</c> returns the account when the token matches.</summary>
    [Fact]
    public async Task FindByResetPasswordTokenAsync_ReturnsAccount_WhenTokenMatches()
    {
        string token = Unique("reset-token");
        await SeedAsync(NewAccount(resetPasswordToken: token));

        Account? result = await Sut.FindByResetPasswordTokenAsync(token, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
    }

    /// <summary>Verifies that <c>FindByResetPasswordTokenAsync</c> returns null when the token is not found.</summary>
    [Fact]
    public async Task FindByResetPasswordTokenAsync_ReturnsNull_WhenTokenNotFound()
    {
        Account? result = await Sut.FindByResetPasswordTokenAsync(Unique("no-such"), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // -- FindByNicknamesAsync -------------------------------------------------

    /// <summary>Verifies that <c>FindByNicknamesAsync</c> returns exactly the matching accounts.</summary>
    [Fact]
    public async Task FindByNicknamesAsync_ReturnsMatchingAccounts()
    {
        string n1 = Unique("Alice");
        string n2 = Unique("Carol");
        await SeedAsync(
            NewAccount(nickname: n1),
            NewAccount(nickname: Unique("Bob")),
            NewAccount(nickname: n2));

        List<Account> result = await Sut.FindByNicknamesAsync([n1, n2], TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a.Nickname == n1);
        Assert.Contains(result, a => a.Nickname == n2);
    }

    /// <summary>Verifies that <c>FindByNicknamesAsync</c> returns empty when none match.</summary>
    [Fact]
    public async Task FindByNicknamesAsync_ReturnsEmpty_WhenNoneMatch()
    {
        List<Account> result = await Sut.FindByNicknamesAsync([Unique("Ghost")], TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    /// <summary>Verifies that <c>FindByNicknamesAsync</c> returns empty when the name list is empty.</summary>
    [Fact]
    public async Task FindByNicknamesAsync_ReturnsEmpty_WhenListIsEmpty()
    {
        List<Account> result = await Sut.FindByNicknamesAsync([], TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    // -- Email limit vs. column limit --------------------------------------------------

    /// <summary>
    /// The 255-character e-mail limit the domain enforces is exactly what the real <c>Account.Email</c>
    /// column (the primary key) holds: an address on the limit persists and round-trips.
    /// </summary>
    [Fact]
    public async Task CreateAsync_PersistsAnEmailOnTheColumnLimit()
    {
        string local = ("l" + Guid.NewGuid().ToString("N")).PadRight(255 - "@example.com".Length, 'x');
        string email = local + "@example.com";
        Assert.Equal(255, email.Length);
        var domainAccount = NewDomainAccount(email, Unique("LongMail"));

        await Sut.CreateAsync(domainAccount, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Account? loaded = await Sut.FindByEmailAsync(email, TestContext.Current.CancellationToken);
        Assert.NotNull(loaded);
        Assert.Equal(255, loaded.Email.Value.Length);
    }

    // -- NicknameExistsIgnoreCaseAsync ----------------------------------------

    /// <summary>
    /// Verifies that <c>NicknameExistsIgnoreCaseAsync</c> matches an existing nickname regardless of
    /// case — the <c>Account_Nickname_unique</c> constraint itself is case-sensitive, so it alone
    /// would accept "bob" next to "Bob".
    /// </summary>
    [Fact]
    public async Task NicknameExistsIgnoreCaseAsync_ReturnsTrue_ForDifferentCase()
    {
        string nickname = Unique("CaseNick");
        await SeedAsync(NewAccount(nickname: nickname));

        Assert.True(await Sut.NicknameExistsIgnoreCaseAsync(nickname, TestContext.Current.CancellationToken));
        Assert.True(await Sut.NicknameExistsIgnoreCaseAsync(nickname.ToLowerInvariant(), TestContext.Current.CancellationToken));
        Assert.True(await Sut.NicknameExistsIgnoreCaseAsync(nickname.ToUpperInvariant(), TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies that <c>NicknameExistsIgnoreCaseAsync</c> returns false for a nickname nobody uses.</summary>
    [Fact]
    public async Task NicknameExistsIgnoreCaseAsync_ReturnsFalse_WhenNoAccountUsesTheNickname()
    {
        Assert.False(await Sut.NicknameExistsIgnoreCaseAsync(Unique("Ghost"), TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies that <c>NicknameExistsIgnoreCaseAsync</c> is not a prefix / substring match.</summary>
    [Fact]
    public async Task NicknameExistsIgnoreCaseAsync_DoesNotMatchAPrefix()
    {
        string nickname = Unique("Prefix");
        await SeedAsync(NewAccount(nickname: nickname + "Longer"));

        Assert.False(await Sut.NicknameExistsIgnoreCaseAsync(nickname, TestContext.Current.CancellationToken));
    }

    // -- CreateAsync --------------------------------------------------------------

    /// <summary>Verifies that <c>CreateAsync</c> persists a new account row that is then retrievable.</summary>
    [Fact]
    public async Task CreateAsync_PersistsAccount_AndIsRetrievable()
    {
        string email = UniqueEmail("new");
        await Sut.CreateAsync(NewDomainAccount(email, Unique("NewUser"), timeZone: "Asia/Seoul"), TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Account? retrieved = await Sut.FindByEmailAsync(email, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal("Asia/Seoul", retrieved.TimeZone.Value);
    }

    /// <summary>PostgreSQL enforces the <c>Account</c> primary key — a duplicate e-mail throws (InMemory did not).</summary>
    [Fact]
    public async Task CreateAsync_Throws_WhenEmailAlreadyExists()
    {
        string email = UniqueEmail("dup");
        await SeedAsync(NewAccount(email, Unique("First")));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => Sut.CreateAsync(NewDomainAccount(email, Unique("Second")), TestContext.Current.CancellationToken));
    }

    /// <summary>PostgreSQL enforces <c>Account_Nickname_unique</c> — a duplicate nickname throws (InMemory did not).</summary>
    [Fact]
    public async Task CreateAsync_Throws_WhenNicknameAlreadyExists()
    {
        string nickname = Unique("Twin");
        await SeedAsync(NewAccount(nickname: nickname));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => Sut.CreateAsync(NewDomainAccount(UniqueEmail("other"), nickname), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// PostgreSQL enforces <c>Account_unique_index_0</c>: a nickname that differs only by case
    /// from an existing one is rejected by the database itself (the app-level check alone cannot stop
    /// two concurrent registrations), and the violation names that index so the service can report it
    /// as a nickname conflict.
    /// </summary>
    [Fact]
    public async Task CreateAsync_Throws_WhenNicknameDiffersOnlyByCase()
    {
        string nickname = Unique("CaseTwin");
        await SeedAsync(NewAccount(nickname: nickname));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(
            () => Sut.CreateAsync(NewDomainAccount(UniqueEmail("case"), nickname.ToUpperInvariant()), TestContext.Current.CancellationToken));

        var pg = Assert.IsType<Npgsql.PostgresException>(ex.InnerException);
        Assert.Equal("23505", pg.SqlState);
        Assert.Equal("Account_unique_index_0", pg.ConstraintName);
    }

    /// <summary>The exact-duplicate case is still reported against the original exact-match constraint.</summary>
    [Fact]
    public async Task CreateAsync_ExactDuplicateNickname_ViolatesTheExactMatchConstraint()
    {
        string nickname = Unique("ExactTwin");
        await SeedAsync(NewAccount(nickname: nickname));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(
            () => Sut.CreateAsync(NewDomainAccount(UniqueEmail("exact"), nickname), TestContext.Current.CancellationToken));

        var pg = Assert.IsType<Npgsql.PostgresException>(ex.InnerException);
        Assert.Contains(pg.ConstraintName, new[] { "Account_Nickname_unique", "Account_unique_index_0" });
    }

    // -- UpdateEntityAsync ---------------------------------------------------

    /// <summary>Verifies that <c>UpdateEntityAsync</c> persists changes to an existing account.</summary>
    [Fact]
    public async Task UpdateEntityAsync_UpdatesExistingAccount()
    {
        string email = UniqueEmail("alice");
        await SeedAsync(NewAccount(email, Unique("Alice")));

        Account updated = NewDomainAccount(email, Unique("Alice"), role: Role.Admin, timeZone: "Asia/Seoul");
        await Sut.UpdateEntityAsync(updated, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Account? result = await Sut.FindByEmailAsync(email, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(Role.Admin, result.Role.Value);
        Assert.Equal("Asia/Seoul", result.TimeZone.Value);
    }

    /// <summary>
    /// An account write that keeps the nickname (a login, a message, a lock ...) sends exactly one UPDATE: the separate
    /// nickname UPDATE (which the <c>Board_fk_0</c> / <c>BoardComment_fk_1</c> cascade needs) is only for an actual change.
    /// </summary>
    [Fact]
    public async Task UpdateEntityAsync_SendsOneUpdate_WhenTheNicknameIsUnchanged()
    {
        string email = UniqueEmail("steady");
        string nickname = Unique("Steady");
        await SeedAsync(NewAccount(email, nickname));

        var recorder = new CommandRecorder();
        var repository = new AccountRepository(NewDbContext(recorder));
        Account account = (await repository.FindByEmailAsync(email, TestContext.Current.CancellationToken))!;
        account.SetMessage("Hello", DateTime.UtcNow);
        recorder.Commands.Clear();

        await repository.UpdateEntityAsync(account, TestContext.Current.CancellationToken);

        Assert.Single(recorder.Commands, c => c.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase));
        Account? stored = await Sut.FindByEmailAsync(email, TestContext.Current.CancellationToken);
        Assert.Equal("Hello", stored!.Message);
        Assert.Equal(nickname, stored.Nickname);
    }

    /// <summary>Records the text of every SQL command a context sends.</summary>
    private sealed class CommandRecorder : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    /// <summary>Verifies that <c>UpdateEntityAsync</c> throws, rather than silently no-op'ing, when the row no longer exists.</summary>
    [Fact]
    public async Task UpdateEntityAsync_Throws_WhenAccountNoLongerExists()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Sut.UpdateEntityAsync(NewDomainAccount(UniqueEmail("ghost"), Unique("Ghost")), TestContext.Current.CancellationToken));
    }

    // -- Column-scoped updates ----------------------------------------------------

    /// <summary>Each column-scoped update writes only its own columns and leaves every other column intact.</summary>
    [Fact]
    public async Task ColumnScopedUpdates_TouchOnlyTheirOwnColumns()
    {
        string email = UniqueEmail("scoped");
        var seed = NewAccount(email, Unique("Scoped"));
        seed.AvatarImagePath = "/original-avatar.jpg";
        seed.TimeZoneIanaId = "UTC";
        seed.HashedPassword = "$2a$13$original";
        seed.SecurityStamp = "original-stamp";
        seed.MustChangePassword = false;
        seed.ResetPasswordToken = "pending-reset-token";
        seed.DefaultMonetaryUnit = "USD";
        seed.Message = "original-message";
        await SeedAsync(seed);

        var ct = TestContext.Current.CancellationToken;
        var t1 = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        int n1 = await Sut.UpdateAvatarPathAsync(email, "/new-avatar.jpg", t1, ct);
        int n2 = await Sut.UpdateTimeZoneAsync(email, "Asia/Seoul", t1, ct);
        int n3 = await Sut.UpdatePasswordAsync(email, "$2a$13$new", "new-stamp", mustChangePassword: true, resetPasswordToken: null, locked: false, loginAttempt: 0, message: "original-message", t1, ct);
        int n4 = await Sut.UpdateDefaultMonetaryUnitAsync(email, "KRW", ct);
        int n5 = await Sut.UpdateMessageAsync(email, "new-message", t1, ct);
        Context.ChangeTracker.Clear();

        Assert.Equal(1, n1);
        Assert.Equal(1, n2);
        Assert.Equal(1, n3);
        Assert.Equal(1, n4);
        Assert.Equal(1, n5);

        Account? result = await Sut.FindByEmailAsync(email, ct);
        Assert.NotNull(result);
        Assert.Equal("/new-avatar.jpg", result.AvatarImagePath);
        Assert.Equal("Asia/Seoul", result.TimeZone.Value);
        Assert.Equal("$2a$13$new", result.HashedPassword);
        Assert.Equal("new-stamp", result.SecurityStamp);
        Assert.True(result.MustChangePassword);
        // A password write discards the pending reset token, so a link mailed earlier is dead.
        Assert.Null(result.ResetPasswordToken);
        Assert.Equal("KRW", result.DefaultMonetaryUnit?.Value);
        Assert.Equal("new-message", result.Message);
        // Untouched by any of the above.
        Assert.True(result.EmailConfirmed);
        Assert.Equal(Role.User, result.Role.Value);
    }

    /// <summary>A self-service password change lifts a failed-login lock: the password write also stores the lock columns.</summary>
    [Fact]
    public async Task UpdatePasswordAsync_WritesTheLockColumns()
    {
        string email = UniqueEmail("pwunlock");
        var seed = NewAccount(email, Unique("PwUnlock"));
        seed.Locked = true;
        seed.LoginAttempt = 3;
        seed.Message = "This account is locked";
        await SeedAsync(seed);

        var ct = TestContext.Current.CancellationToken;
        int n = await Sut.UpdatePasswordAsync(email, "$2a$13$new", "new-stamp", mustChangePassword: false, resetPasswordToken: null,
            locked: false, loginAttempt: 0, message: null, DateTime.UtcNow, ct);
        Context.ChangeTracker.Clear();

        Assert.Equal(1, n);
        Account? result = await Sut.FindByEmailAsync(email, ct);
        Assert.NotNull(result);
        Assert.False(result.Locked);
        Assert.Equal(0, result.LoginAttempt);
        Assert.Null(result.Message);
    }

    /// <summary>A null avatar path persists as the shared default-avatar path, matching ToEntity.</summary>
    [Fact]
    public async Task UpdateAvatarPathAsync_NullPersistsAsDefaultAvatar()
    {
        string email = UniqueEmail("nullavatar");
        var seed = NewAccount(email, Unique("NullAvatar"));
        seed.AvatarImagePath = "/some-custom.jpg";
        await SeedAsync(seed);

        int n = await Sut.UpdateAvatarPathAsync(email, null, DateTime.UtcNow, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Assert.Equal(1, n);
        Account? result = await Sut.FindByEmailAsync(email, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(Account.DefaultAvatarImagePath, result.AvatarImagePath);
    }

    /// <summary>A column-scoped update against a non-existent account affects zero rows (no throw).</summary>
    [Fact]
    public async Task ColumnScopedUpdate_ReturnsZero_WhenAccountMissing()
    {
        int n = await Sut.UpdateMessageAsync(UniqueEmail("ghost"), "x", DateTime.UtcNow, TestContext.Current.CancellationToken);
        Assert.Equal(0, n);
    }

    // -- FindByEmailForUpdateAsync (raw SELECT ... FOR UPDATE) ----------------

    /// <summary>Verifies that the row-locking lookup returns the account when it exists.</summary>
    [Fact]
    public async Task FindByEmailForUpdateAsync_ReturnsAccount_WhenExists()
    {
        string email = UniqueEmail("alice");
        string nickname = Unique("Alice");
        await SeedAsync(NewAccount(email, nickname));

        await using var unitOfWork = new UnitOfWork(Context);
        await unitOfWork.BeginAsync(TestContext.Current.CancellationToken);
        Account? result = await Sut.FindByEmailForUpdateAsync(email, TestContext.Current.CancellationToken);
        await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(nickname, result.Nickname);
    }

    /// <summary>Verifies that the row-locking lookup returns null when the account is not found.</summary>
    [Fact]
    public async Task FindByEmailForUpdateAsync_ReturnsNull_WhenNotFound()
    {
        await using var unitOfWork = new UnitOfWork(Context);
        await unitOfWork.BeginAsync(TestContext.Current.CancellationToken);
        Account? result = await Sut.FindByEmailForUpdateAsync(UniqueEmail("missing"), TestContext.Current.CancellationToken);
        await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>The FOR UPDATE login lookup must also match a differently-cased address (see <see cref="FindByEmailAsync_IsCaseInsensitive"/>).</summary>
    [Fact]
    public async Task FindByEmailForUpdateAsync_IsCaseInsensitive()
    {
        string email = UniqueEmail("alice");
        await SeedAsync(NewAccount(email, Unique("Alice")));

        await using var unitOfWork = new UnitOfWork(Context);
        await unitOfWork.BeginAsync(TestContext.Current.CancellationToken);
        Account? result = await Sut.FindByEmailForUpdateAsync(email.ToUpperInvariant(), TestContext.Current.CancellationToken);
        await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
    }

    /// <summary>
    /// Concurrent failed-login sequences for the same account, each wrapped in its own transaction
    /// around <c>FindByEmailForUpdateAsync</c> + <c>UpdateEntityAsync</c> (mirroring
    /// <c>AccountService.LoginAsync</c>). The row lock serializes the transactions — each blocks
    /// until the prior commits, then re-reads the current counter — so all five increments survive
    /// instead of racing on a stale read.
    /// </summary>
    [Fact]
    public async Task FindByEmailForUpdateAsync_SerializesConcurrentIncrements_AccumulatingCorrectly()
    {
        string email = UniqueEmail("race");
        await SeedAsync(NewAccount(email, Unique("Race")));

        // Contexts created up front: RepositoryTestBase tracks them for disposal and the tracking
        // list is not safe to append from parallel tasks.
        ApplicationDbContext[] contexts = [.. Enumerable.Range(0, 5).Select(_ => NewDbContext())];

        await Task.WhenAll(contexts.Select(async context =>
        {
            var repo = new AccountRepository(context);
            await using var unitOfWork = new UnitOfWork(context);
            await unitOfWork.BeginAsync(TestContext.Current.CancellationToken);
            Account account = (await repo.FindByEmailForUpdateAsync(email, TestContext.Current.CancellationToken))!;
            account.RecordLoginFailure(maxAttempts: 100, DateTime.UtcNow);
            await repo.UpdateEntityAsync(account, TestContext.Current.CancellationToken);
            await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);
        }));

        Context.ChangeTracker.Clear();
        Account? updated = await Sut.FindByEmailAsync(email, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(5, updated.LoginAttempt);
    }

    // -- SearchAsync ----------------------------------------------------------------

    /// <summary>The admin grid search matches nickname/e-mail case-insensitively.</summary>
    [Fact]
    public async Task SearchAsync_MatchesNicknameAndEmailCaseInsensitively()
    {
        string nickname = Unique("Searchable");
        OrmAccount target = NewAccount(nickname: nickname);
        await SeedAsync(target, NewAccount());

        List<Account> byNickname = await Sut.SearchAsync(nickname.ToUpperInvariant(), TestContext.Current.CancellationToken);
        List<Account> byEmail = await Sut.SearchAsync(target.Email!.ToUpperInvariant(), TestContext.Current.CancellationToken);

        Assert.Equal(target.Email, Assert.Single(byNickname).Email.Value);
        Assert.Equal(target.Email, Assert.Single(byEmail).Email.Value);
    }

    /// <summary>
    /// The registration / reset tokens and the admin message are shown in the admin grid (and used to
    /// verify a token issue took effect), so the whole-row search must match them too.
    /// </summary>
    [Fact]
    public async Task SearchAsync_MatchesRegistrationTokenResetTokenAndMessage()
    {
        string regToken = "reg-" + Token;
        string resetToken = "reset-" + Token;
        string message = "note-" + Token;
        OrmAccount withReg = NewAccount(registrationToken: regToken);
        OrmAccount withReset = NewAccount(resetPasswordToken: resetToken);
        OrmAccount withMessage = NewAccount();
        withMessage.Message = message;
        await SeedAsync(withReg, withReset, withMessage, NewAccount());

        Assert.Equal(withReg.Email, Assert.Single(await Sut.SearchAsync(regToken, TestContext.Current.CancellationToken)).Email.Value);
        Assert.Equal(withReset.Email, Assert.Single(await Sut.SearchAsync(resetToken, TestContext.Current.CancellationToken)).Email.Value);
        Assert.Equal(withMessage.Email, Assert.Single(await Sut.SearchAsync(message, TestContext.Current.CancellationToken)).Email.Value);
    }

    /// <summary><c>%</c> and <c>_</c> in the search text are literals, not LIKE wildcards.</summary>
    [Fact]
    public async Task SearchAsync_TreatsLikeWildcardsLiterally()
    {
        OrmAccount percent = NewAccount(nickname: $"100%-{Token}");
        OrmAccount plain = NewAccount(nickname: $"1000-{Token}");
        OrmAccount underscore = NewAccount(nickname: $"a_b-{Token}");
        OrmAccount other = NewAccount(nickname: $"axb-{Token}");
        await SeedAsync(percent, plain, underscore, other);

        List<Account> byPercent = await Sut.SearchAsync($"0%-{Token}", TestContext.Current.CancellationToken);
        List<Account> byUnderscore = await Sut.SearchAsync($"a_b-{Token}", TestContext.Current.CancellationToken);

        Assert.Equal(percent.Email, Assert.Single(byPercent).Email.Value);
        Assert.Equal(underscore.Email, Assert.Single(byUnderscore).Email.Value);
    }

    /// <summary>No matching row → an empty list (the two-step lookup skips its second query).</summary>
    [Fact]
    public async Task SearchAsync_ReturnsEmpty_WhenNothingMatches()
    {
        Assert.Empty(await Sut.SearchAsync("no-such-" + Token, TestContext.Current.CancellationToken));
    }

    // -- UpdateAgreedServiceTermsAsync ------------------------------------------------

    /// <summary>Writes the flag and the timestamp for exactly the addressed account (e-mail is normalized to lower case).</summary>
    [Fact]
    public async Task UpdateAgreedServiceTermsAsync_UpdatesOnlyTheAddressedAccount()
    {
        OrmAccount target = NewAccount();
        target.AgreedServiceTerms = false;
        OrmAccount bystander = NewAccount();
        bystander.AgreedServiceTerms = false;
        await SeedAsync(target, bystander);
        DateTime updated = new(2031, 5, 6, 7, 8, 9, DateTimeKind.Utc);

        int rows = await Sut.UpdateAgreedServiceTermsAsync(target.Email!.ToUpperInvariant(), true, updated, TestContext.Current.CancellationToken);

        Assert.Equal(1, rows);
        OrmAccount reloadedTarget = await Context.Accounts.AsNoTracking().SingleAsync(a => a.Email == target.Email, TestContext.Current.CancellationToken);
        OrmAccount reloadedBystander = await Context.Accounts.AsNoTracking().SingleAsync(a => a.Email == bystander.Email, TestContext.Current.CancellationToken);
        Assert.True(reloadedTarget.AgreedServiceTerms);
        Assert.Equal(updated, reloadedTarget.Updated);
        Assert.False(reloadedBystander.AgreedServiceTerms);
    }
}

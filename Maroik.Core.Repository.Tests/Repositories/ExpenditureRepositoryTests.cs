using Maroik.Core.Domain.Finance;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using OrmExpenditure = Maroik.Core.PostgreSQL.Models.Expenditure;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="ExpenditureRepository"/> against a real PostgreSQL database (Testcontainers)
/// preloaded with the <c>Init.sql</c> schema and seed data. <c>Expenditure</c> carries
/// two composite FKs to <c>Asset</c>: <c>Expenditure_fk_0</c> on <c>(PaymentMethod, AccountEmail)</c>
/// and <c>Expenditure_fk_1</c> on <c>(MyDepositAsset, AccountEmail)</c> (NULL when the entry is not
/// a transfer). Includes <c>UpdateMyDepositAssetWithProductNameAsync</c>, which uses
/// <c>ExecuteUpdateAsync</c> — a translation the EF Core InMemory provider could not run.
/// </summary>
public sealed class ExpenditureRepositoryTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    private ExpenditureRepository Sut => new(Context);

    /// <summary>An unsaved expenditure row owned by <paramref name="email"/>.</summary>
    private static OrmExpenditure MakeExpenditure(
        string email, string content, string paymentMethod = "Wallet", string? myDepositAsset = "Wallet",
        decimal amount = 100m) => new()
    {
        AccountEmail = email,
        MainClass = "ConsumerSpending",
        SubClass = "MealOrEatOutExpenses",
        Content = content,
        Amount = amount,
        PaymentMethod = paymentMethod,
        MyDepositAsset = myDepositAsset,
        Note = "",
        Created = DateTime.UtcNow,
        Updated = DateTime.UtcNow
    };

    /// <summary>Ensures each owner account and every referenced asset exist, then inserts <paramref name="expenditures"/>, saves, and clears the change tracker.</summary>
    private async Task SeedAsync(params OrmExpenditure[] expenditures)
    {
        foreach (var group in expenditures.GroupBy(e => e.AccountEmail!))
            await EnsureAssetsAsync(group.Key, [.. group.SelectMany(e => new[] { e.PaymentMethod, e.MyDepositAsset })]);

        await Context.Expenditures.AddRangeAsync(expenditures);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    // -- GetByAccountEmailAsync -------------------------------------------------

    /// <summary>Verifies that <c>GetByAccountEmailAsync</c> returns only expenditures for the given e-mail.</summary>
    [Fact]
    public async Task GetByAccountEmailAsync_ReturnsOnlyExpendituresForGivenEmail()
    {
        string alice = UniqueEmail("alice");
        string bob = UniqueEmail("bob");
        await SeedAsync(
            MakeExpenditure(alice, "Lunch"),
            MakeExpenditure(alice, "Dinner"),
            MakeExpenditure(bob, "Coffee"));

        List<Expenditure> result = await Sut.GetByAccountEmailAsync(alice, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.Equal(alice, e.AccountEmail.Value));
    }

    /// <summary>Verifies that <c>GetByAccountEmailAsync</c> returns empty when the e-mail has no expenditures.</summary>
    [Fact]
    public async Task GetByAccountEmailAsync_ReturnsEmpty_WhenNoExpendituresForEmail()
    {
        List<Expenditure> result = await Sut.GetByAccountEmailAsync(UniqueEmail("nobody"), TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    /// <summary>
    /// Regression: a non-transfer expenditure (<c>MyDepositAsset</c> NULL) still resolves its
    /// currency from the <c>PaymentMethod</c> asset via <c>AssetNavigation</c> — the mapping used
    /// to join the wrong navigation and return an empty <c>MonetaryUnit</c> for the common case.
    /// </summary>
    [Fact]
    public async Task GetByAccountEmailAsync_ResolvesCurrency_FromPaymentMethodAsset_ForNonTransferExpenditure()
    {
        string alice = UniqueEmail("alice");
        await SeedAsync(MakeExpenditure(alice, "Lunch", paymentMethod: "Wallet", myDepositAsset: null));

        List<Expenditure> result = await Sut.GetByAccountEmailAsync(alice, TestContext.Current.CancellationToken);

        Assert.Equal("KRW", Assert.Single(result).Amount.Currency);
    }

    // -- FindByEmailAndIdAsync -------------------------------------------------

    /// <summary>Verifies that <c>FindByEmailAndIdAsync</c> returns the expenditure when owned by the e-mail.</summary>
    [Fact]
    public async Task FindByEmailAndIdAsync_ReturnsExpenditure_WhenOwnedByEmail()
    {
        string alice = UniqueEmail("alice");
        var orm = MakeExpenditure(alice, "Lunch", amount: 42m);
        await SeedAsync(orm);

        Expenditure? result = await Sut.FindByEmailAndIdAsync(alice, orm.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(42m, result.Amount.Amount);
    }

    /// <summary>Verifies that <c>FindByEmailAndIdAsync</c> returns null for another account's id.</summary>
    [Fact]
    public async Task FindByEmailAndIdAsync_ReturnsNull_WhenNotOwnedByEmail()
    {
        var orm = MakeExpenditure(UniqueEmail("bob"), "Coffee");
        await SeedAsync(orm);

        Expenditure? result = await Sut.FindByEmailAndIdAsync(UniqueEmail("alice"), orm.Id, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // -- FindByEmailAndIdForUpdateAsync (SELECT ... FOR UPDATE) --------------

    /// <summary>The row-locking lookup finds an existing expenditure and returns null for a missing id.</summary>
    [Fact]
    public async Task FindByEmailAndIdForUpdateAsync_FindsExpenditure_OrReturnsNull()
    {
        string alice = UniqueEmail("alice");
        var orm = MakeExpenditure(alice, "Lunch", amount: 42m);
        await SeedAsync(orm);

        Expenditure? found = await Sut.FindByEmailAndIdForUpdateAsync(alice, orm.Id, TestContext.Current.CancellationToken);
        Expenditure? missing = await Sut.FindByEmailAndIdForUpdateAsync(alice, long.MaxValue, TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.Equal(42m, found.Amount.Amount);
        Assert.Null(missing);
    }

    /// <summary>
    /// The FOR UPDATE lookup re-reads through the included path, so the returned domain object still
    /// carries the monetary unit from the linked payment <c>Asset</c> — a bare
    /// <c>SELECT * … FOR UPDATE</c> row on its own would leave it blank.
    /// </summary>
    [Fact]
    public async Task FindByEmailAndIdForUpdateAsync_CarriesMonetaryUnit_FromAssetInclude()
    {
        string alice = UniqueEmail("alice");
        var orm = MakeExpenditure(alice, "Lunch", amount: 20m);
        await SeedAsync(orm); // EnsureAssetsAsync seeds the asset with MonetaryUnit "KRW"

        Expenditure? found = await Sut.FindByEmailAndIdForUpdateAsync(alice, orm.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.Equal("KRW", found.Amount.Currency);
    }

    /// <summary>
    /// Reproduces the concurrent-edit race: two parallel transactions each read the same
    /// expenditure, overlap, then write back an incremented amount. Without FOR UPDATE the second
    /// write is based on a stale read and clobbers the first (final 300); with it the reads
    /// serialize and both increments survive (final 500).
    /// </summary>
    [Fact]
    public async Task FindByEmailAndIdForUpdateAsync_SerializesConcurrentRecordEdits()
    {
        string alice = UniqueEmail("alice");
        var orm = MakeExpenditure(alice, "RaceSpend", amount: 300m);
        await SeedAsync(orm);

        await Task.WhenAll(BumpAsync(), BumpAsync());

        Context.ChangeTracker.Clear();
        OrmExpenditure? result = await Context.Expenditures.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == orm.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(500m, result.Amount); // 300 + 100 + 100: no lost update
        return;

        // One contender: locks the row on its own connection, waits so the other contender queues behind the lock, then adds 100 and commits.
        async Task BumpAsync()
        {
            await using var context = NewDbContext();
            var repo = new ExpenditureRepository(context);
            await using var tx = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

            Expenditure? expenditure = await repo.FindByEmailAndIdForUpdateAsync(alice, orm.Id, TestContext.Current.CancellationToken);
            Assert.NotNull(expenditure);

            await Task.Delay(300, TestContext.Current.CancellationToken);

            expenditure.Update("ConsumerSpending", "MealOrEatOutExpenses", expenditure.Content,
                expenditure.Amount.Amount + 100m, expenditure.Amount.Currency,
                expenditure.PaymentMethod, expenditure.MyDepositAsset, expenditure.Note);
            await repo.UpdateEntityAsync(expenditure, TestContext.Current.CancellationToken);
            // The repository defers its flush while a transaction is open on the context; this test
            // drives the transaction directly, so it flushes the pending UPDATE itself before commit.
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            await tx.CommitAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Regression: the FOR UPDATE lookup re-reads through <c>FindByEmailAndIdAsync</c>-equivalent
    /// path, which must run <c>AsNoTracking()</c>. If this row is already tracked in the same
    /// <see cref="RepositoryTestBase.Context"/> from an earlier plain read, a tracking re-read would perform EF's
    /// identity resolution and hand back that pre-existing (stale) tracked instance instead of the
    /// row's just-locked, current committed values — silently defeating the FOR UPDATE lock.
    /// </summary>
    [Fact]
    public async Task FindByEmailAndIdForUpdateAsync_ReturnsFreshValues_WhenRowAlreadyTrackedInSameContext()
    {
        string alice = UniqueEmail("alice");
        var orm = MakeExpenditure(alice, "Stale", amount: 100m);
        await SeedAsync(orm);

        // Pre-track the row in this test's Context via a normal (tracking) read.
        Expenditure? preRead = await Sut.FindByEmailAndIdAsync(alice, orm.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(preRead);

        // A concurrent writer commits a change to the same row via a separate connection.
        await using var writer = NewDbContext();
        await writer.Expenditures.Where(e => e.Id == orm.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Amount, 999m), TestContext.Current.CancellationToken);

        Expenditure? locked = await Sut.FindByEmailAndIdForUpdateAsync(alice, orm.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(locked);
        Assert.Equal(999m, locked.Amount.Amount);
    }

    // -- CreateAsync --------------------------------------------------------------

    /// <summary>Verifies that <c>CreateAsync</c> inserts an expenditure row.</summary>
    [Fact]
    public async Task CreateAsync_AddsExpenditureToDatabase()
    {
        string alice = UniqueEmail("alice");
        await EnsureAssetsAsync(alice, "CreditCard");

        var expenditure = Expenditure.Reconstitute(
            id: 0, accountEmail: alice, mainClass: "ConsumerSpending", subClass: "MealOrEatOutExpenses",
            content: "Team lunch", amount: 250m, monetaryUnit: "",
            paymentMethod: "CreditCard", myDepositAsset: "CreditCard", note: null,
            created: DateTime.UtcNow, updated: DateTime.UtcNow);

        await Sut.CreateAsync(expenditure, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmExpenditure? saved = await Context.Expenditures.FirstOrDefaultAsync(
            e => e.AccountEmail == alice && e.Content == "Team lunch", TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal(250m, saved.Amount);
    }

    /// <summary>PostgreSQL enforces <c>Expenditure_fk_0</c> — an unknown <c>PaymentMethod</c> asset throws (InMemory did not).</summary>
    [Fact]
    public async Task CreateAsync_Throws_WhenPaymentMethodAssetDoesNotExist()
    {
        string alice = UniqueEmail("alice");
        await EnsureAccountsAsync(alice);

        var expenditure = Expenditure.Reconstitute(
            id: 0, accountEmail: alice, mainClass: "ConsumerSpending", subClass: "MealOrEatOutExpenses",
            content: "Orphan", amount: 1m, monetaryUnit: "",
            paymentMethod: "NoSuchAsset", myDepositAsset: null, note: null,
            created: DateTime.UtcNow, updated: DateTime.UtcNow);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => Sut.CreateAsync(expenditure, TestContext.Current.CancellationToken));
    }

    // -- UpdateEntityAsync -----------------------------------------------

    /// <summary>Verifies that <c>UpdateEntityAsync</c> persists changes to an existing expenditure.</summary>
    [Fact]
    public async Task UpdateEntityAsync_UpdatesExistingExpenditure()
    {
        string alice = UniqueEmail("alice");
        var orm = MakeExpenditure(alice, "Lunch", amount: 100m);
        await SeedAsync(orm);

        var expenditure = Expenditure.Reconstitute(
            id: orm.Id, accountEmail: alice, mainClass: "ConsumerSpending", subClass: "MealOrEatOutExpenses",
            content: "Updated Lunch", amount: 150m, monetaryUnit: "",
            paymentMethod: "Wallet", myDepositAsset: "Wallet", note: null,
            created: orm.Created, updated: DateTime.UtcNow);

        await Sut.UpdateEntityAsync(expenditure, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmExpenditure? updated = await Context.Expenditures.FirstOrDefaultAsync(e => e.Id == orm.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(150m, updated.Amount);
        Assert.Equal("Updated Lunch", updated.Content);
    }

    // -- DeleteByIdAsync -----------------------------------------------

    /// <summary>Verifies that <c>DeleteByIdAsync</c> removes the expenditure row.</summary>
    [Fact]
    public async Task DeleteByIdAsync_RemovesExpenditureFromDatabase()
    {
        string alice = UniqueEmail("alice");
        var orm = MakeExpenditure(alice, "Taxi");
        await SeedAsync(orm);

        await Sut.DeleteByIdAsync(orm.Id, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Assert.Null(await Context.Expenditures.FirstOrDefaultAsync(e => e.Id == orm.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies that <c>DeleteByIdAsync</c> does not affect other expenditures.</summary>
    [Fact]
    public async Task DeleteByIdAsync_DoesNotAffectOtherExpenditures()
    {
        string alice = UniqueEmail("alice");
        var keep = MakeExpenditure(alice, "Lunch");
        var remove = MakeExpenditure(alice, "Dinner");
        await SeedAsync(keep, remove);

        await Sut.DeleteByIdAsync(remove.Id, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        List<OrmExpenditure> remaining = await Context.Expenditures
            .Where(e => e.AccountEmail == alice).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(keep.Id, Assert.Single(remaining).Id);
    }

    // -- SearchByAccountEmailAsync -----------------------------------------------------

    /// <summary>The whole-row search matches a text column case-insensitively and never crosses accounts.</summary>
    [Fact]
    public async Task SearchByAccountEmailAsync_MatchesTextColumnCaseInsensitively_AndIsScopedToTheAccount()
    {
        string alice = UniqueEmail("alice");
        string bob = UniqueEmail("bob");
        string marker = Unique("Groceries");
        await SeedAsync(
            MakeExpenditure(alice, marker),
            MakeExpenditure(alice, "Lunch"),
            MakeExpenditure(bob, marker));

        List<Expenditure> result = await Sut.SearchByAccountEmailAsync(alice, marker.ToUpperInvariant(), TestContext.Current.CancellationToken);

        Expenditure hit = Assert.Single(result);
        Assert.Equal(marker, hit.Content);
        Assert.Equal(alice, hit.AccountEmail.Value);
    }

    /// <summary>PaymentMethod and MyDepositAsset are both searchable columns.</summary>
    [Fact]
    public async Task SearchByAccountEmailAsync_MatchesPaymentMethodAndMyDepositAsset()
    {
        string alice = UniqueEmail("alice");
        string card = Unique("Card");
        string savings = Unique("Savings");
        await SeedAsync(
            MakeExpenditure(alice, "paid by card", paymentMethod: card, myDepositAsset: null),
            MakeExpenditure(alice, "moved to savings", paymentMethod: "Wallet", myDepositAsset: savings),
            MakeExpenditure(alice, "plain", paymentMethod: "Wallet", myDepositAsset: null));

        List<Expenditure> byCard = await Sut.SearchByAccountEmailAsync(alice, card, TestContext.Current.CancellationToken);
        List<Expenditure> bySavings = await Sut.SearchByAccountEmailAsync(alice, savings, TestContext.Current.CancellationToken);

        Assert.Equal("paid by card", Assert.Single(byCard).Content);
        Assert.Equal("moved to savings", Assert.Single(bySavings).Content);
    }

    /// <summary>Amount is searched through <c>CAST(... AS TEXT)</c>, so its stored text form (<c>7654.32</c>) matches.</summary>
    [Fact]
    public async Task SearchByAccountEmailAsync_MatchesTheAmountAsText()
    {
        string alice = UniqueEmail("alice");
        await SeedAsync(
            MakeExpenditure(alice, "Target", amount: 7654.32m),
            MakeExpenditure(alice, "Other", amount: 12m));

        List<Expenditure> result = await Sut.SearchByAccountEmailAsync(alice, "7654.32", TestContext.Current.CancellationToken);

        Assert.Equal("Target", Assert.Single(result).Content);
    }

    /// <summary>The currency label lives on the payment-method asset — the <c>EXISTS</c> join must find it.</summary>
    [Fact]
    public async Task SearchByAccountEmailAsync_MatchesThePaymentMethodAssetsMonetaryUnit()
    {
        string alice = UniqueEmail("alice");
        string unit = "U" + Token;
        await SeedAsync(
            MakeExpenditure(alice, "In foreign currency", paymentMethod: "Foreign", myDepositAsset: null),
            MakeExpenditure(alice, "In KRW", paymentMethod: "Wallet", myDepositAsset: null));
        await Context.Assets.Where(a => a.AccountEmail == alice && a.ProductName == "Foreign")
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.MonetaryUnit, unit), TestContext.Current.CancellationToken);

        List<Expenditure> result = await Sut.SearchByAccountEmailAsync(alice, unit, TestContext.Current.CancellationToken);

        Assert.Equal("In foreign currency", Assert.Single(result).Content);
    }

    /// <summary><c>%</c> and <c>_</c> in the search text are literals, not LIKE wildcards.</summary>
    [Fact]
    public async Task SearchByAccountEmailAsync_TreatsLikeWildcardsLiterally()
    {
        string alice = UniqueEmail("alice");
        await SeedAsync(
            MakeExpenditure(alice, $"100%-{Token}"),
            MakeExpenditure(alice, $"1000-{Token}"),
            MakeExpenditure(alice, $"a_b-{Token}"),
            MakeExpenditure(alice, $"axb-{Token}"));

        List<Expenditure> percent = await Sut.SearchByAccountEmailAsync(alice, $"0%-{Token}", TestContext.Current.CancellationToken);
        List<Expenditure> underscore = await Sut.SearchByAccountEmailAsync(alice, $"a_b-{Token}", TestContext.Current.CancellationToken);

        Assert.Equal($"100%-{Token}", Assert.Single(percent).Content);
        Assert.Equal($"a_b-{Token}", Assert.Single(underscore).Content);
    }

    /// <summary>No matching row → an empty list (the two-step lookup skips its second query).</summary>
    [Fact]
    public async Task SearchByAccountEmailAsync_ReturnsEmpty_WhenNothingMatches()
    {
        string alice = UniqueEmail("alice");
        await SeedAsync(MakeExpenditure(alice, "Lunch"));

        Assert.Empty(await Sut.SearchByAccountEmailAsync(alice, "no-such-" + Token, TestContext.Current.CancellationToken));
    }

    // -- GetByAccountEmailAndDateRangeAsync ---------------------------------------------

    /// <summary>The range is half-open (<c>from</c> inclusive, <c>to</c> exclusive) and scoped to the account.</summary>
    [Fact]
    public async Task GetByAccountEmailAndDateRangeAsync_IsHalfOpen_AndScopedToTheAccount()
    {
        string alice = UniqueEmail("alice");
        string bob = UniqueEmail("bob");
        var from = new DateTime(2031, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2031, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        await SeedAsync(
            At(alice, "before", from.AddTicks(-1)),
            At(alice, "at-from", from),
            At(alice, "inside", from.AddDays(10)),
            At(alice, "at-to", to),
            At(bob, "other-account", from.AddDays(10)));

        List<Expenditure> result = await Sut.GetByAccountEmailAndDateRangeAsync(alice, from, to, TestContext.Current.CancellationToken);

        Assert.Equal(["at-from", "inside"], result.Select(e => e.Content).Order());
        return;

 #pragma warning disable IDE0062
        OrmExpenditure At(string email, string content, DateTime created)
 #pragma warning restore IDE0062
        {
            OrmExpenditure expenditure = MakeExpenditure(email, content);
            expenditure.Created = created;
            return expenditure;
        }
    }
}

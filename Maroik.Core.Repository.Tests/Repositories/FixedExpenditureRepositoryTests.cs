using Maroik.Core.Domain.Finance;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using OrmFixedExpenditure = Maroik.Core.PostgreSQL.Models.FixedExpenditure;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="FixedExpenditureRepository"/> against a real PostgreSQL database
/// (Testcontainers) preloaded with the <c>Init.sql</c> schema and seed data.
/// <c>FixedExpenditure</c> carries the composite FKs <c>FixedExpenditure_fk_0</c> on
/// <c>(PaymentMethod, AccountEmail)</c> and <c>FixedExpenditure_fk_1</c> on
/// <c>(MyDepositAsset, AccountEmail)</c>. Includes <c>UpdateMyDepositAssetWithProductNameAsync</c>
/// (<c>ExecuteUpdateAsync</c>), which the EF Core InMemory provider could not run.
/// </summary>
public sealed class FixedExpenditureRepositoryTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    private FixedExpenditureRepository Sut => new(Context);

    /// <summary>An unsaved fixed-expenditure row owned by <paramref name="email"/>.</summary>
    private static OrmFixedExpenditure MakeFixedExpenditure(
        string email, string content, string paymentMethod = "Wallet", string? myDepositAsset = "Wallet",
        decimal amount = 50m) => new()
    {
        AccountEmail = email,
        MainClass = "ConsumerSpending",
        SubClass = "Tax",
        Content = content,
        Amount = amount,
        PaymentMethod = paymentMethod,
        MyDepositAsset = myDepositAsset,
        DepositMonth = 1,
        DepositDay = 25,
        MaturityDate = DateTime.UtcNow.AddYears(1),
        Note = "",
        Created = DateTime.UtcNow,
        Updated = DateTime.UtcNow,
        Unpunctuality = false
    };

    /// <summary>Ensures each owner account and every referenced asset exist, then inserts <paramref name="items"/>, saves, and clears the change tracker.</summary>
    private async Task SeedAsync(params OrmFixedExpenditure[] items)
    {
        foreach (var group in items.GroupBy(e => e.AccountEmail!))
            await EnsureAssetsAsync(group.Key, [.. group.SelectMany(e => new[] { e.PaymentMethod, e.MyDepositAsset })]);

        await Context.FixedExpenditures.AddRangeAsync(items);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    // -- GetByAccountEmailAsync --------------------------------------------------

    /// <summary>Verifies that <c>GetByAccountEmailAsync</c> returns only items for the given e-mail.</summary>
    [Fact]
    public async Task GetByAccountEmailAsync_ReturnsOnlyItemsForGivenEmail()
    {
        string alice = UniqueEmail("alice");
        string bob = UniqueEmail("bob");
        await SeedAsync(
            MakeFixedExpenditure(alice, "Netflix"),
            MakeFixedExpenditure(alice, "Insurance"),
            MakeFixedExpenditure(bob, "Gym"));

        List<FixedExpenditure> result = await Sut.GetByAccountEmailAsync(alice, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.All(result, fe => Assert.Equal(alice, fe.AccountEmail.Value));
    }

    /// <summary>Verifies that <c>GetByAccountEmailAsync</c> returns empty when the e-mail has no items.</summary>
    [Fact]
    public async Task GetByAccountEmailAsync_ReturnsEmpty_WhenNoItemsForEmail()
    {
        List<FixedExpenditure> result = await Sut.GetByAccountEmailAsync(UniqueEmail("nobody"), TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    // -- FindByEmailAndIdAsync -------------------------------------------------

    /// <summary>Verifies that <c>FindByEmailAndIdAsync</c> returns the item when owned by the e-mail.</summary>
    [Fact]
    public async Task FindByEmailAndIdAsync_ReturnsItem_WhenOwnedByEmail()
    {
        string alice = UniqueEmail("alice");
        var orm = MakeFixedExpenditure(alice, "Netflix", amount: 17m);
        await SeedAsync(orm);

        FixedExpenditure? result = await Sut.FindByEmailAndIdAsync(alice, orm.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(17m, result.Amount.Amount);
    }

    /// <summary>Verifies that <c>FindByEmailAndIdAsync</c> returns null for another account's id.</summary>
    [Fact]
    public async Task FindByEmailAndIdAsync_ReturnsNull_WhenNotOwnedByEmail()
    {
        var orm = MakeFixedExpenditure(UniqueEmail("bob"), "Gym");
        await SeedAsync(orm);

        FixedExpenditure? result = await Sut.FindByEmailAndIdAsync(UniqueEmail("alice"), orm.Id, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // -- CreateAsync ------------------------------------------

    /// <summary>Verifies that <c>CreateAsync</c> inserts a fixed-expenditure row.</summary>
    [Fact]
    public async Task CreateAsync_AddsItemToDatabase()
    {
        string alice = UniqueEmail("alice");
        await EnsureAssetsAsync(alice, "BankAccount");

        var fixedExpenditure = FixedExpenditure.Reconstitute(
            id: 0, accountEmail: alice, mainClass: "ConsumerSpending", subClass: "Tax",
            content: "Property tax", amount: 200m, monetaryUnit: "",
            paymentMethod: "BankAccount", myDepositAsset: "BankAccount", depositMonth: 3, depositDay: 31,
            maturityDate: DateTime.UtcNow.AddYears(1), note: null, unpunctuality: false,
            created: DateTime.UtcNow, updated: DateTime.UtcNow);

        await Sut.CreateAsync(fixedExpenditure, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmFixedExpenditure? saved = await Context.FixedExpenditures.FirstOrDefaultAsync(
            fe => fe.AccountEmail == alice && fe.Content == "Property tax", TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal(200m, saved.Amount);
        Assert.Equal((short)3, saved.DepositMonth);
        Assert.Equal((short)31, saved.DepositDay);
    }

    // -- UpdateEntityAsync ------------------------------------------

    /// <summary>Verifies that <c>UpdateEntityAsync</c> persists changes to an existing item.</summary>
    [Fact]
    public async Task UpdateEntityAsync_UpdatesExistingItem()
    {
        string alice = UniqueEmail("alice");
        var orm = MakeFixedExpenditure(alice, "Netflix", amount: 50m);
        await SeedAsync(orm);

        var fixedExpenditure = FixedExpenditure.Reconstitute(
            id: orm.Id, accountEmail: alice, mainClass: "ConsumerSpending", subClass: "Tax",
            content: "Netflix Premium", amount: 75m, monetaryUnit: "",
            paymentMethod: "Wallet", myDepositAsset: "Wallet", depositMonth: 1, depositDay: 25,
            maturityDate: orm.MaturityDate, note: null, unpunctuality: true,
            created: orm.Created, updated: DateTime.UtcNow);

        await Sut.UpdateEntityAsync(fixedExpenditure, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmFixedExpenditure? updated = await Context.FixedExpenditures.FirstOrDefaultAsync(fe => fe.Id == orm.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(75m, updated.Amount);
        Assert.Equal("Netflix Premium", updated.Content);
        Assert.True(updated.Unpunctuality);
    }

    // -- DeleteByIdAsync ------------------------------------------

    /// <summary>Verifies that <c>DeleteByIdAsync</c> removes the item row.</summary>
    [Fact]
    public async Task DeleteByIdAsync_RemovesItemFromDatabase()
    {
        string alice = UniqueEmail("alice");
        var orm = MakeFixedExpenditure(alice, "Spotify");
        await SeedAsync(orm);

        await Sut.DeleteByIdAsync(orm.Id, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Assert.Null(await Context.FixedExpenditures.FirstOrDefaultAsync(fe => fe.Id == orm.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies that <c>DeleteByIdAsync</c> does not affect other items.</summary>
    [Fact]
    public async Task DeleteByIdAsync_DoesNotAffectOtherItems()
    {
        string alice = UniqueEmail("alice");
        var keep = MakeFixedExpenditure(alice, "Netflix");
        var remove = MakeFixedExpenditure(alice, "Spotify");
        await SeedAsync(keep, remove);

        await Sut.DeleteByIdAsync(remove.Id, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        List<OrmFixedExpenditure> remaining = await Context.FixedExpenditures
            .Where(fe => fe.AccountEmail == alice).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(keep.Id, Assert.Single(remaining).Id);
    }

    // -- SearchByAccountEmailAsync -----------------------------------------------------

    /// <summary>The whole-row search matches a text column case-insensitively and never crosses accounts.</summary>
    [Fact]
    public async Task SearchByAccountEmailAsync_MatchesTextColumnCaseInsensitively_AndIsScopedToTheAccount()
    {
        string alice = UniqueEmail("alice");
        string bob = UniqueEmail("bob");
        string marker = Unique("Rent");
        await SeedAsync(
            MakeFixedExpenditure(alice, marker),
            MakeFixedExpenditure(alice, "Insurance"),
            MakeFixedExpenditure(bob, marker));

        List<FixedExpenditure> result = await Sut.SearchByAccountEmailAsync(alice, marker.ToUpperInvariant(), TestContext.Current.CancellationToken);

        FixedExpenditure hit = Assert.Single(result);
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
            MakeFixedExpenditure(alice, "paid by card", paymentMethod: card, myDepositAsset: null),
            MakeFixedExpenditure(alice, "moved to savings", paymentMethod: "Wallet", myDepositAsset: savings),
            MakeFixedExpenditure(alice, "plain", paymentMethod: "Wallet", myDepositAsset: null));

        List<FixedExpenditure> byCard = await Sut.SearchByAccountEmailAsync(alice, card, TestContext.Current.CancellationToken);
        List<FixedExpenditure> bySavings = await Sut.SearchByAccountEmailAsync(alice, savings, TestContext.Current.CancellationToken);

        Assert.Equal("paid by card", Assert.Single(byCard).Content);
        Assert.Equal("moved to savings", Assert.Single(bySavings).Content);
    }

    /// <summary>MaturityDate is searched through <c>CAST(... AS TEXT)</c> (ISO form).</summary>
    [Fact]
    public async Task SearchByAccountEmailAsync_MatchesTheMaturityDateAsText()
    {
        string alice = UniqueEmail("alice");
        OrmFixedExpenditure matures = MakeFixedExpenditure(alice, "Matures in 2099");
        matures.MaturityDate = new DateTime(2099, 12, 31, 12, 0, 0, DateTimeKind.Utc);
        await SeedAsync(matures, MakeFixedExpenditure(alice, "Matures next year"));

        List<FixedExpenditure> result = await Sut.SearchByAccountEmailAsync(alice, "2099-12-31", TestContext.Current.CancellationToken);

        Assert.Equal("Matures in 2099", Assert.Single(result).Content);
    }

    /// <summary>The currency label lives on the payment-method asset — the <c>EXISTS</c> join must find it.</summary>
    [Fact]
    public async Task SearchByAccountEmailAsync_MatchesThePaymentMethodAssetsMonetaryUnit()
    {
        string alice = UniqueEmail("alice");
        string unit = "U" + Token;
        await SeedAsync(
            MakeFixedExpenditure(alice, "In foreign currency", paymentMethod: "Foreign", myDepositAsset: null),
            MakeFixedExpenditure(alice, "In KRW", paymentMethod: "Wallet", myDepositAsset: null));
        await Context.Assets.Where(a => a.AccountEmail == alice && a.ProductName == "Foreign")
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.MonetaryUnit, unit), TestContext.Current.CancellationToken);

        List<FixedExpenditure> result = await Sut.SearchByAccountEmailAsync(alice, unit, TestContext.Current.CancellationToken);

        Assert.Equal("In foreign currency", Assert.Single(result).Content);
    }

    /// <summary><c>%</c> and <c>_</c> in the search text are literals, not LIKE wildcards.</summary>
    [Fact]
    public async Task SearchByAccountEmailAsync_TreatsLikeWildcardsLiterally()
    {
        string alice = UniqueEmail("alice");
        await SeedAsync(
            MakeFixedExpenditure(alice, $"100%-{Token}"),
            MakeFixedExpenditure(alice, $"1000-{Token}"),
            MakeFixedExpenditure(alice, $"a_b-{Token}"),
            MakeFixedExpenditure(alice, $"axb-{Token}"));

        List<FixedExpenditure> percent = await Sut.SearchByAccountEmailAsync(alice, $"0%-{Token}", TestContext.Current.CancellationToken);
        List<FixedExpenditure> underscore = await Sut.SearchByAccountEmailAsync(alice, $"a_b-{Token}", TestContext.Current.CancellationToken);

        Assert.Equal($"100%-{Token}", Assert.Single(percent).Content);
        Assert.Equal($"a_b-{Token}", Assert.Single(underscore).Content);
    }

    /// <summary>No matching row → an empty list (the two-step lookup skips its second query).</summary>
    [Fact]
    public async Task SearchByAccountEmailAsync_ReturnsEmpty_WhenNothingMatches()
    {
        string alice = UniqueEmail("alice");
        await SeedAsync(MakeFixedExpenditure(alice, "Rent"));

        Assert.Empty(await Sut.SearchByAccountEmailAsync(alice, "no-such-" + Token, TestContext.Current.CancellationToken));
    }
}

using Maroik.Core.Domain.Finance;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using OrmFixedIncome = Maroik.Core.PostgreSQL.Models.FixedIncome;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="FixedIncomeRepository"/> against a real PostgreSQL database (Testcontainers)
/// preloaded with the <c>Init.sql</c> schema and seed data. <c>FixedIncome</c> carries
/// the composite FK <c>FixedIncome_fk_0</c> to <c>Asset(ProductName, AccountEmail)</c> which the
/// repository eager-loads; each test seeds the owning account and asset and scopes to its own
/// unique account e-mail.
/// </summary>
public sealed class FixedIncomeRepositoryTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    private FixedIncomeRepository Sut => new(Context);

    private static OrmFixedIncome MakeFixedIncome(string email, string content, string asset = "BankAccount", decimal amount = 3000m) => new()
    {
        AccountEmail = email,
        MainClass = "RegularIncome",
        SubClass = "LaborIncome",
        Content = content,
        Amount = amount,
        DepositMyAssetProductName = asset,
        DepositMonth = 1,
        DepositDay = 25,
        MaturityDate = DateTime.UtcNow.AddYears(1),
        Note = "",
        Created = DateTime.UtcNow,
        Updated = DateTime.UtcNow,
        Unpunctuality = false
    };

    private async Task SeedAsync(params OrmFixedIncome[] items)
    {
        foreach (var group in items.GroupBy(i => i.AccountEmail!))
            await EnsureAssetsAsync(group.Key, [.. group.Select(i => i.DepositMyAssetProductName)]);

        await Context.FixedIncomes.AddRangeAsync(items);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    // -- GetByAccountEmailAsync ---------------------------------------------------

    /// <summary>Verifies that <c>GetByAccountEmailAsync</c> returns only items for the given e-mail.</summary>
    [Fact]
    public async Task GetByAccountEmailAsync_ReturnsOnlyItemsForGivenEmail()
    {
        string alice = UniqueEmail("alice");
        string bob = UniqueEmail("bob");
        await SeedAsync(
            MakeFixedIncome(alice, "Salary"),
            MakeFixedIncome(alice, "Side job"),
            MakeFixedIncome(bob, "Bob salary"));

        List<FixedIncome> result = await Sut.GetByAccountEmailAsync(alice, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.All(result, fi => Assert.Equal(alice, fi.AccountEmail.Value));
    }

    /// <summary>Verifies that <c>GetByAccountEmailAsync</c> returns empty when the e-mail has no items.</summary>
    [Fact]
    public async Task GetByAccountEmailAsync_ReturnsEmpty_WhenNoItemsForEmail()
    {
        List<FixedIncome> result = await Sut.GetByAccountEmailAsync(UniqueEmail("nobody"), TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    // -- FindByEmailAndIdAsync -------------------------------------------------

    /// <summary>Verifies that <c>FindByEmailAndIdAsync</c> returns the item when owned by the e-mail.</summary>
    [Fact]
    public async Task FindByEmailAndIdAsync_ReturnsItem_WhenOwnedByEmail()
    {
        string alice = UniqueEmail("alice");
        var orm = MakeFixedIncome(alice, "Salary", amount: 4200m);
        await SeedAsync(orm);

        FixedIncome? result = await Sut.FindByEmailAndIdAsync(alice, orm.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(4200m, result.Amount.Amount);
    }

    /// <summary>Verifies that <c>FindByEmailAndIdAsync</c> returns null for another account's id.</summary>
    [Fact]
    public async Task FindByEmailAndIdAsync_ReturnsNull_WhenNotOwnedByEmail()
    {
        var orm = MakeFixedIncome(UniqueEmail("bob"), "Salary");
        await SeedAsync(orm);

        FixedIncome? result = await Sut.FindByEmailAndIdAsync(UniqueEmail("alice"), orm.Id, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // -- CreateAsync --------------------------------------------------------------

    /// <summary>Verifies that <c>CreateAsync</c> inserts a fixed-income row.</summary>
    [Fact]
    public async Task CreateAsync_AddsItemToDatabase()
    {
        string alice = UniqueEmail("alice");
        await EnsureAssetsAsync(alice, "BankAccount");

        var fixedIncome = FixedIncome.Reconstitute(
            id: 0, accountEmail: alice, mainClass: "RegularIncome", subClass: "LaborIncome",
            content: "Monthly salary", amount: 5000m, monetaryUnit: "",
            depositMyAssetProductName: "BankAccount", depositMonth: 1, depositDay: 25,
            maturityDate: DateTime.UtcNow.AddYears(2), note: null, unpunctuality: false,
            created: DateTime.UtcNow, updated: DateTime.UtcNow);

        await Sut.CreateAsync(fixedIncome, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmFixedIncome? saved = await Context.FixedIncomes.FirstOrDefaultAsync(
            fi => fi.AccountEmail == alice && fi.Content == "Monthly salary", TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal(5000m, saved.Amount);
        Assert.Equal((short)25, saved.DepositDay);
    }

    // -- UpdateEntityAsync -----------------------------------------------

    /// <summary>Verifies that <c>UpdateEntityAsync</c> persists changes to an existing item.</summary>
    [Fact]
    public async Task UpdateEntityAsync_UpdatesExistingItem()
    {
        string alice = UniqueEmail("alice");
        var orm = MakeFixedIncome(alice, "Salary", amount: 3000m);
        await SeedAsync(orm);

        var fixedIncome = FixedIncome.Reconstitute(
            id: orm.Id, accountEmail: alice, mainClass: "RegularIncome", subClass: "LaborIncome",
            content: "Salary (Raise)", amount: 3500m, monetaryUnit: "",
            depositMyAssetProductName: "BankAccount", depositMonth: 1, depositDay: 25,
            maturityDate: orm.MaturityDate, note: null, unpunctuality: true,
            created: orm.Created, updated: DateTime.UtcNow);

        await Sut.UpdateEntityAsync(fixedIncome, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmFixedIncome? updated = await Context.FixedIncomes.FirstOrDefaultAsync(fi => fi.Id == orm.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(3500m, updated.Amount);
        Assert.Equal("Salary (Raise)", updated.Content);
        Assert.True(updated.Unpunctuality);
    }

    // -- DeleteByIdAsync -----------------------------------------------

    /// <summary>Verifies that <c>DeleteByIdAsync</c> removes the item row.</summary>
    [Fact]
    public async Task DeleteByIdAsync_RemovesItemFromDatabase()
    {
        string alice = UniqueEmail("alice");
        var orm = MakeFixedIncome(alice, "Pension");
        await SeedAsync(orm);

        await Sut.DeleteByIdAsync(orm.Id, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Assert.Null(await Context.FixedIncomes.FirstOrDefaultAsync(fi => fi.Id == orm.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies that <c>DeleteByIdAsync</c> does not affect other items.</summary>
    [Fact]
    public async Task DeleteByIdAsync_DoesNotAffectOtherItems()
    {
        string alice = UniqueEmail("alice");
        var keep = MakeFixedIncome(alice, "Salary");
        var remove = MakeFixedIncome(alice, "Side job");
        await SeedAsync(keep, remove);

        await Sut.DeleteByIdAsync(remove.Id, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        List<OrmFixedIncome> remaining = await Context.FixedIncomes
            .Where(fi => fi.AccountEmail == alice).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(keep.Id, Assert.Single(remaining).Id);
    }

    // -- SearchByAccountEmailAsync -----------------------------------------------------

    /// <summary>The whole-row search matches a text column case-insensitively and never crosses accounts.</summary>
    [Fact]
    public async Task SearchByAccountEmailAsync_MatchesTextColumnCaseInsensitively_AndIsScopedToTheAccount()
    {
        string alice = UniqueEmail("alice");
        string bob = UniqueEmail("bob");
        string marker = Unique("Pension");
        await SeedAsync(
            MakeFixedIncome(alice, marker),
            MakeFixedIncome(alice, "Salary"),
            MakeFixedIncome(bob, marker));

        List<FixedIncome> result = await Sut.SearchByAccountEmailAsync(alice, marker.ToUpperInvariant(), TestContext.Current.CancellationToken);

        FixedIncome hit = Assert.Single(result);
        Assert.Equal(marker, hit.Content);
        Assert.Equal(alice, hit.AccountEmail.Value);
    }

    /// <summary>Amount is searched through <c>CAST(... AS TEXT)</c>, so its stored text form (<c>7654.32</c>) matches.</summary>
    [Fact]
    public async Task SearchByAccountEmailAsync_MatchesTheAmountAsText()
    {
        string alice = UniqueEmail("alice");
        await SeedAsync(
            MakeFixedIncome(alice, "Target", amount: 7654.32m),
            MakeFixedIncome(alice, "Other", amount: 12m));

        List<FixedIncome> result = await Sut.SearchByAccountEmailAsync(alice, "7654.32", TestContext.Current.CancellationToken);

        Assert.Equal("Target", Assert.Single(result).Content);
    }

    /// <summary>MaturityDate is searched through <c>CAST(... AS TEXT)</c> (ISO form).</summary>
    [Fact]
    public async Task SearchByAccountEmailAsync_MatchesTheMaturityDateAsText()
    {
        string alice = UniqueEmail("alice");
        OrmFixedIncome matures = MakeFixedIncome(alice, "Matures in 2099");
        matures.MaturityDate = new DateTime(2099, 12, 31, 12, 0, 0, DateTimeKind.Utc);
        await SeedAsync(matures, MakeFixedIncome(alice, "Matures next year"));

        List<FixedIncome> result = await Sut.SearchByAccountEmailAsync(alice, "2099-12-31", TestContext.Current.CancellationToken);

        Assert.Equal("Matures in 2099", Assert.Single(result).Content);
    }

    /// <summary>The currency label lives on the asset, not the fixed-income row — the <c>EXISTS</c> join must find it.</summary>
    [Fact]
    public async Task SearchByAccountEmailAsync_MatchesTheDepositAssetsMonetaryUnit()
    {
        string alice = UniqueEmail("alice");
        string unit = "U" + Token;
        await SeedAsync(
            MakeFixedIncome(alice, "In foreign currency", asset: "Foreign"),
            MakeFixedIncome(alice, "In KRW", asset: "BankAccount"));
        await Context.Assets.Where(a => a.AccountEmail == alice && a.ProductName == "Foreign")
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.MonetaryUnit, unit), TestContext.Current.CancellationToken);

        List<FixedIncome> result = await Sut.SearchByAccountEmailAsync(alice, unit, TestContext.Current.CancellationToken);

        Assert.Equal("In foreign currency", Assert.Single(result).Content);
    }

    /// <summary><c>%</c> and <c>_</c> in the search text are literals, not LIKE wildcards.</summary>
    [Fact]
    public async Task SearchByAccountEmailAsync_TreatsLikeWildcardsLiterally()
    {
        string alice = UniqueEmail("alice");
        await SeedAsync(
            MakeFixedIncome(alice, $"100%-{Token}"),
            MakeFixedIncome(alice, $"1000-{Token}"),
            MakeFixedIncome(alice, $"a_b-{Token}"),
            MakeFixedIncome(alice, $"axb-{Token}"));

        List<FixedIncome> percent = await Sut.SearchByAccountEmailAsync(alice, $"0%-{Token}", TestContext.Current.CancellationToken);
        List<FixedIncome> underscore = await Sut.SearchByAccountEmailAsync(alice, $"a_b-{Token}", TestContext.Current.CancellationToken);

        Assert.Equal($"100%-{Token}", Assert.Single(percent).Content);
        Assert.Equal($"a_b-{Token}", Assert.Single(underscore).Content);
    }

    /// <summary>No matching row → an empty list (the two-step lookup skips its second query).</summary>
    [Fact]
    public async Task SearchByAccountEmailAsync_ReturnsEmpty_WhenNothingMatches()
    {
        string alice = UniqueEmail("alice");
        await SeedAsync(MakeFixedIncome(alice, "Salary"));

        Assert.Empty(await Sut.SearchByAccountEmailAsync(alice, "no-such-" + Token, TestContext.Current.CancellationToken));
    }
}

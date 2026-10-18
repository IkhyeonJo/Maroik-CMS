using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.PostgreSQL.Contexts;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Website.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Maroik.Website.Tests.Services;

/// <summary>
/// Lock order between renaming an asset and editing a record that uses it, against the real PostgreSQL schema. An
/// income / expenditure edit locks its record, then its assets; a rename's <c>ON UPDATE CASCADE</c> rewrites the
/// referencing records, so it must also take the records before the asset — otherwise each holds what the other waits
/// for, and PostgreSQL ends one of them as a deadlock (40P01).
/// </summary>
[Collection("Website Integration")]
public class AssetRenameLockOrderTests(MaroikWebApplicationFactory factory)
{
    /// <summary>
    /// The interleaving that deadlocked: the edit holds the expenditure and waits for its new payment asset (held here by
    /// a blocker), the rename starts and waits behind the edit, then the blocker lets go. Both must succeed.
    /// </summary>
    [Fact]
    public async Task RenamingAnAsset_WhileAnExpenditureOfItIsBeingEdited_LetsBothFinish()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"asset-rename-lock-{suffix}@test.com";
        // Ordinal order matters: the edit locks [newPayment, oldPayment] in name order, so the blocked "a-" asset is
        // requested before the "b-" asset the rename takes.
        string newPayment = $"a-{suffix}";
        string oldPayment = $"b-{suffix}";
        string renamed = $"c-{suffix}";

        long expenditureId;
        string connectionString;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            connectionString = db.Database.GetConnectionString()!;
            db.Accounts.Add(new Account
            {
                Email = email, HashedPassword = "$2a$13$placeholder", Nickname = $"lock-{suffix}",
                AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg", Role = "User", TimeZoneIanaId = "UTC",
                Locked = false, LoginAttempt = 0, EmailConfirmed = true, AgreedServiceTerms = true, Deleted = false,
                Created = DateTime.UtcNow, Updated = DateTime.UtcNow
            });
            foreach (string name in new[] { newPayment, oldPayment })
                db.Assets.Add(new Asset
                {
                    ProductName = name, AccountEmail = email, Item = "CashAsset", Amount = 1000, MonetaryUnit = "KRW",
                    Note = "", Deleted = false, Created = DateTime.UtcNow, Updated = DateTime.UtcNow
                });
            var expenditure = new Expenditure
            {
                AccountEmail = email, MainClass = "ConsumerSpending", SubClass = "MealOrEatOutExpenses", Content = "Lunch",
                Amount = 10, PaymentMethod = oldPayment, Created = DateTime.UtcNow, Updated = DateTime.UtcNow
            };
            db.Expenditures.Add(expenditure);
            await db.SaveChangesAsync(ct);
            expenditureId = expenditure.Id;
        }

        await using var blocker = new NpgsqlConnection(connectionString);
        await blocker.OpenAsync(ct);
        await using var blockerTransaction = await blocker.BeginTransactionAsync(ct);
        await using (var lockNewPayment = new NpgsqlCommand(
            """SELECT 1 FROM "Asset" WHERE "AccountEmail" = @email AND "ProductName" = @name FOR UPDATE""", blocker, blockerTransaction))
        {
            lockNewPayment.Parameters.AddWithValue("email", email);
            lockNewPayment.Parameters.AddWithValue("name", newPayment);
            await lockNewPayment.ExecuteNonQueryAsync(ct);
        }

        await using var editScope = factory.Services.CreateAsyncScope();
        await using var renameScope = factory.Services.CreateAsyncScope();
        Task<ServiceResult> edit = editScope.ServiceProvider.GetRequiredService<IExpenditureService>().UpdateAsync(email, new ExpenditureRequest
        {
            Id = expenditureId, MainClass = "ConsumerSpending", SubClass = "MealOrEatOutExpenses", Content = "Lunch",
            Amount = 10, PaymentMethod = newPayment, Created = DateTime.UtcNow, Note = ""
        }, ct);
        await WaitForLockWaitersAsync(connectionString, 1, ct);

        Task<ServiceResult> rename = renameScope.ServiceProvider.GetRequiredService<IAssetService>().UpdateAsync(email, new AssetRequest
        {
            ProductName = renamed, Item = "CashAsset", Amount = 1000, MonetaryUnit = "KRW", Note = "", Deleted = false
        }, oldPayment, ct);
        await WaitForLockWaitersAsync(connectionString, 2, ct);

        await blockerTransaction.CommitAsync(ct);
        ServiceResult[] results = await Task.WhenAll(edit, rename);

        Assert.True(results[0].Success, $"edit: {results[0].ErrorCode}");
        Assert.True(results[1].Success, $"rename: {results[1].ErrorCode}");
        using var verifyScope = factory.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(newPayment, (await verify.Expenditures.SingleAsync(e => e.Id == expenditureId, ct)).PaymentMethod);
        Assert.True(await verify.Assets.AnyAsync(a => a.AccountEmail == email && a.ProductName == renamed, ct));
    }

    /// <summary>Waits (up to 10 s) until at least <paramref name="count"/> sessions of this database are waiting on a lock.</summary>
    private static async Task WaitForLockWaitersAsync(string connectionString, int count, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var query = new NpgsqlCommand(
            "SELECT count(*) FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND datname = current_database()", connection);
        for (int attempt = 0; attempt < 200; attempt++)
        {
            if ((long)(await query.ExecuteScalarAsync(ct))! >= count) return;
            await Task.Delay(50, ct);
        }

        throw new TimeoutException($"Fewer than {count} sessions were waiting on a lock.");
    }
}

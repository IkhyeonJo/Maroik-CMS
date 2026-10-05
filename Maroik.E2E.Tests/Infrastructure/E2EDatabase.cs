using Npgsql;

namespace Maroik.E2E.Tests.Infrastructure;

/// <summary>An account seeded straight into the E2E database, ready to sign in through the UI.</summary>
/// <param name="Email">Sign-in e-mail.</param>
/// <param name="Password">Plain-text password (meets the password policy).</param>
/// <param name="Nickname">Unique nickname.</param>
public sealed record SeededAccount(string Email, string Password, string Nickname);

/// <summary>
/// Direct access to the seeded E2E PostgreSQL database: seeds the fixtures a browser flow needs (an
/// account to sign in as, an asset to spend from, ...) without driving the UI for setup, and reads
/// rows back so a flow can assert what the server actually persisted, not just what the page showed.
/// Every seeded row uses keys unique to the call, so tests in the shared container never collide.
/// </summary>
public sealed class E2EDatabase(string connectionString)
{
    /// <summary>The password every seeded account signs in with.</summary>
    private const string DefaultPassword = "E2ePassw0rd!";

    /// <summary>A random 10-character key for building unique e-mails, nicknames and names.</summary>
    private static string NewKey() => Guid.NewGuid().ToString("N")[..10];

    /// <summary>
    /// Inserts a confirmed, unlocked account of <paramref name="role"/> (bypassing registration and its
    /// e-mail confirmation) and returns its sign-in credentials.
    /// </summary>
    public async Task<SeededAccount> SeedAccountAsync(string role = "User", string? nickname = null)
    {
        string key = NewKey();
        var account = new SeededAccount($"e2e-{key}@test.com", DefaultPassword, nickname ?? $"E2E{key}");

        // Work factor 4: BCrypt.Verify reads the factor from the stored hash, so the sign-in path
        // is unchanged while the seed stays fast.
        string hash = BCrypt.Net.BCrypt.HashPassword(account.Password, 4);

        await ExecuteAsync(
            """
            INSERT INTO "Account" ("Email", "HashedPassword", "Nickname", "Role", "TimeZoneIanaId",
                                   "Locked", "LoginAttempt", "EmailConfirmed", "AgreedServiceTerms", "Deleted")
            VALUES (@email, @hash, @nickname, @role, 'Asia/Seoul', false, 0, true, true, false)
            """,
            ("email", account.Email), ("hash", hash), ("nickname", account.Nickname), ("role", role));

        return account;
    }

    /// <summary>Inserts an asset owned by <paramref name="email"/>.</summary>
    public Task SeedAssetAsync(string email, string productName, decimal amount, string unit = "KRW", string item = "FreeDepositAndWithdrawal")
        => ExecuteAsync(
            """
            INSERT INTO "Asset" ("ProductName", "AccountEmail", "Item", "Amount", "MonetaryUnit", "Created", "Updated", "Note", "Deleted")
            VALUES (@name, @email, @item, @amount, @unit, now(), now(), '', false)
            """,
            ("name", productName), ("email", email), ("item", item), ("amount", amount), ("unit", unit));

    /// <summary>Inserts a monthly fixed income (deposit on the 15th of January, maturing far in the future).</summary>
    public Task SeedFixedIncomeAsync(string email, string depositAsset, string content, bool unpunctuality = false)
        => ExecuteAsync(
            """
            INSERT INTO "FixedIncome" ("AccountEmail", "MainClass", "SubClass", "Content", "Amount", "DepositMyAssetProductName",
                                       "DepositMonth", "DepositDay", "MaturityDate", "Unpunctuality")
            VALUES (@email, 'RegularIncome', 'LaborIncome', @content, 1000, @asset, 1, 15, '2099-12-31', @unpunctuality)
            """,
            ("email", email), ("content", content), ("asset", depositAsset), ("unpunctuality", unpunctuality));

    /// <summary>Inserts a monthly fixed expenditure paid from <paramref name="paymentAsset"/> (a consumer-spending schedule).</summary>
    public Task SeedFixedExpenditureAsync(string email, string paymentAsset, string content, bool unpunctuality = false)
        => ExecuteAsync(
            """
            INSERT INTO "FixedExpenditure" ("AccountEmail", "MainClass", "SubClass", "Content", "Amount", "PaymentMethod",
                                            "DepositMonth", "DepositDay", "MaturityDate", "Unpunctuality")
            VALUES (@email, 'ConsumerSpending', 'MealOrEatOutExpenses', @content, 100, @asset, 1, 15, '2099-12-31', @unpunctuality)
            """,
            ("email", email), ("content", content), ("asset", paymentAsset), ("unpunctuality", unpunctuality));

    /// <summary>
    /// Inserts a free-forum post by <paramref name="writer"/> whose body is <paramref name="contentHtml"/>
    /// (images reference their storage path in <c>alt</c>, as stored posts do) and, when
    /// <paramref name="attachmentPath"/> is given, an attachment "payload.zip" stored there. Returns the post id.
    /// </summary>
    public async Task<long> SeedFreeForumPostAsync(string writer, string contentHtml, string? attachmentPath = null, long attachmentSize = 0)
    {
        long id = await SeedBoardAsync("FreeForum", writer, contentHtml);

        if (attachmentPath != null)
            await ExecuteAsync(
                """
                INSERT INTO "BoardAttachedFile" ("BoardId", "Size", "Name", "Extension", "Path")
                VALUES (@id, @size, 'payload', '.zip', @path)
                """,
                ("id", id), ("size", attachmentSize), ("path", attachmentPath));

        return id;
    }

    /// <summary>Seeds a private note by <paramref name="writer"/> (a nickname) with <paramref name="contentHtml"/> as its body. Returns its id.</summary>
    public Task<long> SeedPrivateNoteAsync(string writer, string contentHtml) => SeedBoardAsync("PrivateNote", writer, contentHtml);

    /// <summary>Inserts a <c>Board</c> row of <paramref name="type"/> and returns its id.</summary>
    private Task<long> SeedBoardAsync(string type, string writer, string contentHtml) =>
        ScalarAsync<long>(
            """
            INSERT INTO "Board" ("Type", "Title", "Content", "Writer", "Created", "Updated", "View", "Deleted", "Locked", "Noticed")
            VALUES (@type, @title, @content, @writer, now(), now(), 0, false, false, false)
            RETURNING "Id"
            """,
            ("type", type), ("title", $"E2E post {NewKey()}"), ("content", contentHtml), ("writer", writer));

    /// <summary>Runs a statement that returns no rows.</summary>
    public async Task ExecuteAsync(string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = Build(connection, sql, parameters);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Runs a query and returns the first column of the first row (or <see langword="default"/> when there is none).</summary>
    public async Task<T?> ScalarAsync<T>(string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = Build(connection, sql, parameters);
        object? result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default : (T)Convert.ChangeType(result, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    /// <summary>A command for <paramref name="sql"/> with each named parameter bound (<see langword="null"/> as <c>DBNull</c>).</summary>
    private static NpgsqlCommand Build(NpgsqlConnection connection, string sql, (string Name, object? Value)[] parameters)
    {
        var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }
}

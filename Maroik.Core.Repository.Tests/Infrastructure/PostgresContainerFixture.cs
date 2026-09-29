using Npgsql;
using Testcontainers.PostgreSql;

namespace Maroik.Core.Repository.Tests.Infrastructure;

/// <summary>
/// One PostgreSQL 17 container shared by the whole test assembly.
/// <para>
/// On startup, it loads the real production schema <b>and its seed data</b> from
/// <c>Maroik.DB/PostgreSQL/SQL_Init_Script/Debugging/Init.sql</c> into a template database.
/// Every repository test class then gets its own throwaway database cloned from that template
/// (<see cref="CloneTemplateAsync"/> / <see cref="DropDatabaseAsync"/>), so tests run against the
/// real CHECK/FK constraints, column defaults (<c>now()</c>, <c>gen_random_uuid()</c>),
/// <c>ON UPDATE/DELETE CASCADE</c> behavior and the seed rows — while staying isolated from
/// one another. This replaces the EF Core InMemory provider, which modeled none of that.
/// </para>
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    /// <summary>Name of the database that Init.sql is loaded into and that every test DB is cloned from.</summary>
    private const string TemplateDatabase = "maroik_template";

    /// <summary>Repository-root-relative path of the seed script (schema, menu/category rows and the admin and demo accounts).</summary>
    private const string InitScriptRelativePath =
        "Maroik.DB/PostgreSQL/SQL_Init_Script/Debugging/Init.sql";

    /// <summary>Throwaway PostgreSQL 17 container whose initial database is the template.</summary>
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
        .WithDatabase(TemplateDatabase)
        .Build();

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        // The schema is full of `timestamp without time zone` columns. Npgsql 6+ maps those to
        // DateTimeKind.Unspecified and throws when handed an Utc/Local DateTime unless the legacy
        // switch is on. Set before the first connection is opened.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        await _container.StartAsync();

        // Init.sql is a `pg_dump` plain-format script: it contains psql meta-commands
        // (`\restrict`, `COPY ... FROM stdin` blocks terminated by `\.`) that only the psql
        // client understands, so it has to be piped through psql rather than executed over a
        // Npgsql connection. ExecScriptAsync copies the script into the container and runs it
        // with psql against the container's default database (TemplateDatabase).
        //
        // `\restrict` / `\unrestrict` are psql 17.5+ client guards around the dump; they are not
        // needed to replay it and are stripped so the load does not depend on the client version
        // bundled in the Postgres image.
        string[] lines = await File.ReadAllLinesAsync(ResolveInitScriptPath());
        string initSql = string.Join(
            '\n',
            lines.Where(l => !l.StartsWith("\\restrict ", StringComparison.Ordinal)
                          && !l.StartsWith("\\unrestrict ", StringComparison.Ordinal)));

        var result = await _container.ExecScriptAsync(initSql);
        if (result.ExitCode != 0L)
        {
            throw new InvalidOperationException(
                $"Loading Init.sql into '{TemplateDatabase}' failed (psql exit code {result.ExitCode}).\n" +
                $"stderr:\n{result.Stderr}\nstdout:\n{result.Stdout}");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// Creates a fresh database cloned from the seeded template and returns its name plus a
    /// connection string pointing at it. Pooling is disabled on the returned string so the caller
    /// can <see cref="DropDatabaseAsync"/> it cleanly afterward.
    /// </summary>
    public async Task<(string Database, string ConnectionString)> CloneTemplateAsync()
    {
        string database = "test_" + Guid.NewGuid().ToString("N");

        await using var admin = new NpgsqlConnection(ConnectionStringFor("postgres"));
        await admin.OpenAsync();
        await using var cmd = admin.CreateCommand();
        // TEMPLATE copy is a physical file copy of the seeded DB — a few hundred ms,
        // far cheaper than replaying Init.sql per class.
        cmd.CommandText = $"CREATE DATABASE \"{database}\" TEMPLATE \"{TemplateDatabase}\";";
        await cmd.ExecuteNonQueryAsync();

        return (database, ConnectionStringFor(database));
    }

    /// <summary>Drops a database previously produced by <see cref="CloneTemplateAsync"/>.</summary>
    public async Task DropDatabaseAsync(string database)
    {
        await using var admin = new NpgsqlConnection(ConnectionStringFor("postgres"));
        await admin.OpenAsync();
        await using var cmd = admin.CreateCommand();
        // WITH (FORCE) terminates any connection still attached (there should be none — test
        // contexts use Pooling=false and are disposed first) so the drop cannot hang.
        cmd.CommandText = $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE);";
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Connection string for <paramref name="database"/> in the container, with pooling off so connections close for real.</summary>
    private string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = database,
            Pooling = false,
        }.ConnectionString;

    /// <summary>Absolute path of the seed script, found by walking up to the directory holding <c>Maroik.sln</c>.</summary>
    private static string ResolveInitScriptPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Maroik.sln")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException(
                "Could not locate the repository root (Maroik.sln) from " + AppContext.BaseDirectory);

        string path = Path.Combine(
            dir.FullName, InitScriptRelativePath.Replace('/', Path.DirectorySeparatorChar));

        return !File.Exists(path) ? throw new FileNotFoundException($"Seed script not found at expected location.", path) : path;

    }
}

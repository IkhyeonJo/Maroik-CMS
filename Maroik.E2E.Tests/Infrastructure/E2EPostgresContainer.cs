using Testcontainers.PostgreSql;

namespace Maroik.E2E.Tests.Infrastructure;

/// <summary>
/// One PostgreSQL 17 container for the whole E2E session.
/// <para>
/// On <see cref="StartAsync"/> it boots a <c>postgres:17</c> container and loads the real
/// production schema <b>and its seed data</b> from
/// <c>Maroik.DB/PostgreSQL/SQL_Init_Script/Debugging/Init.sql</c> — the same script the
/// repository integration tests use — so the browser tests hit the real CHECK/FK constraints,
/// column defaults and the seeded navigation menu (anonymous <c>Category</c> / <c>SubCategory</c>
/// rows the <c>AuthorizationFilter</c> needs) instead of the EF Core InMemory provider, which
/// modeled none of that.
/// </para>
/// <para>
/// The E2E collection already runs sequentially against a single shared server, so one database
/// (no per-class template clone) is enough here.
/// </para>
/// </summary>
public sealed class E2EPostgresContainer : IAsyncDisposable
{
    /// <summary>Repository-root-relative path of the seed script (schema, menu/category rows and the admin and demo accounts).</summary>
    private const string InitScriptRelativePath =
        "Maroik.DB/PostgreSQL/SQL_Init_Script/Debugging/Init.sql";

    /// <summary>Throwaway PostgreSQL 17 container holding the <c>maroik</c> database.</summary>
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("maroik")
        .Build();

    /// <summary>Npgsql connection string to the seeded database. Empty until <see cref="StartAsync"/> completes.</summary>
    public string ConnectionString { get; private set; } = "";

    /// <summary>Boots the container and replays <c>Init.sql</c> into it.</summary>
    public async Task StartAsync()
    {
        // The schema is full of `timestamp without time zone` columns. Npgsql 6+ maps those to
        // DateTimeKind.Unspecified and throws when handed an Utc/Local DateTime unless the legacy
        // switch is on. Set before the first connection is opened. (Program.cs also sets it, but
        // this guarantees it is on even if the container is queried before the host is built.)
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        await _container.StartAsync();

        // Init.sql is a `pg_dump` plain-format script full of psql meta-commands
        // (`COPY ... FROM stdin` blocks, `\restrict` / `\unrestrict` client guards) that only the
        // psql client understands, so it is piped through psql via ExecScriptAsync rather than run
        // over a Npgsql connection. `\restrict` / `\unrestrict` are psql 17.5+ only and are not
        // needed to replay the dump, so they are stripped to stay independent of the client
        // version bundled in the Postgres image.
        string[] lines = await File.ReadAllLinesAsync(ResolveInitScriptPath());
        string initSql = string.Join(
            '\n',
            lines.Where(l => !l.StartsWith("\\restrict ", StringComparison.Ordinal)
                          && !l.StartsWith("\\unrestrict ", StringComparison.Ordinal)));

        var result = await _container.ExecScriptAsync(initSql);
        if (result.ExitCode != 0L)
        {
            throw new InvalidOperationException(
                $"Loading Init.sql into the E2E database failed (psql exit code {result.ExitCode}).\n" +
                $"stderr:\n{result.Stderr}\nstdout:\n{result.Stdout}");
        }

        ConnectionString = _container.GetConnectionString();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

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

        return !File.Exists(path) ? throw new FileNotFoundException("Seed script not found at expected location.", path) : path;
    }
}

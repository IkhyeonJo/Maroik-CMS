using Npgsql;
using Testcontainers.PostgreSql;
// ReSharper disable ClassNeverInstantiated.Global

namespace Maroik.Core.PostgreSQL.Tests.Infrastructure;

/// <summary>
/// A throwaway PostgreSQL 17 container with the repository's real init script loaded (schema and seed data), so the
/// model tests catch any drift between it and the EF model.
/// </summary>
public abstract class SchemaDatabaseFixture(string initScriptRelativePath) : IAsyncLifetime
{
    /// <summary>Throwaway PostgreSQL 17 container the init script is loaded into.</summary>
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").WithDatabase("maroik").Build();

    /// <summary>Connection string of the loaded database.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        // The schema is full of `timestamp without time zone` columns: Npgsql maps them to DateTimeKind.Unspecified unless the legacy switch is on.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        await _container.StartAsync();

        // Init.sql is a pg_dump plain-format script (psql meta-commands, COPY ... FROM stdin), so it is replayed through psql. The
        // `\restrict` guards are psql-17.5+ dump wrappers that are not needed to replay it and would tie the load to the client version.
        string[] lines = await File.ReadAllLinesAsync(Path.Combine(FindRepositoryRoot(), initScriptRelativePath));
        string script = string.Join('\n', lines.Where(l => !l.StartsWith("\\restrict ", StringComparison.Ordinal)
                                                        && !l.StartsWith("\\unrestrict ", StringComparison.Ordinal)));
        var result = await _container.ExecScriptAsync(script);
        if (result.ExitCode != 0L)
            throw new InvalidOperationException($"Loading '{initScriptRelativePath}' failed (psql exit code {result.ExitCode}).\n{result.Stderr}");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
        GC.SuppressFinalize(this);
    }
    
    /// <summary>Opens a new connection to the loaded database.</summary>
    public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    /// <summary>Walks up from the test binary's folder to the directory holding <c>Maroik.sln</c>.</summary>
    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Maroik.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate the repository root (Maroik.sln) from " + AppContext.BaseDirectory);
    }
}

/// <summary>The debugging init script (schema, the menu/category rows the site needs, and the admin and demo accounts).</summary>
public sealed class DebuggingSchemaFixture() : SchemaDatabaseFixture("Maroik.DB/PostgreSQL/SQL_Init_Script/Debugging/Init.sql");

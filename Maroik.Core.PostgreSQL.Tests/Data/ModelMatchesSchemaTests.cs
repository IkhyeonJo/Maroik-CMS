using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Core.PostgreSQL.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace Maroik.Core.PostgreSQL.Tests.Data;

/// <summary>
/// The hand-maintained EF Core model (<see cref="ApplicationDbContext"/>) against the real database schema created by the repository's
/// init script: every mapped table and column exists with a compatible type and no NULL the model cannot read, the key columns are the tables'
/// primary keys, every mapped relationship has its foreign key, and no table or required column is left unmapped (an unmapped NOT NULL
/// column without a default would make every insert fail). Run against the init script by the concrete class below.
/// </summary>
public abstract class ModelMatchesSchemaTests(SchemaDatabaseFixture database)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>One column of the live schema as read from <c>information_schema.columns</c>.</summary>
    private sealed record Column(string Table, string Name, bool Nullable, bool HasDefault, string DataType);

    /// <summary>A context over the fixture's database (only its model is inspected).</summary>
    private ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options);

    /// <summary>Reads every column of every base table in the <c>public</c> schema; identity and generated columns count as having a default.</summary>
    private async Task<List<Column>> ReadColumnsAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT c.table_name, c.column_name, c.is_nullable = 'YES', c.column_default IS NOT NULL OR c.is_identity = 'YES' OR c.is_generated <> 'NEVER', c.data_type
            FROM information_schema.columns c
            JOIN information_schema.tables t ON t.table_schema = c.table_schema AND t.table_name = c.table_name AND t.table_type = 'BASE TABLE'
            WHERE c.table_schema = 'public'
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        List<Column> columns = [];
        while (await reader.ReadAsync(Ct))
            columns.Add(new Column(reader.GetString(0), reader.GetString(1), reader.GetBoolean(2), reader.GetBoolean(3), reader.GetString(4)));
        return columns;
    }

    /// <summary>Normalizes an EF Core column type to the <c>information_schema</c> <c>data_type</c> spelling (length/precision dropped, aliases expanded).</summary>
    private static string BaseType(string relationalType)
    {
        string type = relationalType.ToLowerInvariant();
        int paren = type.IndexOf('(');
        if (paren >= 0) type = type[..paren].TrimEnd();
        return type switch
        {
            "int" or "int4" => "integer",
            "int8" => "bigint",
            "int2" => "smallint",
            "bool" => "boolean",
            "timestamp" => "timestamp without time zone",
            "timestamptz" => "timestamp with time zone",
            "varchar" => "character varying",
            "bpchar" => "character",
            "float8" => "double precision",
            "float4" => "real",
            _ => type,
        };
    }

    /// <summary>The model maps exactly the tables of the schema — none missing, none left over.</summary>
    [Fact]
    public async Task EveryTable_IsMapped_AndEveryMappedTable_Exists()
    {
        await using ApplicationDbContext db = CreateContext();
        var columns = await ReadColumnsAsync();

        string[] tables = [.. columns.Select(c => c.Table).Distinct().Order()];
        string[] mapped = [.. db.Model.GetEntityTypes().Select(e => e.GetTableName()!).Order()];

        Assert.Equal(tables, mapped);
    }

    /// <summary>Every mapped property has a column of that name, with a compatible base type, and the model never forbids a NULL the column can hold.</summary>
    [Fact]
    public async Task EveryMappedProperty_HasAMatchingColumn()
    {
        await using ApplicationDbContext db = CreateContext();
        var columns = (await ReadColumnsAsync()).ToDictionary(c => (c.Table, c.Name));

        List<string> problems = [];
        foreach (IEntityType entity in db.Model.GetEntityTypes())
        {
            string table = entity.GetTableName()!;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            foreach (IProperty property in entity.GetProperties())
            {
                string name = property.GetColumnName(store)!;
                if (!columns.TryGetValue((table, name), out Column? column)) { problems.Add($"{table}.{name}: no such column"); continue; }
                // (a model that is looser than the database — e.g. a scaffolded `string` without nullable annotations — is harmless: the
                // database still refuses a null. The dangerous direction is a model that forbids null where the database stores it: reading
                // such a row throws.)
                if (!property.IsNullable && column.Nullable) problems.Add($"{table}.{name}: model forbids null, database column is nullable");
                string modelType = BaseType(property.GetColumnType());
                if (modelType != column.DataType) problems.Add($"{table}.{name}: model type '{modelType}', database type '{column.DataType}'");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// Every <c>numeric</c> column has the precision and scale the model declares, so values the
    /// domain accepts are stored exactly as validated (the database would otherwise round them).
    /// </summary>
    [Fact]
    public async Task EveryNumericColumn_HasTheModelsPrecisionAndScale()
    {
        await using ApplicationDbContext db = CreateContext();
        await using NpgsqlConnection connection = await database.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT c.table_name, c.column_name, c.numeric_precision, c.numeric_scale
            FROM information_schema.columns c
            WHERE c.table_schema = 'public' AND c.data_type = 'numeric'
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Dictionary<(string, string), (int?, int?)> schema = [];
        while (await reader.ReadAsync(Ct))
            schema[(reader.GetString(0), reader.GetString(1))] =
                (reader.IsDBNull(2) ? null : reader.GetInt32(2), reader.IsDBNull(3) ? null : reader.GetInt32(3));

        Dictionary<(string, string), (int?, int?)> model = [];
        foreach (IEntityType entity in db.Model.GetEntityTypes())
        {
            string table = entity.GetTableName()!;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            foreach (IProperty property in entity.GetProperties().Where(p => BaseType(p.GetColumnType()) == "numeric"))
                model[(table, property.GetColumnName(store)!)] = (property.GetPrecision(), property.GetScale());
        }

        Assert.NotEmpty(schema);
        Assert.Equal(schema.OrderBy(e => e.Key), model.OrderBy(e => e.Key));
    }

    /// <summary>
    /// Every column of every table is either mapped or safe to leave out (nullable, or filled by a default) — an unmapped NOT NULL
    /// column without a default would make every insert through the model fail.
    /// </summary>
    [Fact]
    public async Task EveryUnmappedColumn_IsNullableOrHasADefault()
    {
        await using ApplicationDbContext db = CreateContext();
        var columns = await ReadColumnsAsync();
        HashSet<(string, string)> mapped = [.. db.Model.GetEntityTypes().SelectMany(e =>
        {
            var store = StoreObjectIdentifier.Table(e.GetTableName()!, e.GetSchema());
            return e.GetProperties().Select(p => (e.GetTableName()!, p.GetColumnName(store)!));
        })];

        var risky = columns.Where(c => !mapped.Contains((c.Table, c.Name)) && c is { Nullable: false, HasDefault: false })
            .Select(c => $"{c.Table}.{c.Name}").ToList();

        Assert.True(risky.Count == 0, "unmapped NOT NULL columns without a default: " + string.Join(", ", risky));
    }

    /// <summary>The model's key columns are the primary key of the table, in the same order.</summary>
    [Fact]
    public async Task EveryPrimaryKey_MatchesTheDatabase()
    {
        await using ApplicationDbContext db = CreateContext();
        await using NpgsqlConnection connection = await database.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT tc.table_name, kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu ON kcu.constraint_name = tc.constraint_name AND kcu.table_schema = tc.table_schema
            WHERE tc.table_schema = 'public' AND tc.constraint_type = 'PRIMARY KEY'
            ORDER BY tc.table_name, kcu.ordinal_position
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Dictionary<string, List<string>> real = [];
        while (await reader.ReadAsync(Ct))
        {
            string table = reader.GetString(0);
            if (!real.TryGetValue(table, out var list)) real[table] = list = [];
            list.Add(reader.GetString(1));
        }

        foreach (IEntityType entity in db.Model.GetEntityTypes())
        {
            string table = entity.GetTableName()!;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            string[] modelKey = [.. entity.FindPrimaryKey()!.Properties.Select(p => p.GetColumnName(store)!)];
            Assert.True(real.TryGetValue(table, out var actual), $"{table} has no primary key in the database");
            Assert.Equal(actual.Order(), modelKey.Order());
        }
    }

    /// <summary>Every relationship in the model has its foreign-key constraint (same table, columns and referenced table) in the database.</summary>
    [Fact]
    public async Task EveryModelRelationship_HasAForeignKeyInTheDatabase()
    {
        await using ApplicationDbContext db = CreateContext();
        await using NpgsqlConnection connection = await database.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT con.conrelid::regclass::text, con.confrelid::regclass::text,
                   (SELECT string_agg(a.attname, ',' ORDER BY k.ord) FROM unnest(con.conkey) WITH ORDINALITY k(attnum, ord) JOIN pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = k.attnum)
            FROM pg_constraint con
            WHERE con.contype = 'f' AND con.connamespace = 'public'::regnamespace
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        HashSet<string> real = [];
        while (await reader.ReadAsync(Ct))
            real.Add($"{reader.GetString(0).Trim('"')}({reader.GetString(2)})->{reader.GetString(1).Trim('"')}");

        List<string> missing =
        [
            .. from fk in db.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()) let store = StoreObjectIdentifier.Table(fk.DeclaringEntityType.GetTableName()!, fk.DeclaringEntityType.GetSchema()) let columns = string.Join(',', fk.Properties.Select(p => p.GetColumnName(store)!)) select $"{fk.DeclaringEntityType.GetTableName()}({columns})->{fk.PrincipalEntityType.GetTableName()}" into key where !real.Contains(key) select key

        ];

        Assert.True(missing.Count == 0, "foreign keys in the model but not in the database: " + string.Join("; ", missing));
    }

    /// <summary>
    /// Every foreign-key constraint of the database is a relationship of the model (same table, columns and referenced table): a
    /// constraint added to the init scripts without its navigation would leave the model unaware of it.
    /// </summary>
    [Fact]
    public async Task EveryDatabaseForeignKey_IsAModelRelationship()
    {
        await using ApplicationDbContext db = CreateContext();
        await using NpgsqlConnection connection = await database.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT con.conrelid::regclass::text, con.confrelid::regclass::text,
                   (SELECT string_agg(a.attname, ',' ORDER BY k.ord) FROM unnest(con.conkey) WITH ORDINALITY k(attnum, ord) JOIN pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = k.attnum)
            FROM pg_constraint con
            WHERE con.contype = 'f' AND con.connamespace = 'public'::regnamespace
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        List<string> real = [];
        while (await reader.ReadAsync(Ct))
            real.Add($"{reader.GetString(0).Trim('"')}({reader.GetString(2)})->{reader.GetString(1).Trim('"')}");

        HashSet<string> modelled =
        [
            .. from fk in db.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys())
               let store = StoreObjectIdentifier.Table(fk.DeclaringEntityType.GetTableName()!, fk.DeclaringEntityType.GetSchema())
               select $"{fk.DeclaringEntityType.GetTableName()}({string.Join(',', fk.Properties.Select(p => p.GetColumnName(store)!))})->{fk.PrincipalEntityType.GetTableName()}"
        ];
        List<string> missing = [.. real.Where(key => !modelled.Contains(key))];

        Assert.True(missing.Count == 0, "foreign keys in the database but not in the model: " + string.Join("; ", missing));
    }

    /// <summary>
    /// A post's and a comment's <c>Writer</c> is the author's <c>Account.Nickname</c>, enforced by a foreign key that follows a nickname
    /// change (<c>ON UPDATE CASCADE</c>) — so a later account that takes the old nickname does not inherit the posts. The model maps the
    /// same relationship onto the nickname (an alternate key) under the same constraint names.
    /// </summary>
    [Theory]
    [InlineData("Board", "Board_fk_0", typeof(Board))]
    [InlineData("BoardComment", "BoardComment_fk_1", typeof(BoardComment))]
    public async Task TheWriter_ReferencesTheAccountNickname_OnUpdateCascade(string table, string constraint, Type entity)
    {
        await using NpgsqlConnection connection = await database.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT con.conrelid::regclass::text, con.confrelid::regclass::text, con.confupdtype::text, con.confdeltype::text,
                   (SELECT a.attname FROM pg_attribute a WHERE a.attrelid = con.conrelid AND a.attnum = con.conkey[1]),
                   (SELECT a.attname FROM pg_attribute a WHERE a.attrelid = con.confrelid AND a.attnum = con.confkey[1])
            FROM pg_constraint con
            WHERE con.contype = 'f' AND con.conname = @name AND array_length(con.conkey, 1) = 1
            """, connection);
        command.Parameters.AddWithValue("name", constraint);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct), $"no single-column foreign key named {constraint}");
        Assert.Equal(table, reader.GetString(0).Trim('"'));
        Assert.Equal("Account", reader.GetString(1).Trim('"'));
        Assert.Equal("c", reader.GetString(2)); // ON UPDATE CASCADE
        Assert.Equal("a", reader.GetString(3)); // ON DELETE NO ACTION (accounts are soft-deleted)
        Assert.Equal("Writer", reader.GetString(4));
        Assert.Equal("Nickname", reader.GetString(5));

        await using ApplicationDbContext db = CreateContext();
        IForeignKey fk = Assert.Single(db.Model.FindEntityType(entity)!.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(Account));
        Assert.Equal(["Writer"], fk.Properties.Select(p => p.Name));
        Assert.Equal(["Nickname"], fk.PrincipalKey.Properties.Select(p => p.Name));
        Assert.Equal(constraint, fk.GetConstraintName());
    }

    /// <summary>Every unique index the model declares exists in the database (a duplicate would otherwise pass the model and fail at runtime).</summary>
    [Fact]
    public async Task EveryUniqueIndexInTheModel_ExistsInTheDatabase()
    {
        await using ApplicationDbContext db = CreateContext();
        await using NpgsqlConnection connection = await database.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT tablename, indexname FROM pg_indexes WHERE schemaname = 'public'", connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        HashSet<(string, string)> real = [];
        while (await reader.ReadAsync(Ct)) real.Add((reader.GetString(0), reader.GetString(1)));

        var missing = db.Model.GetEntityTypes()
            .SelectMany(e => e.GetIndexes().Where(i => i.IsUnique && i.GetDatabaseName() != null).Select(i => (Table: e.GetTableName()!, Index: i.GetDatabaseName()!)))
            .Where(i => !real.Contains(i)).ToList();

        Assert.True(missing.Count == 0, "unique indexes in the model but not in the database: " + string.Join(", ", missing.Select(m => $"{m.Table}.{m.Index}")));
    }

    /// <summary>The model can be queried end to end: selecting every mapped column of every table works (no missing / misspelled column).</summary>
    [Fact]
    public async Task EveryEntity_CanBeQueried()
    {
        await using ApplicationDbContext db = CreateContext();

        await db.Accounts.AsNoTracking().Take(1).ToListAsync(Ct);
        await db.Assets.AsNoTracking().Take(1).ToListAsync(Ct);
        await db.Boards.AsNoTracking().Take(1).ToListAsync(Ct);
        await db.BoardAttachedFiles.AsNoTracking().Take(1).ToListAsync(Ct);
        await db.BoardComments.AsNoTracking().Take(1).ToListAsync(Ct);
        await db.Calendars.AsNoTracking().Take(1).ToListAsync(Ct);
        await db.CalendarEvents.AsNoTracking().Take(1).ToListAsync(Ct);
        await db.CalendarEventAttachedFiles.AsNoTracking().Take(1).ToListAsync(Ct);
        await db.CalendarEventReminders.AsNoTracking().Take(1).ToListAsync(Ct);
        await db.CalendarRecurrences.AsNoTracking().Take(1).ToListAsync(Ct);
        await db.CalendarShareds.AsNoTracking().Take(1).ToListAsync(Ct);
        await db.Categories.AsNoTracking().Take(1).ToListAsync(Ct);
        await db.Expenditures.AsNoTracking().Take(1).ToListAsync(Ct);
        await db.FixedExpenditures.AsNoTracking().Take(1).ToListAsync(Ct);
        await db.FixedIncomes.AsNoTracking().Take(1).ToListAsync(Ct);
        await db.Incomes.AsNoTracking().Take(1).ToListAsync(Ct);
        await db.OtherCalendars.AsNoTracking().Take(1).ToListAsync(Ct);
        await db.SubCategories.AsNoTracking().Take(1).ToListAsync(Ct);
    }

    /// <summary>
    /// <c>OtherCalendar</c> is a pure two-column composite-PK join table (<c>AccountEmail</c>, <c>CalendarId</c>),
    /// each column itself a foreign key (to <c>Account</c> and <c>Calendar</c>). Scaffolding that shape from the
    /// database on its own does not produce an <c>OtherCalendar</c> CLR type or <c>DbSet</c> at all — EF Core's
    /// scaffolder collapses it into an implicit skip-navigation many-to-many directly between <c>Account</c> and
    /// <c>Calendar</c> (a <c>Dictionary&lt;string, object&gt;</c>-backed shared-type join entity, confirmed by
    /// scaffolding this schema fresh). The hand-edit in <c>OnModelCreating</c> instead keeps it promoted to an
    /// explicit entity with two ordinary one-to-many relationships (<c>Account.OtherCalendars</c> and
    /// <c>Calendar.OtherCalendars</c>, each a real <c>HasOne(...).WithMany(...)</c> against its own foreign key) —
    /// its own POCO, <c>DbSet&lt;OtherCalendar&gt;</c> and repository, so it can be an <c>AggregateRoot</c> with
    /// its own identity. Re-running the scaffolder over this model would silently collapse that promotion back
    /// into the implicit join table and its single many-to-many relationship; this pins both the explicit entity
    /// and its two relationships so such a regression fails here instead of surfacing as a missing
    /// repository/DbSet or a broken navigation elsewhere.
    /// </summary>
    [Fact]
    public void OtherCalendar_IsAnExplicitEntity_WithItsOwnTwoRelationships()
    {
        using ApplicationDbContext db = CreateContext();

        IEntityType? otherCalendar = db.Model.FindEntityType(typeof(OtherCalendar));
        Assert.NotNull(otherCalendar);
        Assert.False(otherCalendar.HasSharedClrType, "OtherCalendar must be its own CLR entity, not the scaffolder's default Dictionary<string, object> join entity");

        IEntityType account = db.Model.FindEntityType(typeof(Account))!;
        IEntityType calendar = db.Model.FindEntityType(typeof(Calendar))!;
        Assert.Empty(account.GetSkipNavigations());
        Assert.Empty(calendar.GetSkipNavigations());

        // OtherCalendar -> Account (many-to-one, via the AccountEmail FK).
        INavigation toAccount = otherCalendar.FindNavigation(nameof(OtherCalendar.Account))!;
        Assert.NotNull(toAccount);
        Assert.False(toAccount.IsCollection);
        Assert.Equal(typeof(Account), toAccount.TargetEntityType.ClrType);

        // OtherCalendar -> Calendar (many-to-one, via the CalendarId FK).
        INavigation toCalendar = otherCalendar.FindNavigation(nameof(OtherCalendar.Calendar))!;
        Assert.NotNull(toCalendar);
        Assert.False(toCalendar.IsCollection);
        Assert.Equal(typeof(Calendar), toCalendar.TargetEntityType.ClrType);

        // Account -> OtherCalendar (one-to-many, the inverse of the AccountEmail FK).
        INavigation accountOtherCalendars = account.FindNavigation(nameof(Account.OtherCalendars))!;
        Assert.NotNull(accountOtherCalendars);
        Assert.True(accountOtherCalendars.IsCollection);
        Assert.Equal(typeof(OtherCalendar), accountOtherCalendars.TargetEntityType.ClrType);

        // Calendar -> OtherCalendar (one-to-many, the inverse of the CalendarId FK).
        INavigation calendarOtherCalendars = calendar.FindNavigation(nameof(Calendar.OtherCalendars))!;
        Assert.NotNull(calendarOtherCalendars);
        Assert.True(calendarOtherCalendars.IsCollection);
        Assert.Equal(typeof(OtherCalendar), calendarOtherCalendars.TargetEntityType.ClrType);
    }

    /// <summary>The context exposes a <c>DbSet</c> for every entity of the model.</summary>
    [Fact]
    public void EveryEntity_HasADbSet()
    {
        using ApplicationDbContext db = CreateContext();
        Type[] withDbSet = [.. typeof(ApplicationDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0]).OrderBy(t => t.Name)];
        Type[] entities = [.. db.Model.GetEntityTypes().Select(e => e.ClrType).OrderBy(t => t.Name)];

        Assert.Equal(entities, withDbSet);
    }
}

/// <summary>The model against the debugging init script.</summary>
public sealed class ModelMatchesDebuggingSchemaTests(DebuggingSchemaFixture database)
    : ModelMatchesSchemaTests(database), IClassFixture<DebuggingSchemaFixture>;

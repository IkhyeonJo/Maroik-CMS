// ReSharper disable ClassNeverInstantiated.Global
namespace Maroik.Core.Repository.Tests.Infrastructure;

/// <summary>
/// Per-test-class database. Clones the seeded template in <see cref="InitializeAsync"/> and drops
/// it in <see cref="DisposeAsync"/>, so every test class starts from an identical, fully-seeded
/// copy of the real schema and no class can see another's writes.
/// <para>
/// All test methods within one class share this database (xUnit creates the class fixture once per
/// class), so each test must scope the rows it inserts to keys unique to itself — see
/// <see cref="RepositoryTestBase.UniqueEmail"/> / <see cref="RepositoryTestBase.Unique"/>.
/// </para>
/// </summary>
public sealed class DatabaseFixture(PostgresContainerFixture container) : IAsyncLifetime
{
    private string _database = "";

    /// <summary>Connection string to this class's freshly cloned, seeded database.</summary>
    public string ConnectionString { get; private set; } = "";

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
        => (_database, ConnectionString) = await container.CloneTemplateAsync();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
        => await container.DropDatabaseAsync(_database);
}

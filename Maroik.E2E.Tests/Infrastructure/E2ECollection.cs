namespace Maroik.E2E.Tests.Infrastructure;

/// <summary>
/// xUnit collection definition named "E2E". Classes decorated with <c>[Collection("E2E")]</c>
/// share a single <see cref="E2ESharedFixture"/> instance (one Kestrel server, one browser)
/// and run sequentially rather than in parallel, since they all hit the same server/database.
/// </summary>
[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<E2ESharedFixture>;

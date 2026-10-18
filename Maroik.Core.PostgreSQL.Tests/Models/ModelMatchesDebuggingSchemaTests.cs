using Maroik.Core.PostgreSQL.Tests.Fixtures;

namespace Maroik.Core.PostgreSQL.Tests.Models;

/// <summary>The model against the debugging init script.</summary>
public sealed class ModelMatchesDebuggingSchemaTests(DebuggingSchemaFixture database)
    : ModelMatchesSchemaTests(database), IClassFixture<DebuggingSchemaFixture>;

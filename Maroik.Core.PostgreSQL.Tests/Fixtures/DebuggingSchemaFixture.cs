
namespace Maroik.Core.PostgreSQL.Tests.Fixtures;

/// <summary>The debugging init script (schema, the menu/category rows the site needs, and the admin and demo accounts).</summary>
public sealed class DebuggingSchemaFixture() : SchemaDatabaseFixture("Maroik.DB/PostgreSQL/SQL_Init_Script/Debugging/Init.sql");

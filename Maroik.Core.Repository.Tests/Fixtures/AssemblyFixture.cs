// One PostgreSQL container for the entire assembly (xUnit v3 assembly fixture). It is
// constructor-injected into DatabaseFixture, which hands each test class its own cloned database.
//
// Test-collection parallelism is switched off in xunit.runner.json: every test class talks to the
// same shared database server, and running them in parallel would race on
// CREATE DATABASE ... TEMPLATE / DROP DATABASE.
[assembly: AssemblyFixture(typeof(PostgresContainerFixture))]

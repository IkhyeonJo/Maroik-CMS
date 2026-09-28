namespace Maroik.Website.Tests.Infrastructure;

/// <summary>
/// Shares a single <see cref="MaroikWebApplicationFactory"/> instance across all
/// controller integration-test classes.  A shared factory means Program.cs runs
/// once per test session, which avoids the Serilog bootstrap-logger freeze race
/// that occurs when multiple factories start concurrently.
/// </summary>
[CollectionDefinition("Website Integration")]
public class WebsiteIntegrationCollection : ICollectionFixture<MaroikWebApplicationFactory>;

using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// Database-only tests (no WebApplicationFactory). Shares the same Testcontainers session
/// and Postgres/SQL Server instance as <see cref="EndatixIntegrationTestCollection" /> via reference counting.
/// </summary>
[CollectionDefinition(nameof(DbIntegrationTestCollection))]
public sealed class DbIntegrationTestCollection : ICollectionFixture<DbIntegrationFixture>
{
}

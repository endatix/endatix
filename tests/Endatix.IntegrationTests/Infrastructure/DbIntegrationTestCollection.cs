using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// Database-only tests (no WebApplicationFactory). Shares the Postgres/SQL Server container with
/// <see cref="EndatixIntegrationTestCollection" />, on a database of its own.
/// </summary>
[CollectionDefinition(nameof(DbIntegrationTestCollection))]
public sealed class DbIntegrationTestCollection : ICollectionFixture<DbIntegrationFixture>
{
}

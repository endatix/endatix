namespace Endatix.IntegrationTests;

[CollectionDefinition(nameof(EndatixIntegrationTestCollection))]
public sealed class EndatixIntegrationTestCollection : ICollectionFixture<EndatixIntegrationWebHostFixture>
{
}

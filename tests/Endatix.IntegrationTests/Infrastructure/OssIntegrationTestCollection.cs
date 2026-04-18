using Xunit;

namespace Endatix.IntegrationTests;

[CollectionDefinition(nameof(OssIntegrationTestCollection))]
public sealed class OssIntegrationTestCollection : ICollectionFixture<OssIntegrationWebHostFixture>
{
}

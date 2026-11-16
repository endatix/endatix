using System.Security.Cryptography;
using System.Text;
using Endatix.Infrastructure.Branding;
using FluentAssertions;

namespace Endatix.Infrastructure.Tests.Branding;

public sealed class BrandPackSchemaTests
{
    [Fact]
    public void Sha256_MatchesTheEmbeddedSchemaCopy()
    {
        // Arrange
        var lineFeedOnly = BrandPackSchema.ReadText().ReplaceLineEndings("\n");

        // Act
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(lineFeedOnly)));

        // Assert
        hash.Should().Be(BrandPackSchema.Sha256);
    }
}

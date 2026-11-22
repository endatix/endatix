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

    [Fact]
    public void Sha256_MatchesThePublishedDocsSchemaCopy()
    {
        // Arrange
        var path = Path.Combine(RepositoryRoot(), "docs", "endatix-docs", "static", "schemas", "brand", "1", "brand.schema.json");
        var lineFeedOnly = File.ReadAllText(path).ReplaceLineEndings("\n");

        // Act
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(lineFeedOnly)));

        // Assert
        hash.Should().Be(BrandPackSchema.Sha256);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Endatix.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Endatix.slnx not found above the test output.");
    }
}

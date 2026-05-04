using Endatix.Infrastructure.Caching;
using FluentAssertions;
using Xunit;

namespace Endatix.Infrastructure.Tests.Features.AccessControl;

public sealed class CacheKeyFingerprintTests
{
    [Fact]
    public void ComputeHmacSha256Hex_Produces64CharUppercaseHex()
    {
        // Arrange
        const string raw = "raw-jwt-material";
        const string key = "test-signing-key-32-characters";

        // Act
        var fingerprint = CacheKeyFingerprint.ComputeHmacSha256Hex(raw, key);

        // Assert
        fingerprint.Should().HaveLength(64);
        fingerprint.Should().MatchRegex("^[0-9A-F]{64}$");
    }

    [Fact]
    public void ComputeHmacSha256Hex_DoesNotEmbedRawToken()
    {
        // Arrange
        const string raw = "header.eyJzdWIiOiJzZWNyZXQifQ.signature-UNIQUE";

        // Act
        var fingerprint = CacheKeyFingerprint.ComputeHmacSha256Hex(raw, "test-signing-key-32-characters");

        // Assert
        fingerprint.Should().NotContain(raw);
        fingerprint.Should().NotContain("UNIQUE");
    }
}

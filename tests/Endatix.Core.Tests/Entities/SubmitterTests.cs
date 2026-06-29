using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public sealed class SubmitterTests
{
    [Fact]
    public void Refresh_WithMissingDisplayIdAndProfile_ClearsExistingValues()
    {
        // Arrange
        var submitter = Submitter.Create(
            tenantId: 1,
            authProvider: "Keycloak",
            externalSubjectId: "external-123",
            displayId: "panelist-123",
            appUserId: null,
            profileJson: """{"department":"sales"}""",
            lastSeenAt: DateTimeOffset.UtcNow);
        var refreshedAt = new DateTimeOffset(2026, 6, 11, 8, 4, 0, TimeSpan.FromHours(3));

        // Act
        submitter.Refresh(null, null, refreshedAt);

        // Assert
        submitter.DisplayId.Should().BeNull();
        submitter.ProfileJson.Should().BeNull();
        submitter.LastSeenAt.Should().Be(refreshedAt.UtcDateTime);
    }
}

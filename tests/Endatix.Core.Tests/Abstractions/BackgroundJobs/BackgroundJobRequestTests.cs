using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Core.Tests.Abstractions.BackgroundJobs;

public sealed class BackgroundJobRequestTests
{
    [Fact]
    public void Create_WithTypedPayload_SetsJobTypeAndPayloadJson()
    {
        // Arrange
        var payload = new TestPayload(9007199254740993);
        var tenantId = 5;

        // Act
        var request = BackgroundJobRequest.Create(payload, tenantId: tenantId);

        // Assert
        request.JobType.Should().Be("Test");
        request.TenantId.Should().Be(5);
        request.PayloadJson.Should().Be("{\"id\":9007199254740993}");
    }

    [Fact]
    public void Create_WithDedupKey_CarriesKeyAndOptionalFields()
    {
        // Arrange
        var payload = new TestPayload(1);
        var expiresAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var request = BackgroundJobRequest.Create(
            payload, tenantId: 7, createdByUserId: 11, expiresAt: expiresAt, dedupKey: "42:Test");

        // Assert
        request.Should().Be(new BackgroundJobRequest("Test", "{\"id\":1}", 7, 11, expiresAt, "42:Test"));
    }

    [Fact]
    public void Constructor_WithoutDedupKey_LeavesKeyNull()
    {
        // Arrange
        var jobType = "Untyped";

        // Act
        var request = new BackgroundJobRequest(jobType, "{}", 3);

        // Assert
        request.DedupKey.Should().BeNull();
    }
}

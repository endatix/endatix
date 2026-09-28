using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Core.Tests.Abstractions.BackgroundJobs;

public sealed class BackgroundJobPayloadSerializerTests
{
    [Fact]
    public void TryDeserialize_RoundTrip_ReturnsEqualPayload()
    {
        // Arrange
        var original = new TestPayload(9007199254740993);
        var json = BackgroundJobPayloadSerializer.Serialize(original);

        // Act
        var read = BackgroundJobPayloadSerializer.TryDeserialize<TestPayload>(json, out var payload);

        // Assert
        read.Should().BeTrue();
        payload.Should().Be(original);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{\"id\":\"not a number\"}")]
    public void TryDeserialize_UnreadableInput_ReturnsFalse(string json)
    {
        // Arrange — each case is text no retry could ever read.

        // Act
        var read = BackgroundJobPayloadSerializer.TryDeserialize<TestPayload>(json, out var payload);

        // Assert
        read.Should().BeFalse();
        payload.Should().BeNull();
    }

    [Fact]
    public void Serialize_LongId_WritesJsonNumber()
    {
        // Arrange
        var payload = new TestPayload(long.MaxValue);

        // Act
        var json = BackgroundJobPayloadSerializer.Serialize(payload);

        // Assert
        json.Should().Be($"{{\"id\":{long.MaxValue}}}");
    }
}

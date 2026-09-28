using System.Text.Json;
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
    [InlineData("{}")]
    [InlineData("{\"id\":null}")]
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

    [Fact]
    public void TryDeserialize_MissingParameterWithDefault_ReturnsPayloadWithDefault()
    {
        // Arrange — input written before the optional parameter existed.
        const string json = "{\"id\":7}";

        // Act
        var read = BackgroundJobPayloadSerializer.TryDeserialize<OptionalMemberPayload>(json, out var payload);

        // Assert
        read.Should().BeTrue();
        payload.Should().Be(new OptionalMemberPayload(7));
    }

    [Theory]
    [InlineData("{\"name\":null}")]
    [InlineData("{}")]
    public void TryDeserialize_NonNullableReferenceMissingOrNull_ReturnsFalse(string json)
    {
        // Arrange — a null here would reach the handler as a non-nullable string.

        // Act
        var read = BackgroundJobPayloadSerializer.TryDeserialize<NamedPayload>(json, out var payload);

        // Assert
        read.Should().BeFalse();
        payload.Should().BeNull();
    }

    [Fact]
    public void Serialize_NullInNonNullableReference_Throws()
    {
        // Arrange
        var payload = new NamedPayload(null!);

        // Act
        var act = () => BackgroundJobPayloadSerializer.Serialize(payload);

        // Assert
        act.Should().Throw<JsonException>();
    }

    private sealed record OptionalMemberPayload(long Id, bool Notify = false) : IBackgroundJobPayload
    {
        public static string JobType => "OptionalMember";
    }

    private sealed record NamedPayload(string Name) : IBackgroundJobPayload
    {
        public static string JobType => "Named";
    }
}

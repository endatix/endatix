using System.Text.Json;
using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Core.Tests.Abstractions.BackgroundJobs;

public sealed class BackgroundJobPayloadSerializerTests
{
    [Fact]
    public void Deserialize_RoundTrip_ReturnsEqualPayload()
    {
        // Arrange
        var original = new TestPayload(9007199254740993);
        var json = BackgroundJobPayloadSerializer.Serialize(original);

        // Act
        var payload = BackgroundJobPayloadSerializer.Deserialize<TestPayload>(json);

        // Assert
        payload.Should().Be(original);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{\"id\":\"not a number\"}")]
    [InlineData("{}")]
    [InlineData("{\"id\":null}")]
    public void Deserialize_UnreadableInput_ThrowsJsonException(string json)
    {
        // Arrange — each case is text no retry could ever read.

        // Act
        var act = () => BackgroundJobPayloadSerializer.Deserialize<TestPayload>(json);

        // Assert
        act.Should().Throw<JsonException>();
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
    public void Deserialize_MissingParameterWithDefault_ReturnsPayloadWithDefault()
    {
        // Arrange — input written before the optional parameter existed.
        const string json = "{\"id\":7}";

        // Act
        var payload = BackgroundJobPayloadSerializer.Deserialize<OptionalMemberPayload>(json);

        // Assert
        payload.Should().Be(new OptionalMemberPayload(7));
    }

    [Theory]
    [InlineData("{\"name\":null}")]
    [InlineData("{}")]
    public void Deserialize_NonNullableReferenceMissingOrNull_ThrowsJsonException(string json)
    {
        // Arrange — a null here would reach the handler as a non-nullable string.

        // Act
        var act = () => BackgroundJobPayloadSerializer.Deserialize<NamedPayload>(json);

        // Assert
        act.Should().Throw<JsonException>();
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

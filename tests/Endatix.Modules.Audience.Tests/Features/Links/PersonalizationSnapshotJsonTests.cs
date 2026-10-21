using System.Text.Json;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Features.Links;

namespace Endatix.Modules.Audience.Tests.Features.Links;

public sealed class PersonalizationSnapshotJsonTests
{
    [Fact]
    public void Write_FirstCedarGroveRow_TypesCityAndAge()
    {
        var json = PersonalizationSnapshotJson.Write(
            new SnapshotHeader(1, "CG-000001-01", new DateTime(2026, 10, 8, 8, 0, 0, DateTimeKind.Utc)),
            [
                Cell("city", AudienceDataTypeCodes.Text, "03. MIDWEST - COLUMBUS"),
                Cell("age", AudienceDataTypeCodes.Number, "65"),
                Cell("note", AudienceDataTypeCodes.Text, ""),
            ]);

        using var document = JsonDocument.Parse(json);
        var variables = document.RootElement.GetProperty("variables");
        variables.GetProperty("city").GetString().Should().Be("03. MIDWEST - COLUMBUS");
        variables.GetProperty("age").GetDouble().Should().Be(65);
        variables.TryGetProperty("note", out _).Should().BeFalse();
        document.RootElement.GetProperty("identifier").GetString().Should().Be("CG-000001-01");
        document.RootElement.GetProperty("source").GetString().Should().Be("link");
        document.RootElement.GetProperty("audienceLinkId").GetInt64().Should().Be(1);
    }

    [Fact]
    public void Write_OnBehalf_OmitsLinkAndKeepsMember()
    {
        var json = PersonalizationSnapshotJson.Write(
            new SnapshotHeader(
                null,
                "CG-000001-01",
                new DateTime(2026, 10, 8, 8, 0, 0, DateTimeKind.Utc),
                "on_behalf",
                42),
            [Cell("age", AudienceDataTypeCodes.Number, "65")]);

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("source").GetString().Should().Be("on_behalf");
        document.RootElement.GetProperty("memberId").GetInt64().Should().Be(42);
        document.RootElement.TryGetProperty("audienceLinkId", out _).Should().BeFalse();
        document.RootElement.GetProperty("variables").GetProperty("age").GetDouble().Should().Be(65);
    }

    [Fact]
    public void Write_Test_OmitsLinkAndKeepsMember()
    {
        var json = PersonalizationSnapshotJson.Write(
            new SnapshotHeader(null, "ada@example.com", DateTime.UnixEpoch, PersonalizationSources.Test, 42),
            [Cell("age", AudienceDataTypeCodes.Number, "65")]);

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("source").GetString().Should().Be("test");
        document.RootElement.GetProperty("memberId").GetInt64().Should().Be(42);
        document.RootElement.TryGetProperty("audienceLinkId", out _).Should().BeFalse();
    }

    [Fact]
    public void Write_BooleanAndChoice_AreTypedJson()
    {
        var json = PersonalizationSnapshotJson.Write(
            new SnapshotHeader(1, "CG-000001-01", DateTime.UnixEpoch),
            [
                Cell("consent", AudienceDataTypeCodes.Boolean, "true"),
                Cell("sites", AudienceDataTypeCodes.MultipleChoice, """["a","b"]"""),
            ]);

        using var document = JsonDocument.Parse(json);
        var variables = document.RootElement.GetProperty("variables");
        variables.GetProperty("consent").GetBoolean().Should().BeTrue();
        variables.GetProperty("sites").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void Write_MalformedChoice_IsKeptAsText()
    {
        var json = PersonalizationSnapshotJson.Write(
            new SnapshotHeader(1, "CG-000001-01", DateTime.UnixEpoch),
            [Cell("sites", AudienceDataTypeCodes.MultipleChoice, "not-json")]);

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("variables").GetProperty("sites").GetString().Should().Be("not-json");
    }

    [Fact]
    public void Write_KeepsEachPropertysIdNameAndTypeBesideItsValue()
    {
        // Act
        var json = PersonalizationSnapshotJson.Write(
            new SnapshotHeader(null, "CG-000001-01", DateTime.UnixEpoch, "on_behalf", 42),
            [Cell("age", AudienceDataTypeCodes.Number, "65"), Cell("note", AudienceDataTypeCodes.Text, "")]);

        // Assert
        using var document = JsonDocument.Parse(json);
        var value = document.RootElement.GetProperty("values").EnumerateArray().Should().ContainSingle().Subject;
        value.GetProperty("propertyId").GetInt64().Should().Be(PropertyIdOf("age"));
        value.GetProperty("variableName").GetString().Should().Be("age");
        value.GetProperty("name").GetString().Should().Be("Age");
        value.GetProperty("dataType").GetString().Should().Be(AudienceDataTypeCodes.Number);
        value.GetProperty("value").GetDouble().Should().Be(65);
    }

    [Theory]
    [InlineData(PersonalizationSources.Test, null)]
    [InlineData(PersonalizationSources.Link, 7L)]
    public void NamesParticipant_MatchesTheParticipantAWrittenSnapshotNames(string source, long? audienceLinkId)
    {
        // Arrange
        var json = PersonalizationSnapshotJson.Write(
            new SnapshotHeader(audienceLinkId, "ada@example.com", DateTime.UnixEpoch, source, 42),
            [Cell("age", AudienceDataTypeCodes.Number, "65")]);

        // Act
        var names = PersonalizationSnapshotJson.NamesParticipant(42).Compile();
        var namesOther = PersonalizationSnapshotJson.NamesParticipant(4).Compile();
        var namesLonger = PersonalizationSnapshotJson.NamesParticipant(420).Compile();

        // Assert
        names(json).Should().BeTrue();
        namesOther(json).Should().BeFalse();
        namesLonger(json).Should().BeFalse();
    }

    [Fact]
    public void NamesParticipant_DoesNotMatchASnapshotWithoutOne()
    {
        // Arrange
        var json = PersonalizationSnapshotJson.Write(
            new SnapshotHeader(42, "ada@example.com", DateTime.UnixEpoch),
            [Cell("age", AudienceDataTypeCodes.Number, "42")]);

        // Act
        var names = PersonalizationSnapshotJson.NamesParticipant(42).Compile();

        // Assert
        names(json).Should().BeFalse();
        names(null).Should().BeFalse();
    }

    [Fact]
    public void IsOnBehalf_MatchesOnlyAnOnBehalfWrittenSnapshot()
    {
        // Arrange
        var onBehalf = PersonalizationSnapshotJson.Write(
            new SnapshotHeader(null, "ada@example.com", DateTime.UnixEpoch, PersonalizationSources.OnBehalf, 42),
            [Cell("age", AudienceDataTypeCodes.Number, "65")]);
        var link = PersonalizationSnapshotJson.Write(
            new SnapshotHeader(7, "ada@example.com", DateTime.UnixEpoch, PersonalizationSources.Link, 42),
            [Cell("age", AudienceDataTypeCodes.Number, "65")]);
        var test = PersonalizationSnapshotJson.Write(
            new SnapshotHeader(null, "ada@example.com", DateTime.UnixEpoch, PersonalizationSources.Test, 42),
            [Cell("age", AudienceDataTypeCodes.Number, "65")]);

        // Act
        var isOnBehalf = PersonalizationSnapshotJson.IsOnBehalf().Compile();

        // Assert
        isOnBehalf(onBehalf).Should().BeTrue();
        isOnBehalf(link).Should().BeFalse();
        isOnBehalf(test).Should().BeFalse();
        isOnBehalf(null).Should().BeFalse();
    }

    /// <summary>A cell whose property is named after its variable ("age" → "Age"), with a stable id.</summary>
    private static SnapshotCell Cell(string variableName, string dataType, string value) =>
        new(PropertyIdOf(variableName), variableName, char.ToUpperInvariant(variableName[0]) + variableName[1..], dataType, value);

    private static long PropertyIdOf(string variableName) => 1000 + variableName.Length;
}

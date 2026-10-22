using System.Text.Json.Nodes;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Variables;

namespace Endatix.Modules.Audience.Tests.Features.Variables;

public sealed class PresetBuilderTests
{
    private static readonly Property City = PropertyOf(1, "City", AudienceDataTypeCodes.Text);
    private static readonly Property Seats = PropertyOf(2, "Seats", AudienceDataTypeCodes.Number);
    private static readonly Property Renews = PropertyOf(3, "Renews", AudienceDataTypeCodes.DateTime);
    private static readonly Property Sites =
        new(new PropertyCreateArgs(1, 10, "Sites", AudienceDataTypeCodes.MultipleChoice, 4, """["a","b"]""")) { Id = 4 };

    private static readonly IReadOnlyDictionary<(long PropertyId, bool IsTest), int> NothingFilled =
        new Dictionary<(long PropertyId, bool IsTest), int>();

    [Fact]
    public void Assemble_FormWithoutProperties_HasNoVariablesButCountsItsParticipants()
    {
        // Act
        AudienceVariablesDto variables = PresetBuilder.Assemble(
            new FormAudience(
                [],
                [new PresetSource("maria@acme.test", new Dictionary<long, string>())],
                new ParticipantCounts(1, 3),
                NothingFilled));

        // Assert
        variables.VariablePresets.Should().BeNull();
        variables.Coverage.Participants.Should().Be(new ParticipantCounts(1, 3));
        variables.Coverage.Properties.Should().BeEmpty();
        variables.Editing.Should().Be(new PresetEditing(false, false, false));
    }

    [Fact]
    public void Assemble_FormWithProperties_DefinesThemAndNamesOnePresetPerTestParticipantByIdentifier()
    {
        // Act
        AudienceVariablesDto variables = PresetBuilder.Assemble(new FormAudience(
            [City, Seats],
            [
                new PresetSource("zed@acme.test", new Dictionary<long, string> { [City.Id] = "Sofia" }),
                new PresetSource("Maria@acme.test", new Dictionary<long, string> { [Seats.Id] = "12" }),
                new PresetSource("ada@acme.test", new Dictionary<long, string>()),
            ],
            new ParticipantCounts(Test: 42, Real: 0),
            NothingFilled));

        // Assert
        VariablePresetsDto presets = variables.VariablePresets!;
        presets.Definition["elements"]!.AsArray().Select(question => question!["name"]!.GetValue<string>())
            .Should().Equal("city", "seats");
        presets.Presets.Select(preset => preset.Name)
            .Should().Equal("ada@acme.test", "Maria@acme.test", "zed@acme.test");
        presets.Presets[0].Variables.Should().BeEmpty();
        presets.TotalPresets.Should().Be(42);
    }

    [Fact]
    public void Assemble_CountsFilledTestAndRealParticipantsPerPropertyInTabOrder_ZeroWhenNoneFilled()
    {
        // Act
        AudienceVariablesDto variables = PresetBuilder.Assemble(new FormAudience(
            [City, Seats, Renews],
            [],
            new ParticipantCounts(Test: 3, Real: 4810),
            new Dictionary<(long PropertyId, bool IsTest), int>
            {
                [(City.Id, true)] = 3,
                [(City.Id, false)] = 4790,
                [(Seats.Id, false)] = 12,
            }));

        // Assert
        variables.Coverage.Participants.Should().Be(new ParticipantCounts(3, 4810));
        variables.Coverage.Properties.Should().Equal(
            new PropertyCoverage("city", FilledTest: 3, FilledReal: 4790),
            new PropertyCoverage("seats", FilledTest: 0, FilledReal: 12),
            new PropertyCoverage("renews", FilledTest: 0, FilledReal: 0));
    }

    [Fact]
    public void Assemble_IdentifiersThatDifferOnlyInCase_AreOrderedByCase()
    {
        // Act
        AudienceVariablesDto variables = PresetBuilder.Assemble(new FormAudience(
            [City],
            [
                new PresetSource("aB-1", new Dictionary<long, string>()),
                new PresetSource("Ab-1", new Dictionary<long, string>()),
            ],
            new ParticipantCounts(Test: 2, Real: 0),
            NothingFilled));

        // Assert
        variables.VariablePresets!.Presets.Select(preset => preset.Name).Should().Equal("Ab-1", "aB-1");
    }

    [Fact]
    public void Build_TypesEachLiveCellAsTheRespondentReadsIt()
    {
        // Act
        VariablePresetDto preset = PresetBuilder.Build(
            new PresetSource(
                "maria@acme.test",
                new Dictionary<long, string>
                {
                    [City.Id] = "Sofia",
                    [Seats.Id] = "12",
                    [Renews.Id] = "2026-10-21T09:30:00+03:00",
                    [Sites.Id] = """["a","b"]""",
                }),
            [City, Seats, Renews, Sites]);

        // Assert
        preset.Name.Should().Be("maria@acme.test");
        JsonNode.DeepEquals(
            preset.Variables,
            JsonNode.Parse("""{"city":"Sofia","seats":12,"renews":"2026-10-21T09:30:00+03:00","sites":["a","b"]}"""))
            .Should().BeTrue(preset.Variables.ToJsonString());
    }

    [Fact]
    public void Build_LeavesOutCellsOfOtherProperties()
    {
        // Act
        VariablePresetDto preset = PresetBuilder.Build(
            new PresetSource(
                "maria@acme.test",
                new Dictionary<long, string> { [Seats.Id] = "3", [99] = "orphan" }),
            [City, Seats]);

        // Assert
        JsonNode.DeepEquals(preset.Variables, JsonNode.Parse("""{"seats":3}""")).Should().BeTrue();
    }

    private static Property PropertyOf(long id, string name, string dataType) =>
        new(new PropertyCreateArgs(1, 10, name, dataType, (int)id)) { Id = id };
}

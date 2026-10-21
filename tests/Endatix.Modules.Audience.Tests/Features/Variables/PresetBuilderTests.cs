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

    [Fact]
    public void Assemble_FormWithoutProperties_HasNoVariablesAndNothingEditable()
    {
        // Act
        AudienceVariablesDto variables = PresetBuilder.Assemble(
            new FormAudience([], [new PresetSource("maria@acme.test", new Dictionary<long, string>())]));

        // Assert
        variables.VariablePresets.Should().BeNull();
        variables.Coverage.Participants.Should().Be(new ParticipantCounts(0, 0));
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
            ]));

        // Assert
        VariablePresetsDto presets = variables.VariablePresets!;
        presets.Definition["elements"]!.AsArray().Select(question => question!["name"]!.GetValue<string>())
            .Should().Equal("city", "seats");
        presets.Presets.Select(preset => preset.Name)
            .Should().Equal("ada@acme.test", "Maria@acme.test", "zed@acme.test");
        presets.Presets[0].Variables.Should().BeEmpty();
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
    public void Build_LeavesOutEmptyCellsAndCellsOfOtherProperties()
    {
        // Act
        VariablePresetDto preset = PresetBuilder.Build(
            new PresetSource(
                "maria@acme.test",
                new Dictionary<long, string> { [City.Id] = "", [Seats.Id] = "3", [99] = "orphan" }),
            [City, Seats]);

        // Assert
        JsonNode.DeepEquals(preset.Variables, JsonNode.Parse("""{"seats":3}""")).Should().BeTrue();
    }

    private static Property PropertyOf(long id, string name, string dataType) =>
        new(new PropertyCreateArgs(1, 10, name, dataType, (int)id)) { Id = id };
}

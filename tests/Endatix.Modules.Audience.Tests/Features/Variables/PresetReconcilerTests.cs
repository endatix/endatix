using System.Text.Json.Nodes;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Variables;

namespace Endatix.Modules.Audience.Tests.Features.Variables;

public sealed class PresetReconcilerTests
{
    private static readonly Property City = PropertyOf(1, "City", AudienceDataTypeCodes.Text);
    private static readonly Property Seats = PropertyOf(2, "Seats", AudienceDataTypeCodes.Number);
    private static readonly IReadOnlyList<Property> Properties = [City, Seats];

    [Fact]
    public void Plan_NewIdentifier_CreatesATestParticipantWithItsFilledValues()
    {
        // Act
        Result<IReadOnlyList<PresetWrite>> plan = PresetReconciler.Plan(
            Properties, NoneOnForm, [Preset("maria@acme.test", """{"city":"Sofia","unknown":1}""")]);

        // Assert
        PresetWrite write = plan.Value.Should().ContainSingle().Subject;
        write.MembershipId.Should().BeNull();
        write.Identifier.Should().Be("maria@acme.test");
        write.Values.Should().Equal(new Dictionary<long, string> { [City.Id] = "Sofia" });
    }

    [Fact]
    public void Plan_ChangedTestParticipant_UpdatesTheChangedValues_ClearingOnesLeftOut()
    {
        // Arrange
        Dictionary<string, OnForm> onForm = new()
        {
            ["maria@acme.test"] = new OnForm(7, IsTest: true, Cells(("City", "Sofia"), ("Seats", "3"))),
        };

        // Act
        Result<IReadOnlyList<PresetWrite>> plan = PresetReconciler.Plan(
            Properties, onForm, [Preset("Maria@acme.test", """{"city":"Varna"}""")]);

        // Assert
        PresetWrite write = plan.Value.Should().ContainSingle().Subject;
        write.MembershipId.Should().Be(7);
        write.Values.Should().Equal(new Dictionary<long, string> { [City.Id] = "Varna", [Seats.Id] = "" });
    }

    [Fact]
    public void Plan_ChangedTestParticipant_LeavesTheOtherValuesAsStored_EvenOnesTheirPropertyNoLongerAllows()
    {
        // Arrange
        Dictionary<string, OnForm> onForm = new()
        {
            ["maria@acme.test"] = new OnForm(7, IsTest: true, Cells(("City", "Sofia"), ("Seats", "many"))),
        };

        // Act
        Result<IReadOnlyList<PresetWrite>> plan = PresetReconciler.Plan(
            Properties, onForm, [Preset("maria@acme.test", """{"city":"Varna","seats":"many"}""")]);

        // Assert
        PresetWrite write = plan.Value.Should().ContainSingle().Subject;
        write.Values.Should().Equal(new Dictionary<long, string> { [City.Id] = "Varna" });
    }

    [Fact]
    public void Plan_TestParticipantWithTheSameValues_WritesNothing_HoweverANumberIsWritten()
    {
        // Arrange
        Dictionary<string, OnForm> onForm = new()
        {
            ["maria@acme.test"] = new OnForm(7, IsTest: true, Cells(("City", "Sofia"), ("Seats", "12.50"))),
        };

        // Act
        Result<IReadOnlyList<PresetWrite>> plan = PresetReconciler.Plan(
            Properties, onForm, [Preset("maria@acme.test", """{"city":"Sofia","seats":12.5}""")]);

        // Assert
        plan.Value.Should().BeEmpty();
    }

    [Fact]
    public void Plan_UnchangedTestParticipant_KeepsAValueItsPropertyNoLongerAllows()
    {
        // Arrange
        Dictionary<string, OnForm> onForm = new()
        {
            ["maria@acme.test"] = new OnForm(7, IsTest: true, Cells(("Seats", "many"))),
        };

        // Act
        Result<IReadOnlyList<PresetWrite>> plan = PresetReconciler.Plan(
            Properties, onForm, [Preset("maria@acme.test", """{"seats":"many"}""")]);

        // Assert
        plan.IsSuccess.Should().BeTrue();
        plan.Value.Should().BeEmpty();
    }

    [Fact]
    public void Plan_RealParticipantEchoedUnchanged_WritesNothing()
    {
        // Arrange: the Creator sends its whole list, real participants' read-only presets included
        Dictionary<string, OnForm> onForm = new()
        {
            ["real@acme.test"] = new OnForm(8, IsTest: false, Cells(("City", "Ruse"))),
        };

        // Act
        Result<IReadOnlyList<PresetWrite>> plan = PresetReconciler.Plan(
            Properties, onForm, [Preset("real@acme.test", """{"city":"Ruse"}""")]);

        // Assert
        plan.IsSuccess.Should().BeTrue();
        plan.Value.Should().BeEmpty();
    }

    [Fact]
    public void Plan_RealParticipant_IsRefusedAndNothingIsWritten()
    {
        // Arrange
        Dictionary<string, OnForm> onForm = new()
        {
            ["real@acme.test"] = new OnForm(8, IsTest: false, Cells()),
        };

        // Act
        Result<IReadOnlyList<PresetWrite>> plan = PresetReconciler.Plan(
            Properties,
            onForm,
            [Preset("new@acme.test", """{"city":"Sofia"}"""), Preset("real@acme.test", """{"city":"Ruse"}""")]);

        // Assert
        plan.Status.Should().Be(ResultStatus.Invalid);
        plan.ValidationErrors.Select(error => error.ErrorMessage).Should().Equal("Real participants are read-only.");
    }

    [Fact]
    public void Plan_ValueOfTheWrongType_IsRefusedNamingTheParticipant()
    {
        // Act
        Result<IReadOnlyList<PresetWrite>> plan = PresetReconciler.Plan(
            Properties, NoneOnForm, [Preset("maria@acme.test", """{"seats":"many"}""")]);

        // Assert
        plan.Status.Should().Be(ResultStatus.Invalid);
        plan.ValidationErrors.Select(error => error.ErrorMessage)
            .Should().Equal("maria@acme.test: 'Seats' must be a number.");
    }

    [Fact]
    public void Plan_ValueLongerThanACell_IsRefusedBeforeAnyWrite()
    {
        // Act
        Result<IReadOnlyList<PresetWrite>> plan = PresetReconciler.Plan(
            Properties,
            NoneOnForm,
            [Preset("maria@acme.test", $$"""{"city":"{{new string('a', PropertyValue.VALUE_MAX_LENGTH + 1)}}"}""")]);

        // Assert
        plan.Status.Should().Be(ResultStatus.Invalid);
        plan.ValidationErrors.Select(error => error.ErrorMessage)
            .Should().Equal($"maria@acme.test: Value must be at most {PropertyValue.VALUE_MAX_LENGTH} characters.");
    }

    [Fact]
    public void Plan_SameParticipantTwice_IsRefused()
    {
        // Act
        Result<IReadOnlyList<PresetWrite>> plan = PresetReconciler.Plan(
            Properties,
            NoneOnForm,
            [Preset("maria@acme.test", "{}"), Preset("MARIA@acme.test", "{}")]);

        // Assert
        plan.Status.Should().Be(ResultStatus.Invalid);
        plan.ValidationErrors.Select(error => error.ErrorMessage)
            .Should().Equal("Each participant can appear once: 'MARIA@acme.test'.");
    }

    private static readonly IReadOnlyDictionary<string, OnForm> NoneOnForm = new Dictionary<string, OnForm>();

    /// <summary>Matched as the handler matches: by identifier normalized for an email match key.</summary>
    private static PresetEdit Preset(string name, string variables) =>
        new(name, name.ToLowerInvariant(), JsonNode.Parse(variables)!.AsObject());

    private static Dictionary<long, string> Cells(params (string Property, string Value)[] cells) =>
        cells.ToDictionary(cell => Properties.Single(property => property.Name == cell.Property).Id, cell => cell.Value);

    private static Property PropertyOf(long id, string name, string dataType) =>
        new(new PropertyCreateArgs(1, 10, name, dataType, (int)id)) { Id = id };
}

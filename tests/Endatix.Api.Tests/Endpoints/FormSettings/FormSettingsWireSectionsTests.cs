using System.Reflection;
using Endatix.Api.Endpoints.FormSettings;
using FluentAssertions;

namespace Endatix.Api.Tests.Endpoints.FormSettings;

/// <summary>Every form settings section is on the wire: in the response model and in the PATCH request.</summary>
public sealed class FormSettingsWireSectionsTests
{
    public static TheoryData<string> Sections() => new(
        typeof(Core.Entities.FormSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name.EndsWith("Json", StringComparison.Ordinal))
            .Select(property => property.Name[..^"Json".Length]));

    [Theory]
    [MemberData(nameof(Sections))]
    public void Section_IsInTheResponseAndTheRequest(string section)
    {
        typeof(FormSettingsModel).GetProperty(section).Should().NotBeNull();
        typeof(PartialUpdateFormSettingsRequest).GetProperty(section).Should().NotBeNull();
    }

    [Theory]
    [MemberData(nameof(Sections))]
    public void Validator_AcceptsABodyThatNamesOnlyThatSection(string section)
    {
        var request = new PartialUpdateFormSettingsRequest { FormId = 1 };
        var property = typeof(PartialUpdateFormSettingsRequest).GetProperty(section)!;
        property.SetValue(request, Activator.CreateInstance(property.PropertyType));

        new PartialUpdateFormSettingsValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_RefusesABodyThatNamesNoSection()
    {
        var result = new PartialUpdateFormSettingsValidator().Validate(new PartialUpdateFormSettingsRequest { FormId = 1 });

        result.IsValid.Should().BeFalse();
    }
}

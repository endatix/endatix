using System.Reflection;
using System.Text.Json;
using Endatix.Api.Endpoints.FormSettings;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.FormSettings;
using FluentAssertions;

namespace Endatix.Api.Tests.Endpoints.FormSettings;

/// <summary>
/// Every form settings section is on the wire: in the response model and in the PATCH request, and copied between them
/// and the change and the DTO, so a section the endpoint forgets fails here instead of answering 200 and dropping it.
/// </summary>
public sealed class FormSettingsWireSectionsTests
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    // One value per section that differs from its defaults. A new section adds its entry here.
    private static readonly Dictionary<string, object> _nonDefault = new()
    {
        ["Pdf"] = new PdfSettings { UsesFormTheme = true },
        ["WebHooks"] = new WebHookConfiguration
        {
            Events = new Dictionary<string, WebHookEventConfig>
            {
                ["SubmissionCompleted"] = new()
                {
                    IsEnabled = true,
                    WebHookEndpoints =
                    [
                        new WebHookEndpointConfig
                        {
                            Url = "https://hooks.example.test/completed",
                            Authentication = new WebHookAuthConfig { Type = "ApiKey", ApiKey = "key", ApiKeyHeader = "X-Key" },
                        },
                    ],
                },
            },
        },
    };

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

    [Theory]
    [MemberData(nameof(Sections))]
    public void ToChange_WithOnlyThatSection_NamesIt(string section)
    {
        var request = new PartialUpdateFormSettingsRequest { FormId = 1 };
        var property = typeof(PartialUpdateFormSettingsRequest).GetProperty(section)!;
        property.SetValue(request, Activator.CreateInstance(property.PropertyType));

        var change = request.ToChange();

        typeof(FormSettingsChange).GetProperty(section)!.GetValue(change).Should().NotBeNull();
    }

    [Theory]
    [MemberData(nameof(Sections))]
    public void Map_CopiesThatSection(string section)
    {
        _nonDefault.Should().ContainKey(section, "each section needs a non-default value for this test");
        var value = _nonDefault[section];
        Serialize(value).Should().NotBe(Serialize(Activator.CreateInstance(value.GetType())!));

        var model = FormSettingsModel.Map(DtoWith(section, value));

        Serialize(typeof(FormSettingsModel).GetProperty(section)!.GetValue(model)!).Should().Be(Serialize(value));
    }

    [Fact]
    public void Validator_RefusesABodyThatNamesNoSection()
    {
        var result = new PartialUpdateFormSettingsValidator().Validate(new PartialUpdateFormSettingsRequest { FormId = 1 });

        result.IsValid.Should().BeFalse();
    }

    // A DTO at version 1 with every section at its defaults except the one named.
    private static FormSettingsDto DtoWith(string section, object value)
    {
        var constructor = typeof(FormSettingsDto).GetConstructors().Single();
        var arguments = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType == typeof(long)
                ? 1L
                : string.Equals(parameter.Name, section, StringComparison.OrdinalIgnoreCase)
                    ? value
                    : Activator.CreateInstance(parameter.ParameterType))
            .ToArray();
        return (FormSettingsDto)constructor.Invoke(arguments);
    }

    private static string Serialize(object value) => JsonSerializer.Serialize(value, value.GetType(), _json);
}

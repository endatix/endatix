using System.Reflection;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.FormSettings;

namespace Endatix.Core.Tests.Entities;

/// <summary>
/// A new section is a <c>*Json</c> column on <see cref="FormSettings"/>. Each one must also be in the change, in the
/// DTO, in <see cref="FormSettings.Apply"/> and in <see cref="FormSettings.UnreadableSections"/>, or a write would
/// silently drop it or an unreadable value would go unlogged.
/// </summary>
public sealed class FormSettingsSectionListTests
{
    public static TheoryData<string> Sections() => new(FormSettingsSections.Names());

    [Theory]
    [MemberData(nameof(Sections))]
    public void Section_IsInTheChangeAndTheDto(string section)
    {
        typeof(FormSettingsChange).GetProperty(section).Should().NotBeNull();
        typeof(FormSettingsDto).GetProperty(section).Should().NotBeNull();
    }

    [Theory]
    [MemberData(nameof(Sections))]
    public void Apply_WithOnlyThatSection_ChangesIt(string section)
    {
        var property = typeof(FormSettingsChange).GetProperty(section)!;
        var change = new FormSettingsChange();
        property.SetValue(change, Activator.CreateInstance(property.PropertyType));
        var form = Form.Create(2, new FormCreateArgs(TenantId: 1, Name: "Form"));
        var settings = FormSettings.For(form);

        settings.Apply(change, form, FormSettings.UNSAVED_VERSION, DateTime.UtcNow).Value.Should().BeTrue();
        typeof(FormSettings).GetProperty(section + "Json")!.GetValue(settings).Should().NotBeNull();
    }

    [Theory]
    [MemberData(nameof(Sections))]
    public void UnreadableSections_WithThatSectionUnreadable_NamesIt(string section)
    {
        var settings = FormSettings.For(Form.Create(2, new FormCreateArgs(TenantId: 1, Name: "Form")));
        typeof(FormSettings).GetProperty(section + "Json")!.SetValue(settings, "[]");

        settings.UnreadableSections().Should().Equal(JsonNamingPolicy.CamelCase.ConvertName(section));
    }
}

/// <summary>The sections of <see cref="FormSettings"/>, named after their <c>*Json</c> column.</summary>
public static class FormSettingsSections
{
    /// <summary>Each section's name: <c>Pdf</c> for <c>PdfJson</c>.</summary>
    public static IEnumerable<string> Names() =>
        typeof(FormSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name.EndsWith("Json", StringComparison.Ordinal))
            .Select(property => property.Name[..^"Json".Length]);
}

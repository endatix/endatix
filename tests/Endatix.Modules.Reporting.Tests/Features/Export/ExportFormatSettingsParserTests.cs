using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed class ExportFormatSettingsParserTests
{
    [Fact]
    public void Parse_WithNullOrEmpty_ReturnsDefaults()
    {
        ExportFormatSettingsParser.Parse(null).Should().Be(ExportFormatSettings.Default);
        ExportFormatSettingsParser.Parse("   ").Should().Be(ExportFormatSettings.Default);
    }

    [Fact]
    public void Parse_WithValidJson_ReturnsConfiguredSettings()
    {
        const string settingsJson = """
            {
              "aliasProfile": "crunch",
              "locale": "es",
              "columnScope": ["q1", "q2__yes"],
              "includeTestSubmissions": true
            }
            """;

        ExportFormatSettings settings = ExportFormatSettingsParser.Parse(settingsJson);

        settings.AliasProfile.Should().Be(ColumnAliasProfile.Crunch);
        settings.Locale.Should().Be("es");
        settings.ColumnScope.Should().BeEquivalentTo(["q1", "q2__yes"]);
        settings.IncludeTestSubmissions.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithInvalidJson_ReturnsDefaults()
    {
        ExportFormatSettings settings = ExportFormatSettingsParser.Parse("{ not-json");

        settings.Should().Be(ExportFormatSettings.Default);
    }

    [Fact]
    public void Resolve_AppliesRequestOverridesOverStoredSettings()
    {
        const string settingsJson = """
            {
              "aliasProfile": "native",
              "includeTestSubmissions": false,
              "columnScope": ["q1"]
            }
            """;

        ExportFormatSettings settings = ExportFormatSettingsParser.Resolve(
            settingsJson,
            includeTestSubmissions: true,
            columnScope: ["q2"]);

        settings.AliasProfile.Should().Be(ColumnAliasProfile.Native);
        settings.IncludeTestSubmissions.Should().BeTrue();
        settings.ColumnScope.Should().BeEquivalentTo(["q2"]);
    }

    [Fact]
    public void MergeRequestOverrides_WithEmptyColumnScope_PreservesExistingColumnScope()
    {
        ExportFormatSettings settings = ExportFormatSettings.Default with { ColumnScope = ["q1"] };

        ExportFormatSettings merged = settings.MergeRequestOverrides(includeTestSubmissions: null, columnScope: []);

        merged.ColumnScope.Should().BeEquivalentTo(["q1"]);
    }

    [Fact]
    public void Resolve_WithEmptyColumnScopeOverride_PreservesStoredColumnScope()
    {
        const string settingsJson = """{ "columnScope": ["q1"] }""";

        ExportFormatSettings settings = ExportFormatSettingsParser.Resolve(
            settingsJson,
            includeTestSubmissions: null,
            columnScope: []);

        settings.ColumnScope.Should().BeEquivalentTo(["q1"]);
    }
}

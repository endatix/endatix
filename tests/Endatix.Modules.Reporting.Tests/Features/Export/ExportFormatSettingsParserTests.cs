using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed class ExportFormatSettingsParserTests
{
    private static readonly ExportFormatSettingsParser Parser =
        new(NullLogger<ExportFormatSettingsParser>.Instance);

    [Fact]
    public void Parse_WithKeySeparator_ReturnsConfiguredSeparator()
    {
        const string settingsJson = """
            {
              "keySeparator": "--"
            }
            """;

        ExportFormatSettings settings = Parser.Parse(settingsJson);

        settings.KeySeparator.Should().Be("--");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RequireKeySeparator_WithEmptyValue_ThrowsArgumentException(string keySeparator)
    {
        Action act = () => ExportFormatSettings.RequireKeySeparator(keySeparator);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*key separator cannot be empty*");
    }

    [Fact]
    public void Parse_WithNullOrEmpty_ReturnsDefaults()
    {
        Parser.Parse(null).Should().Be(ExportFormatSettings.Default);
        Parser.Parse("   ").Should().Be(ExportFormatSettings.Default);
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

        ExportFormatSettings settings = Parser.Parse(settingsJson);

        settings.AliasProfile.Should().Be(ColumnAliasProfile.Crunch);
        settings.Locale.Should().Be("es");
        settings.ColumnScope.Should().BeEquivalentTo(["q1", "q2__yes"]);
        settings.IncludeTestSubmissions.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithInvalidJson_ReturnsDefaults()
    {
        ExportFormatSettings settings = Parser.Parse("{ not-json");

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

        ExportFormatSettings settings = Parser.Resolve(
            settingsJson,
            includeTestSubmissions: true,
            columnScope: ["q2"]);

        settings.AliasProfile.Should().Be(ColumnAliasProfile.Native);
        settings.IncludeTestSubmissions.Should().BeTrue();
        settings.ColumnScope.Should().BeEquivalentTo(["q2"]);
    }

    [Fact]
    public void Resolve_AppliesRequestLocaleOverride()
    {
        const string settingsJson = """{ "aliasProfile": "native", "keySeparator": "__" }""";

        ExportFormatSettings settings = Parser.Resolve(
            settingsJson,
            includeTestSubmissions: null,
            columnScope: null,
            locale: "es");

        settings.Locale.Should().Be("es");
    }

    [Fact]
    public void Resolve_WithPartialRequestOverride_PreservesNullOverrideValues()
    {
        const string settingsJson = """
            {
              "includeTestSubmissions": false,
              "columnScope": ["q1"]
            }
            """;

        ExportFormatSettings includeTestOverride = Parser.Resolve(
            settingsJson,
            includeTestSubmissions: true,
            columnScope: null);

        includeTestOverride.IncludeTestSubmissions.Should().BeTrue();
        includeTestOverride.ColumnScope.Should().BeEquivalentTo(["q1"]);

        ExportFormatSettings columnScopeOverride = Parser.Resolve(
            settingsJson,
            includeTestSubmissions: null,
            columnScope: ["q2"]);

        columnScopeOverride.IncludeTestSubmissions.Should().BeFalse();
        columnScopeOverride.ColumnScope.Should().BeEquivalentTo(["q2"]);
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

        ExportFormatSettings settings = Parser.Resolve(
            settingsJson,
            includeTestSubmissions: null,
            columnScope: []);

        settings.ColumnScope.Should().BeEquivalentTo(["q1"]);
    }
}

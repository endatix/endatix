using System.Text.Json;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Modules.Reporting.Tests.Features.ExportFormats;

public sealed class ExportFormatSettingsWriterTests
{
    [Theory]
    [InlineData(7, 7)]
    [InlineData(-1, 98)]
    public void Serialize_InvalidCodes_IdentifiesSettingsField(int none, int refused)
    {
        // Arrange
        var input = new ExportFormatSettingsInput { SpecialChoiceCodes = new(none, refused) };

        // Act
        var result = ExportFormatSettingsWriter.Serialize(input);

        // Assert
        result.ValidationErrors.Should().ContainSingle().Which.Identifier.Should().Be("specialChoiceCodes");
    }

    [Theory]
    [InlineData(ExportTarget.Submissions)]
    [InlineData(ExportTarget.Codebook)]
    public void Serialize_ConfiguredSpecials_RoundTripThroughRequestOverrides(ExportTarget target)
    {
        // Arrange
        var input = JsonSerializer.Deserialize<ExportFormatSettingsInput>(
            """{"specialChoiceCodes":{"none":99,"dontknow":97}}""", JsonSerializerOptions.Web);
        var parser = new ExportFormatSettingsParser(NullLogger<ExportFormatSettingsParser>.Instance);

        // Act
        var persisted = ExportFormatSettingsWriter.Serialize(input, target).Value;
        var resolved = parser.Resolve(persisted, false, null, "fr");

        // Assert
        persisted.Should().Contain("""{"none":99,"dontknow":97}""");
        resolved.SpecialChoiceCodes.Should().Be(new SpecialChoiceCodes(None: 99, DontKnow: 97));
    }

    [Fact]
    public void Serialize_WithInvalidKeySeparator_ReturnsInvalidResult()
    {
        ExportFormatSettingsInput input = new(KeySeparator: "   ");

        Result<string?> result = ExportFormatSettingsWriter.Serialize(input);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Serialize_WithValidSubmissionsSettings_ReturnsJsonWithoutLocale()
    {
        ExportFormatSettingsInput input = new(
            AliasProfile: ColumnAliasProfile.Crunch,
            KeySeparator: "--",
            IncludeTestSubmissions: true);

        Result<string?> result = ExportFormatSettingsWriter.Serialize(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("--");
        result.Value.Should().Contain("crunch");
        result.Value.Should().Contain("includeTestSubmissions");
        result.Value.Should().NotContain("locale");
    }

    [Fact]
    public void Serialize_WithCodebookNamingSettings_ReturnsJsonWithoutLocale()
    {
        ExportFormatSettingsInput input = new(
            AliasProfile: ColumnAliasProfile.Native,
            KeySeparator: "--");

        Result<string?> result = ExportFormatSettingsWriter.Serialize(input, ExportTarget.Codebook);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("native");
        result.Value.Should().Contain("--");
        result.Value.Should().NotContain("locale");
        result.Value.Should().NotContain("includeTestSubmissions");
    }

    [Fact]
    public void Serialize_WithCodebookIncludeTestSubmissions_ReturnsInvalid()
    {
        ExportFormatSettingsInput input = new(
            IncludeTestSubmissions: true,
            KeySeparator: "__");

        Result<string?> result = ExportFormatSettingsWriter.Serialize(input, ExportTarget.Codebook);

        result.IsSuccess.Should().BeFalse();
    }
}

using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Modules.Reporting.Tests.Features.ExportFormats;

public sealed class ExportFormatSettingsWriterTests
{
    private static readonly ExportFormatSettingsWriter Writer = new();

    [Fact]
    public void Serialize_WithInvalidKeySeparator_ReturnsInvalidResult()
    {
        ExportFormatSettingsInput input = new(KeySeparator: "   ");

        Result<string?> result = Writer.Serialize(input);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Serialize_WithValidSubmissionsSettings_ReturnsJsonWithoutLocale()
    {
        ExportFormatSettingsInput input = new(
            AliasProfile: ColumnAliasProfile.Crunch,
            KeySeparator: "--",
            IncludeTestSubmissions: true);

        Result<string?> result = Writer.Serialize(input);

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

        Result<string?> result = Writer.Serialize(input, ExportTarget.Codebook);

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

        Result<string?> result = Writer.Serialize(input, ExportTarget.Codebook);

        result.IsSuccess.Should().BeFalse();
    }
}

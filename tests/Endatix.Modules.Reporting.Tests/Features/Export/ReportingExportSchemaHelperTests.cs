using Endatix.Modules.Reporting.Features.Export;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed class ReportingExportSchemaHelperTests
{
    [Theory]
    [InlineData(null, "default")]
    [InlineData("", "default")]
    [InlineData("  ", "default")]
    [InlineData("es", "es")]
    [InlineData(" es ", "es")]
    public void ResolveLocaleOrDefault_UsesDefaultForBlank(string? locale, string expected)
    {
        ReportingExportSchemaHelper.ResolveLocaleOrDefault(locale).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("es", true)]
    [InlineData("fr", false)]
    public void IsLocaleAllowed_AllowsBlankAndCatalogLocales(string? locale, bool expected)
    {
        ReportingExportSchemaHelper.IsLocaleAllowed("""["default","es"]""", locale)
            .Should().Be(expected);
    }
}

using Endatix.Modules.Reporting.Features.FormSchema;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

public sealed class FormSchemaLocalesTests
{
    [Fact]
    public void Parse_WithValidArray_ReturnsLocales()
    {
        IReadOnlyList<string> locales = FormSchemaLocales.Parse("""["default","es","en"]""");

        locales.Should().Equal("default", "es", "en");
    }

    [Fact]
    public void Parse_WithEmptyOrInvalid_ReturnsDefaultOnly()
    {
        FormSchemaLocales.Parse("").Should().Equal("default");
        FormSchemaLocales.Parse("null").Should().Equal("default");
        FormSchemaLocales.Parse("{}").Should().Equal("default");
        FormSchemaLocales.Parse("[]").Should().Equal("default");
    }

    [Fact]
    public void Contains_MatchesOrdinal()
    {
        const string localesJson = """["default","es"]""";

        FormSchemaLocales.Contains(localesJson, "es").Should().BeTrue();
        FormSchemaLocales.Contains(localesJson, "ES").Should().BeFalse();
        FormSchemaLocales.Contains(localesJson, "fr").Should().BeFalse();
    }
}

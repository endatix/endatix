using Endatix.Core.Common.Translations;

namespace Endatix.Core.Tests.Entities;

public class TranslationLocaleListTests
{
    [Fact]
    public void ParseMany_CommaSeparatedAndRepeatedValues_ReturnsFlatNormalizedList()
    {
        string[] locales = ["ES, fr", " DE "];

        IReadOnlyList<CultureCode> parsed = TranslationLocaleList.ParseMany(locales);

        parsed.Select(c => c.Value).Should().Equal("es", "fr", "de");
    }

    [Fact]
    public void ParseMany_DuplicatesAndMalformedCodes_AreDropped()
    {
        string[] locales = ["es", "ES", "not a culture!", ""];

        IReadOnlyList<CultureCode> parsed = TranslationLocaleList.ParseMany(locales);

        parsed.Select(c => c.Value).Should().Equal("es");
    }

    [Fact]
    public void ParseMany_MoreLocalesThanCap_TruncatesToCap()
    {
        string[] locales = ["es", "fr", "de"];

        IReadOnlyList<CultureCode> parsed = TranslationLocaleList.ParseMany(locales, maxCount: 2);

        parsed.Select(c => c.Value).Should().Equal("es", "fr");
    }

    [Fact]
    public void ParseMany_NullLocales_ReturnsEmpty()
    {
        IReadOnlyList<CultureCode> parsed = TranslationLocaleList.ParseMany(null);

        parsed.Should().BeEmpty();
    }
}

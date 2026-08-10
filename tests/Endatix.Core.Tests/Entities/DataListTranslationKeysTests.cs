using Endatix.Core.Common.Translations;
using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class DataListTranslationKeysTests
{
    private static DataList CreateList(params string[] locales)
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities", defaultLocale: "en");
        foreach (string locale in locales)
        {
            dataList.AddCulture(CultureCode.Parse(locale));
        }

        return dataList;
    }

    [Fact]
    public void ResolveTranslationKeys_CatalogLocales_ReturnsNormalizedKeys()
    {
        DataList dataList = CreateList("es", "fr");

        IReadOnlyList<string> keys = dataList.ResolveTranslationKeys(
            [CultureCode.Parse("ES"), CultureCode.Parse("fr")]);

        keys.Should().Equal("es", "fr");
    }

    [Fact]
    public void ResolveTranslationKeys_DefaultLocaleOrSyntheticKey_FoldsIntoDefaultKey()
    {
        DataList dataList = CreateList("es");

        IReadOnlyList<string> keys = dataList.ResolveTranslationKeys(
            [CultureCode.Parse("en"), CultureCode.SyntheticDefault, CultureCode.Parse("es")]);

        keys.Should().Equal("default", "es");
    }

    [Fact]
    public void ResolveTranslationKeys_LocalesOutsideCatalog_AreDropped()
    {
        DataList dataList = CreateList("es");

        IReadOnlyList<string> keys = dataList.ResolveTranslationKeys(
            [CultureCode.Parse("de"), CultureCode.Parse("es")]);

        keys.Should().Equal("es");
    }

    [Fact]
    public void ResolveTranslationKeys_NullLocales_ReturnsEmpty()
    {
        DataList dataList = CreateList("es");

        IReadOnlyList<string> keys = dataList.ResolveTranslationKeys(null);

        keys.Should().BeEmpty();
    }
}

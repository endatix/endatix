using Endatix.Core.Common.Translations;
using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Common.Translations;

public class TranslationLabelKeysTests
{
    [Fact]
    public void TryResolveLabelKey_SyntheticDefault_ReturnsDefaultKey()
    {
        DataList dataList = CreateCatalog();

        var resolved = dataList.TryResolveLabelKey(CultureCode.SyntheticDefault, out string labelKey);

        resolved.Should().BeTrue();
        labelKey.Should().Be(SurveyJsTranslationKeys.DefaultKey);
    }

    [Fact]
    public void TryResolveLabelKey_DefaultCulture_ReturnsDefaultKey()
    {
        DataList dataList = CreateCatalog(defaultLocale: "en");

        var resolved = dataList.TryResolveLabelKey(CultureCode.Parse("en"), out string labelKey);

        resolved.Should().BeTrue();
        labelKey.Should().Be(SurveyJsTranslationKeys.DefaultKey);
    }

    [Fact]
    public void TryResolveLabelKey_CatalogLocale_ReturnsCultureValue()
    {
        DataList dataList = CreateCatalog();
        dataList.AddCulture(CultureCode.Parse("es"));

        var resolved = dataList.TryResolveLabelKey(CultureCode.Parse("es"), out string labelKey);

        resolved.Should().BeTrue();
        labelKey.Should().Be("es");
    }

    [Fact]
    public void TryResolveLabelKey_UnknownLocale_ReturnsFalse()
    {
        DataList dataList = CreateCatalog();

        var resolved = dataList.TryResolveLabelKey(CultureCode.Parse("fr"), out string labelKey);

        resolved.Should().BeFalse();
        labelKey.Should().BeEmpty();
    }

    private static DataList CreateCatalog(string defaultLocale = "en") =>
        new(SampleData.TENANT_ID, "Cities", normalizedName: "cities", defaultLocale: defaultLocale);
}

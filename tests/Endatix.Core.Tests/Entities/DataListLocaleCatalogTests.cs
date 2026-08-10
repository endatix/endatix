using Endatix.Core.Common.Translations;
using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class DataListLocaleCatalogTests
{
    [Fact]
    public void DataList_ImplementsIHasTranslations()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");

        dataList.Should().BeAssignableTo<IHasTranslations>();
        dataList.DefaultCulture.Should().Be(SurveyJsTranslationKeys.FallbackDefaultCulture);
        dataList.MaxAvailableCultures.Should().Be(IHasTranslations.DEFAULT_MAX_AVAILABLE_CULTURES);
    }

    [Fact]
    public void AddCulture_AddsCultureToCatalog()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");
        IHasTranslations translations = dataList;

        translations.AddCulture(CultureCode.Parse("ES"));

        dataList.AvailableLocales.Should().Equal("es");
        translations.AvailableCultures.Should().Equal("es");
    }

    [Fact]
    public void AddCulture_DefaultKey_Throws()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");

        Action act = () => dataList.AddCulture(CultureCode.SyntheticDefault);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RemoveCulture_StripsLabelsFromItems()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");
        dataList.AddCulture(CultureCode.Parse("es"));
        dataList.AddItem(
            new Dictionary<string, string>
            {
                ["default"] = "Apple",
                ["es"] = "Manzana"
            },
            "apple");

        dataList.RemoveCulture(CultureCode.Parse("es"));

        dataList.AvailableLocales.Should().BeEmpty();
        dataList.Items.Single().Labels.Should().NotContainKey("es");
        dataList.Items.Single().Labels["default"].Should().Be("Apple");
    }

    [Fact]
    public void AddItem_UnknownCulture_Throws()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");

        Action act = () => dataList.AddItem(
            new Dictionary<string, string>
            {
                ["default"] = "Apple",
                ["fr"] = "Pomme"
            },
            "apple");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_OmitsDefaultLocale_UsesFallbackCulture()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");

        dataList.DefaultLocale.Should().Be(SurveyJsTranslationKeys.FallbackDefaultCulture);
    }

    [Fact]
    public void Ctor_RealDefaultLocale_StoresNormalizedCulture()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities", defaultLocale: " ES ");

        dataList.DefaultLocale.Should().Be("es");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("DEFAULT")]
    [InlineData(" Default ")]
    public void Ctor_SyntheticDefaultLocale_Throws(string defaultLocale)
    {
        Action act = () => _ = new DataList(SampleData.TENANT_ID, "Cities", defaultLocale: defaultLocale);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("defaultLocale");
    }

    [Fact]
    public void SetDefaultCulture_RejectsSyntheticDefaultKey()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");

        Action act = () => dataList.SetDefaultCulture(CultureCode.SyntheticDefault);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsDefaultKey_MatchesSyntheticDefaultAndDefaultCulture()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities", defaultLocale: "en");
        IHasTranslations translations = dataList;

        translations.IsDefaultKey(CultureCode.SyntheticDefault).Should().BeTrue();
        translations.IsDefaultKey(CultureCode.Parse("DEFAULT")).Should().BeTrue();
        translations.IsDefaultKey(CultureCode.Parse("en")).Should().BeTrue();
        translations.IsDefaultKey(CultureCode.Parse("EN")).Should().BeTrue();
        translations.IsDefaultKey(CultureCode.Parse("es")).Should().BeFalse();
    }

    [Theory]
    [InlineData("default")]
    [InlineData("DEFAULT")]
    [InlineData(" Default ")]
    public void AllowsTranslationKey_AcceptsSyntheticDefaultKeyVariants(string key)
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");

        dataList.AllowsTranslationKey(CultureCode.Parse(key)).Should().BeTrue();
    }

    [Fact]
    public void AllowsTranslationKey_AllowsDefaultAndCatalogCultures()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");
        dataList.AddCulture(CultureCode.Parse("es"));

        dataList.AllowsTranslationKey(CultureCode.SyntheticDefault).Should().BeTrue();
        dataList.AllowsTranslationKey(CultureCode.Parse("ES")).Should().BeTrue();
        dataList.AllowsTranslationKey(CultureCode.Parse("fr")).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "default")]
    [InlineData("", "default")]
    [InlineData("default", "default")]
    [InlineData("en", "default")]
    [InlineData("ES", "es")]
    [InlineData("fr", "default")]
    public void ResolveLabelSearchKey_MapsLocaleToJsonKey(string? locale, string expectedKey)
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities", defaultLocale: "en");
        dataList.AddCulture(CultureCode.Parse("es"));

        CultureCode? culture = string.IsNullOrWhiteSpace(locale) ? null : CultureCode.Parse(locale);
        dataList.ResolveLabelSearchKey(culture).Should().Be(expectedKey);
    }
}

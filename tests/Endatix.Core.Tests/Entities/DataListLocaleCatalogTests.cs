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

        translations.AddCulture("ES");

        dataList.AvailableLocales.Should().Equal("es");
        translations.AvailableCultures.Should().Equal("es");
    }

    [Fact]
    public void AddCulture_DefaultKey_Throws()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");

        Action act = () => dataList.AddCulture("default");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RemoveCulture_StripsLabelsFromItems()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");
        dataList.AddCulture("es");
        dataList.AddItem(
            new Dictionary<string, string>
            {
                ["default"] = "Apple",
                ["es"] = "Manzana"
            },
            "apple");

        dataList.RemoveCulture("es");

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
    public void SetDefaultCulture_RejectsSyntheticDefaultKey()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");

        Action act = () => dataList.SetDefaultCulture("default");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsSyntheticDefault_UsesSharedNormalizer()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");
        IHasTranslations translations = dataList;

        translations.IsSyntheticDefault("DEFAULT").Should().BeTrue();
        translations.IsSyntheticDefault("es").Should().BeFalse();
    }

    [Theory]
    [InlineData("default")]
    [InlineData("DEFAULT")]
    [InlineData(" Default ")]
    public void AllowsTranslationKey_AcceptsSyntheticDefaultKeyVariants(string key)
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");

        dataList.AllowsTranslationKey(key).Should().BeTrue();
    }

    [Fact]
    public void AllowsTranslationKey_AllowsDefaultAndCatalogCultures()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");
        dataList.AddCulture("es");

        dataList.AllowsTranslationKey("default").Should().BeTrue();
        dataList.AllowsTranslationKey("ES").Should().BeTrue();
        dataList.AllowsTranslationKey("fr").Should().BeFalse();
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
        dataList.AddCulture("es");

        dataList.ResolveLabelSearchKey(locale).Should().Be(expectedKey);
    }

    [Theory]
    [InlineData("not a culture!")]
    [InlineData("en_US")]
    [InlineData("123")]
    public void ResolveLabelSearchKey_InvalidCultureCode_ThrowsArgumentException(string locale)
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities", defaultLocale: "en");

        Action act = () => dataList.ResolveLabelSearchKey(locale);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("cultureCode");
    }
}

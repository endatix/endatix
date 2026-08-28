using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.UseCases.DataLists;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Core.Tests.UseCases.DataLists;

public class DataListEnsureLocalesTests
{
    private static DataList CreateDataList() =>
        new(SampleData.TENANT_ID, "Cities", "Test list", "cities");

    [Fact]
    public void TryEnsure_WithValidLocales_ReturnsNull()
    {
        // Arrange
        var dataList = CreateDataList();

        // Act
        var errors = DataListEnsureLocales.TryEnsure(dataList, ["es", "fr"], NullLogger.Instance);

        // Assert
        errors.Should().BeNull();
        dataList.AvailableLocales.Should().Contain(["es", "fr"]);
    }

    /// <summary>
    /// The catalog cap is a domain rule the caller can act on. Masking it as "Could not add locale."
    /// left the failure undiagnosable, so it must arrive intact via <see cref="IEndUserSafeError"/>.
    /// </summary>
    [Fact]
    public void TryEnsure_WhenCatalogCapExceeded_SurfacesTheDomainRule()
    {
        // Arrange
        var dataList = CreateDataList();
        var locales = BuildLocalesBeyondCap(dataList.MaxAvailableCultures);

        // Act
        var errors = DataListEnsureLocales.TryEnsure(dataList, locales, NullLogger.Instance);

        // Assert
        errors.Should().NotBeNull();
        errors!.Should().Contain(e =>
            e.ErrorMessage == $"A data list cannot have more than {dataList.MaxAvailableCultures} cultures.");
        errors.Should().NotContain(e => e.ErrorMessage == "Could not add locale.");
    }

    [Fact]
    public void TryEnsure_WithMalformedCultureCode_ReportsTheToken()
    {
        // Arrange
        var dataList = CreateDataList();

        // Act
        var errors = DataListEnsureLocales.TryEnsure(dataList, ["not a locale"], NullLogger.Instance);

        // Assert
        errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("'not a locale' is not a valid culture code.");
    }

    [Fact]
    public void TryEnsure_WithSyntheticDefaultKey_IsRejected()
    {
        // Arrange
        var dataList = CreateDataList();

        // Act
        var errors = DataListEnsureLocales.TryEnsure(dataList, ["default"], NullLogger.Instance);

        // Assert
        errors.Should().ContainSingle()
            .Which.Identifier.Should().Be("EnsureLocales.default");
    }

    /// <summary>
    /// Enough distinct, well-formed culture codes to push the catalog past its cap.
    /// </summary>
    private static string[] BuildLocalesBeyondCap(int cap)
    {
        string[] pool =
        [
            "es", "fr", "de", "it", "pt", "nl", "sv", "da", "fi", "no",
            "pl", "cs", "sk", "hu", "ro", "bg", "el", "tr", "ru", "uk",
            "ja", "ko", "zh", "ar", "he", "hi", "th", "vi", "id", "ms",
            "et", "lv", "lt", "sl", "hr", "sr", "ca", "eu", "gl", "is"
        ];

        return [.. pool.Take(cap + 1)];
    }
}

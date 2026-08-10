using Endatix.Api.Endpoints.DataLists;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class RemoveDataListLocaleValidatorTests
{
    private readonly RemoveDataListLocaleValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_PassesValidation()
    {
        var result = _validator.TestValidate(new RemoveDataListLocaleRequest
        {
            DataListId = 1,
            Locale = "es"
        });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveDataListId_ReturnsError(long dataListId)
    {
        var result = _validator.TestValidate(new RemoveDataListLocaleRequest
        {
            DataListId = dataListId,
            Locale = "es"
        });

        result.ShouldHaveValidationErrorFor(x => x.DataListId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyLocale_ReturnsError(string? locale)
    {
        var result = _validator.TestValidate(new RemoveDataListLocaleRequest
        {
            DataListId = 1,
            Locale = locale
        });

        result.ShouldHaveValidationErrorFor(x => x.Locale);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("DEFAULT")]
    [InlineData("not a culture!")]
    [InlineData("en_US")]
    public void Validate_InvalidOrSyntheticLocale_ReturnsError(string locale)
    {
        var result = _validator.TestValidate(new RemoveDataListLocaleRequest
        {
            DataListId = 1,
            Locale = locale
        });

        result.ShouldHaveValidationErrorFor(x => x.Locale)
            .WithErrorMessage("Locale must be a valid culture code (e.g. 'es'), not 'default'.");
    }

    [Theory]
    [InlineData("ES")]
    [InlineData(" en-US ")]
    public void Validate_LocaleCaseAndWhitespace_PassesValidation(string locale)
    {
        var result = _validator.TestValidate(new RemoveDataListLocaleRequest
        {
            DataListId = 1,
            Locale = locale
        });

        result.ShouldNotHaveAnyValidationErrors();
    }
}

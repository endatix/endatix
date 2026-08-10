using Endatix.Api.Endpoints.DataLists;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class SetDataListDefaultLocaleValidatorTests
{
    private readonly SetDataListDefaultLocaleValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_PassesValidation()
    {
        var result = _validator.TestValidate(new SetDataListDefaultLocaleRequest
        {
            DataListId = 1,
            DefaultLocale = "en"
        });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveDataListId_ReturnsError(long dataListId)
    {
        var result = _validator.TestValidate(new SetDataListDefaultLocaleRequest
        {
            DataListId = dataListId,
            DefaultLocale = "en"
        });

        result.ShouldHaveValidationErrorFor(x => x.DataListId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyDefaultLocale_ReturnsError(string? defaultLocale)
    {
        var result = _validator.TestValidate(new SetDataListDefaultLocaleRequest
        {
            DataListId = 1,
            DefaultLocale = defaultLocale
        });

        result.ShouldHaveValidationErrorFor(x => x.DefaultLocale);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("DEFAULT")]
    [InlineData("not a culture!")]
    [InlineData("en_US")]
    public void Validate_InvalidOrSyntheticDefaultLocale_ReturnsError(string defaultLocale)
    {
        var result = _validator.TestValidate(new SetDataListDefaultLocaleRequest
        {
            DataListId = 1,
            DefaultLocale = defaultLocale
        });

        result.ShouldHaveValidationErrorFor(x => x.DefaultLocale)
            .WithErrorMessage("Default Locale must be a valid culture code (e.g. 'es'), not 'default'.");
    }

    [Theory]
    [InlineData("EN")]
    [InlineData(" en-US ")]
    public void Validate_DefaultLocaleCaseAndWhitespace_PassesValidation(string defaultLocale)
    {
        var result = _validator.TestValidate(new SetDataListDefaultLocaleRequest
        {
            DataListId = 1,
            DefaultLocale = defaultLocale
        });

        result.ShouldNotHaveAnyValidationErrors();
    }
}

using Endatix.Api.Endpoints.Common;
using Endatix.Api.Endpoints.DataLists;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class DataListsListValidatorTests
{
    private readonly DataListsListValidator _validator = new();

    [Fact]
    public void Validate_SingleHasLocale_Passes()
    {
        var result = _validator.TestValidate(new DataListsListRequest { HasLocale = "es" });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_CommaSeparatedHasLocale_Passes()
    {
        var result = _validator.TestValidate(new DataListsListRequest { HasLocale = "es,de" });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_HasLocaleDefault_Fails()
    {
        var result = _validator.TestValidate(new DataListsListRequest { HasLocale = "default" });

        result.ShouldHaveValidationErrorFor(x => x.HasLocale)
            .WithErrorMessage("Has Locale must be a culture code or comma-separated list (e.g. 'es' or 'es,de'), not 'default'.");
    }

    [Fact]
    public void Validate_HasLocaleTooMany_Fails()
    {
        string tooMany = string.Join(
            ",",
            Enumerable.Range(0, CultureCodeValidation.MaxLocales + 1).Select(i => $"aa-{i:00}"));

        var result = _validator.TestValidate(new DataListsListRequest { HasLocale = tooMany });

        result.ShouldHaveValidationErrorFor(x => x.HasLocale)
            .WithErrorMessage($"No more than {CultureCodeValidation.MaxLocales} locales can be requested.");
    }

    [Fact]
    public void Validate_HasLocaleEmptyTokens_Fails()
    {
        var result = _validator.TestValidate(new DataListsListRequest { HasLocale = "," });

        result.ShouldHaveValidationErrorFor(x => x.HasLocale);
    }
}

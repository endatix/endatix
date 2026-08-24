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
            .WithErrorMessage(
                $"No more than {CultureCodeValidation.MaxLocales} locales can be requested. Received: {tooMany}.");
    }

    [Fact]
    public void Validate_HasLocaleEmptyTokens_Fails()
    {
        var result = _validator.TestValidate(new DataListsListRequest { HasLocale = "," });

        result.ShouldHaveValidationErrorFor(x => x.HasLocale);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("CreatedAt")]
    [InlineData("modifiedAt")]
    [InlineData("itemsCount")]
    [InlineData("isActive")]
    public void Validate_AllowedSortBy_Passes(string sortBy)
    {
        var result = _validator.TestValidate(new DataListsListRequest { SortBy = sortBy });

        result.ShouldNotHaveValidationErrorFor(x => x.SortBy);
    }

    [Fact]
    public void Validate_UnknownSortBy_Fails()
    {
        var result = _validator.TestValidate(new DataListsListRequest { SortBy = "unknownField" });

        result.ShouldHaveValidationErrorFor(x => x.SortBy);
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("DESC")]
    public void Validate_AllowedSortDir_Passes(string sortDir)
    {
        var result = _validator.TestValidate(new DataListsListRequest { SortDir = sortDir });

        result.ShouldNotHaveValidationErrorFor(x => x.SortDir);
    }

    [Fact]
    public void Validate_UnknownSortDir_Fails()
    {
        var result = _validator.TestValidate(new DataListsListRequest { SortDir = "sideways" });

        result.ShouldHaveValidationErrorFor(x => x.SortDir);
    }

    [Theory]
    [InlineData(nameof(DataListsListRequest.CreatedFrom))]
    [InlineData(nameof(DataListsListRequest.CreatedTo))]
    [InlineData(nameof(DataListsListRequest.ModifiedFrom))]
    [InlineData(nameof(DataListsListRequest.ModifiedTo))]
    public void Validate_DateBound_InvalidFormat_Fails(string propertyName)
    {
        var request = new DataListsListRequest();
        typeof(DataListsListRequest).GetProperty(propertyName)!.SetValue(request, "12/31/2024");

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(propertyName);
    }

    [Fact]
    public void Validate_DateBoundAtCalendarMaximum_Passes()
    {
        // Regression guard: List.ParseExclusiveDayEndUtc must not overflow on the
        // last representable calendar date when computing the exclusive upper bound.
        var result = _validator.TestValidate(new DataListsListRequest
        {
            CreatedTo = "9999-12-31",
            ModifiedTo = "9999-12-31"
        });

        result.ShouldNotHaveValidationErrorFor(x => x.CreatedTo);
        result.ShouldNotHaveValidationErrorFor(x => x.ModifiedTo);
    }
}

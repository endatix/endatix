using Endatix.Api.Endpoints.Common;
using Endatix.Api.Endpoints.DataLists;
using Endatix.Core.Entities;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class ImportDataListValidatorTests
{
    private readonly ImportDataListValidator _validator = new();

    [Fact]
    public void Validate_JsonItemsAtMaxLimit_PassesValidation()
    {
        var items = Enumerable.Range(1, DataList.MAX_ITEMS)
            .Select(i => new ImportDataListItemRequest { Label = $"Label{i}", Value = $"Value{i}" })
            .ToList();

        var result = _validator.TestValidate(new ImportDataListRequest
        {
            DataListId = 1,
            Format = Import.FormatJson,
            Items = items
        });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_JsonItemsExceedsMaxLimit_ReturnsError()
    {
        var items = Enumerable.Range(1, DataList.MAX_ITEMS + 1)
            .Select(i => new ImportDataListItemRequest { Label = $"Label{i}", Value = $"Value{i}" })
            .ToList();

        var result = _validator.TestValidate(new ImportDataListRequest
        {
            DataListId = 1,
            Format = Import.FormatJson,
            Items = items
        });

        result.ShouldHaveValidationErrorFor(x => x.Items)
            .WithErrorMessage($"A data list cannot have more than {DataList.MAX_ITEMS} items.");
    }

    [Fact]
    public void Validate_EmptyJsonItems_PassesValidation()
    {
        var result = _validator.TestValidate(new ImportDataListRequest
        {
            DataListId = 1,
            Format = Import.FormatJson,
            Items = []
        });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_NullJsonItems_ReturnsError()
    {
        var result = _validator.TestValidate(new ImportDataListRequest
        {
            DataListId = 1,
            Format = Import.FormatJson,
            Items = null
        });

        result.ShouldHaveValidationErrorFor(x => x.Items);
    }

    [Fact]
    public void Validate_NullFormat_DefaultsToJson_RequiresItems()
    {
        var result = _validator.TestValidate(new ImportDataListRequest
        {
            DataListId = 1,
            Format = null,
            Items = null
        });

        result.ShouldHaveValidationErrorFor(x => x.Items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveDataListId_ReturnsError(long dataListId)
    {
        var result = _validator.TestValidate(new ImportDataListRequest
        {
            DataListId = dataListId,
            Format = Import.FormatJson,
            Items = []
        });

        result.ShouldHaveValidationErrorFor(x => x.DataListId);
    }

    [Fact]
    public void Validate_CsvWithoutBody_ReturnsError()
    {
        var result = _validator.TestValidate(new ImportDataListRequest
        {
            DataListId = 1,
            Format = Import.FormatCsv,
            Csv = null
        });

        result.ShouldHaveValidationErrorFor(x => x.Csv);
    }

    [Fact]
    public void Validate_CsvExceedsMaxLength_ReturnsError()
    {
        var result = _validator.TestValidate(new ImportDataListRequest
        {
            DataListId = 1,
            Format = Import.FormatCsv,
            Csv = new string('a', ImportDataListValidator.MaxCsvLength + 1)
        });

        result.ShouldHaveValidationErrorFor(x => x.Csv);
    }

    [Fact]
    public void Validate_ValidCsv_PassesValidation()
    {
        var result = _validator.TestValidate(new ImportDataListRequest
        {
            DataListId = 1,
            Format = Import.FormatCsv,
            Csv = "value,default\r\napple,Apple\r\n"
        });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("xlsx")]
    public void Validate_InvalidFormat_ReturnsError(string format)
    {
        var result = _validator.TestValidate(new ImportDataListRequest
        {
            DataListId = 1,
            Format = format,
            Items = []
        });

        result.ShouldHaveValidationErrorFor(x => x.Format)
            .WithErrorMessage("Format must be 'json' or 'csv'.");
    }

    [Theory]
    [InlineData("JSON")]
    [InlineData(" csv ")]
    public void Validate_FormatCaseAndWhitespace_PassesValidation(string format)
    {
        var result = _validator.TestValidate(new ImportDataListRequest
        {
            DataListId = 1,
            Format = format,
            Items = format.Contains("csv", StringComparison.OrdinalIgnoreCase)
                ? null
                : [],
            Csv = format.Contains("csv", StringComparison.OrdinalIgnoreCase)
                ? "value,default\r\napple,Apple\r\n"
                : null
        });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_JsonItemMissingLabelsAndLabel_ReturnsError()
    {
        var result = _validator.TestValidate(new ImportDataListRequest
        {
            DataListId = 1,
            Format = Import.FormatJson,
            Items = [new ImportDataListItemRequest { Value = "apple" }]
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Either Labels or Label is required.");
    }

    [Fact]
    public void Validate_JsonItemMissingValue_ReturnsError()
    {
        var result = _validator.TestValidate(new ImportDataListRequest
        {
            DataListId = 1,
            Format = Import.FormatJson,
            Items = [new ImportDataListItemRequest { Label = "Apple", Value = "" }]
        });

        result.ShouldHaveValidationErrorFor("Items[0].Value");
    }

    [Fact]
    public void Validate_EnsureLocalesRejectsSyntheticDefault_ReturnsError()
    {
        var result = _validator.TestValidate(new ImportDataListRequest
        {
            DataListId = 1,
            Format = Import.FormatJson,
            Items = [],
            EnsureLocales = ["default"]
        });

        result.ShouldHaveValidationErrorFor(x => x.EnsureLocales);
    }

    [Fact]
    public void Validate_EnsureLocalesExceedsMax_ReturnsErrorWithoutRequiringParseOfRest()
    {
        string[] ensureLocales =
        [
            .. Enumerable.Range(0, CultureCodeValidation.MaxLocales + 1)
                .Select(i => $"l{i}")
        ];

        var result = _validator.TestValidate(new ImportDataListRequest
        {
            DataListId = 1,
            Format = Import.FormatJson,
            Items = [],
            EnsureLocales = ensureLocales
        });

        result.ShouldHaveValidationErrorFor(x => x.EnsureLocales)
            .WithErrorMessage($"No more than {CultureCodeValidation.MaxLocales} locales can be ensured.");
    }

    [Fact]
    public void Validate_EnsureLocalesValidCultures_PassesValidation()
    {
        var result = _validator.TestValidate(new ImportDataListRequest
        {
            DataListId = 1,
            Format = Import.FormatJson,
            Items = [],
            EnsureLocales = ["es", "fr"]
        });

        result.ShouldNotHaveAnyValidationErrors();
    }
}

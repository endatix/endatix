using Endatix.Api.Endpoints.DataLists;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class ExportDataListValidatorTests
{
    private readonly ExportDataListValidator _validator = new();

    [Fact]
    public void Validate_ValidCsvRequest_PassesValidation()
    {
        var result = _validator.TestValidate(new ExportDataListRequest
        {
            DataListId = 1,
            Format = Export.FormatCsv
        });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_NullFormat_DefaultsAllowed_PassesValidation()
    {
        var result = _validator.TestValidate(new ExportDataListRequest
        {
            DataListId = 1,
            Format = null
        });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveDataListId_ReturnsError(long dataListId)
    {
        var result = _validator.TestValidate(new ExportDataListRequest
        {
            DataListId = dataListId,
            Format = Export.FormatCsv
        });

        result.ShouldHaveValidationErrorFor(x => x.DataListId);
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("xlsx")]
    [InlineData("text")]
    public void Validate_InvalidFormat_ReturnsError(string format)
    {
        var result = _validator.TestValidate(new ExportDataListRequest
        {
            DataListId = 1,
            Format = format
        });

        result.ShouldHaveValidationErrorFor(x => x.Format)
            .WithErrorMessage("Format must be 'csv' or 'json'.");
    }

    [Theory]
    [InlineData("CSV")]
    [InlineData(" json ")]
    public void Validate_FormatCaseAndWhitespace_PassesValidation(string format)
    {
        var result = _validator.TestValidate(new ExportDataListRequest
        {
            DataListId = 1,
            Format = format
        });

        result.ShouldNotHaveAnyValidationErrors();
    }
}

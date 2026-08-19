using Endatix.Api.Endpoints.DataLists;
using Endatix.Core.UseCases.DataLists.Search;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class ListDataListItemsValidatorTests
{
    private readonly ListDataListItemsValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_PassesValidation()
    {
        var result = _validator.TestValidate(new ListDataListItemsRequest
        {
            DataListId = 1,
            Query = "york",
            Page = 1,
            PageSize = 25,
            MatchMode = DataListSearchMatchMode.Contains,
            Locale = "es",
            IncludeLocales = ["fr"]
        });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveDataListId_ReturnsError(long dataListId)
    {
        var result = _validator.TestValidate(new ListDataListItemsRequest
        {
            DataListId = dataListId
        });

        result.ShouldHaveValidationErrorFor(x => x.DataListId);
    }

    [Fact]
    public void Validate_PageSizeAboveMaxTake_ReturnsError()
    {
        var result = _validator.TestValidate(new ListDataListItemsRequest
        {
            DataListId = 1,
            PageSize = SearchDataListItemsQuery.MaxTake + 1
        });

        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }
}

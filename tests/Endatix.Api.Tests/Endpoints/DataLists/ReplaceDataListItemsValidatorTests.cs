using Endatix.Api.Endpoints.DataLists;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class ReplaceDataListItemsValidatorTests
{
    private readonly ReplaceDataListItemsValidator _validator;

    public ReplaceDataListItemsValidatorTests()
    {
        _validator = new ReplaceDataListItemsValidator();
    }

    [Fact]
    public void Validate_ItemsAtMaxLimit_PassesValidation()
    {
        var items = Enumerable.Range(1, 5_000)
            .Select(i => new ReplaceDataListItemRequest { Label = $"Label{i}", Value = $"Value{i}" })
            .ToList();

        var request = new ReplaceDataListItemsRequest { DataListId = 1, Items = items };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ItemsExceedsMaxLimit_ReturnsError()
    {
        var items = Enumerable.Range(1, 5_001)
            .Select(i => new ReplaceDataListItemRequest { Label = $"Label{i}", Value = $"Value{i}" })
            .ToList();

        var request = new ReplaceDataListItemsRequest { DataListId = 1, Items = items };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Items)
            .WithErrorMessage("A data list cannot have more than 5,000 items.");
    }

    [Fact]
    public void Validate_EmptyItems_PassesValidation()
    {
        var request = new ReplaceDataListItemsRequest { DataListId = 1, Items = [] };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_NullItems_ReturnsError()
    {
        var request = new ReplaceDataListItemsRequest { DataListId = 1, Items = null! };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Items);
    }
}
using Endatix.Api.Common;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Common;

public class FilteredRequestValidatorTests
{
    private readonly FilteredRequestValidator _validator;

    public FilteredRequestValidatorTests()
    {
        var validFields = new Dictionary<string, Type>
        {
            { "name", typeof(string) },
            { "name1", typeof(string) },
            { "age", typeof(int) },
            { "created", typeof(DateTime) },
        };
        _validator = new FilteredRequestValidator(validFields);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_EmptyFilter_ReturnsError(string filter)
    {
        // Arrange
        var request = new TestFilteredRequest { Filter = [filter] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Filter)
            .WithErrorMessage($"Invalid filter '{filter}': Filter cannot be empty");
    }

    [Theory]
    [InlineData(":value")]
    [InlineData("invalid:value")]
    public void Validate_InvalidFieldName_ReturnsError(string filter)
    {
        // Arrange
        var request = new TestFilteredRequest { Filter = [filter] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Filter)
            .WithErrorMessage($"Invalid filter '{filter}': Filter must start with a valid field name. Allowed fields: name, name1, age, created");
    }

    [Fact]
    public void Validate_InvalidOperator_ReturnsError()
    {
        // Arrange
        var invalidFilter = "name==test";
        var request = new TestFilteredRequest { Filter = [invalidFilter] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Filter)
            .WithErrorMessage($"Invalid filter '{invalidFilter}': Filter must contain a valid operator after the field name. Allowed operators: !:, >:, <:, :, >, <");
    }

    [Fact]
    public void Validate_MissingValue_ReturnsError()
    {
        var filter = "age:";
        // Arrange
        var request = new TestFilteredRequest { Filter = [filter] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Filter)
            .WithErrorMessage($"Invalid filter '{filter}': Filter must contain a value after the operator.");
    }

    [Theory]
    [InlineData("age:notanumber", "One or more values are not valid for type Int32")]
    [InlineData("age:5|twenty", "One or more values are not valid for type Int32")]
    [InlineData("age>:abc", "Value is not valid for type Int32")]
    [InlineData("created>yesterday", "Value is not valid for type DateTime")]
    public void Validate_InvalidValueType_ReturnsError(string filter, string expectedError)
    {
        // Arrange
        var request = new TestFilteredRequest { Filter = [filter] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Filter)
            .WithErrorMessage($"Invalid filter '{filter}': {expectedError}");
    }

    [Theory]
    [InlineData("name:john")]
    [InlineData("name1:john|jane")]
    [InlineData("age:25")]
    [InlineData("age>:18")]
    [InlineData("created>2025-01-05T15:14:13")]
    public void Validate_ValidFilter_PassesValidation(string filter)
    {
        // Arrange
        var request = new TestFilteredRequest { Filter = [filter] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    private class TestFilteredRequest : IFilteredRequest
    {
        public IEnumerable<string>? Filter { get; set; } = Array.Empty<string>();
    }
}

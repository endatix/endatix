using Endatix.Api.Common;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Common;

public class FilteredRequestValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_EmptyFilter_ReturnsError(string filter)
    {
        // Arrange
        var validator = new FilteredRequestValidator();
        var request = new TestFilteredRequest { Filter = [filter] };

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Filter)
            .WithErrorMessage($"Invalid filter '{filter}': Filter cannot be empty");
    }

    [Fact]
    public void Validate_InvalidOperator_ReturnsError()
    {
        // Arrange
        var validator = new FilteredRequestValidator();
        var invalidFilter = "name==test";
        var request = new TestFilteredRequest { Filter = [invalidFilter] };

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Filter)
            .WithErrorMessage($"Invalid filter '{invalidFilter}': Filter must contain one of these operators: !:, >:, <:, :, >, <");
    }

    [Theory]
    [InlineData(":value")]
    [InlineData("field:")]
    [InlineData(":")]
    public void Validate_InvalidFilterFormat_ReturnsError(string filter)
    {
        // Arrange
        var validator = new FilteredRequestValidator();
        var request = new TestFilteredRequest { Filter = [filter] };

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Filter)
            .WithErrorMessage($"Invalid filter '{filter}': Filter must be in format 'field[operator]value'");
    }

    [Fact]
    public void Validate_InvalidFieldName_ReturnsError()
    {
        // Arrange
        var validFields = new Dictionary<string, Type>
        {
            { "name", typeof(string) },
            { "age", typeof(int) }
        };
        var validator = new FilteredRequestValidator(validFields);
        var invalidFilter = "invalid:value";
        var request = new TestFilteredRequest { Filter = [invalidFilter] };

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Filter)
            .WithErrorMessage($"Invalid filter '{invalidFilter}': Invalid field name 'invalid'. Allowed fields: name, age");
    }

    [Theory]
    [InlineData("age:notanumber", "One or more values are not valid for type Int32")]
    [InlineData("age>:abc", "Value is not valid for type Int32")]
    public void Validate_InvalidValueType_ReturnsError(string filter, string expectedError)
    {
        // Arrange
        var validFields = new Dictionary<string, Type>
        {
            { "name", typeof(string) },
            { "age", typeof(int) }
        };
        var validator = new FilteredRequestValidator(validFields);
        var request = new TestFilteredRequest { Filter = [filter] };

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Filter)
            .WithErrorMessage($"Invalid filter '{filter}': {expectedError}");
    }

    [Theory]
    [InlineData("name:john")]
    [InlineData("age:25")]
    [InlineData("age>:18")]
    [InlineData("name:john,jane")]
    public void Validate_ValidFilter_PassesValidation(string filter)
    {
        // Arrange
        var validFields = new Dictionary<string, Type>
        {
            { "name", typeof(string) },
            { "age", typeof(int) }
        };
        var validator = new FilteredRequestValidator(validFields);
        var request = new TestFilteredRequest { Filter = [filter] };

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    private class TestFilteredRequest : IFilteredRequest
    {
        public IEnumerable<string>? Filter { get; set; } = Array.Empty<string>();
    }
}

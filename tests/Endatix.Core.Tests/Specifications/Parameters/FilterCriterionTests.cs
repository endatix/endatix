using System.Linq.Expressions;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Tests.Specifications.Parameters;

public class FilterCriterionTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_EmptyExpression_ThrowsArgumentException(string filterExpression)
    {
        // Act
        var act = () => new FilterCriterion(filterExpression);

        // Assert
        act.Should()
           .Throw<ArgumentException>()
           .WithParameterName("filterExpression");
    }

    [Theory]
    [InlineData(":value")]
    [InlineData("^&")]
    public void Constructor_EmptyField_ThrowsArgumentException(string filterExpression)
    {
        // Act
        var act = () => new FilterCriterion(filterExpression);

        // Assert
        act.Should()
           .Throw<ArgumentException>()
           .WithMessage(ErrorMessages.GetErrorMessage("filterExpression", ErrorType.FilterNoField));
    }

    [Theory]
    [InlineData("field")]
    [InlineData("field=value")]
    [InlineData("field^value")]
    [InlineData("field$:value")]
    [InlineData("field@:value")]
    [InlineData("field-name:value")]
    public void Constructor_InvalidOperator_ThrowsArgumentException(string filterExpression)
    {
        // Act
        var act = () => new FilterCriterion(filterExpression);

        // Assert
        act.Should()
           .Throw<ArgumentException>()
           .WithParameterName("filterExpression")
           .WithMessage("*Filter must have a valid operator*");
    }

    [Theory]
    [InlineData("field:")]
    [InlineData("field:value1,,value2")]
    [InlineData("field:value1, ,value2")]
    public void Constructor_EmptyValues_ThrowsArgumentException(string filterExpression)
    {
        // Act
        var act = () => new FilterCriterion(filterExpression);

        // Assert
        act.Should()
           .Throw<ArgumentException>()
           .WithMessage(ErrorMessages.GetErrorMessage("filterExpression", ErrorType.FilterEmptyValue));
    }

    [Theory]
    [InlineData("age:18", ExpressionType.Equal, "age", new[] { "18" })]
    [InlineData("status!:active", ExpressionType.NotEqual, "status", new[] { "active" })]
    [InlineData("price>:100.35", ExpressionType.GreaterThanOrEqual, "price", new[] { "100.35" })]
    [InlineData("date<:2024-01-01", ExpressionType.LessThanOrEqual, "date", new[] { "2024-01-01" })]
    [InlineData("date<2024-01-01T10:15:25", ExpressionType.LessThan, "date", new[] { "2024-01-01T10:15:25" })]
    [InlineData("date<:2024-01-01T10:15:25", ExpressionType.LessThanOrEqual, "date", new[] { "2024-01-01T10:15:25" })]
    [InlineData("count>5", ExpressionType.GreaterThan, "count", new[] { "5" })]
    [InlineData("amount<10", ExpressionType.LessThan, "amount", new[] { "10" })]
    [InlineData("tags:draft,published", ExpressionType.Equal, "tags", new[] { "draft", "published" })]
    public void Constructor_ValidExpression_CreatesFilterCriterion(
        string filterExpression,
        ExpressionType expectedOperator,
        string expectedField,
        string[] expectedValues)
    {
        // Act
        var filter = new FilterCriterion(filterExpression);

        // Assert
        filter.Should().NotBeNull();
        filter.Field.Should().Be(expectedField);
        filter.Operator.Should().Be(expectedOperator);
        filter.Values.Should().BeEquivalentTo(expectedValues);
    }

    [Fact]
    public void Parse_ValidExpression_ReturnsFilterCriterion()
    {
        // Arrange
        var filterExpression = "status:active,pending";

        // Act
        var filter = FilterCriterion.Parse(filterExpression);

        // Assert
        filter.Should().NotBeNull();
        filter.Field.Should().Be("status");
        filter.Operator.Should().Be(ExpressionType.Equal);
        filter.Values.Should().BeEquivalentTo(["active", "pending"]);
    }
}

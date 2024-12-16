using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Tests.Specifications.Parameters;

public class FilterParametersTests
{
    [Fact]
    public void Constructor_NullFilterExpressions_CreatesEmptyFilterParameters()
    {
        // Arrange
        IEnumerable<string> filterExpressions = null!;

        // Act
        var parameters = new FilterParameters(filterExpressions);

        // Assert
        parameters.Should().NotBeNull();
        parameters.Criteria.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_ValidFilterExpressions_CreatesFilterParameters()
    {
        // Arrange
        var filterExpressions = new[] { "name:test", "age>18" };

        // Act
        var parameters = new FilterParameters(filterExpressions);

        // Assert
        parameters.Should().NotBeNull();
        parameters.Criteria.Should().HaveCount(2);
    }

    [Fact]
    public void AddFilter_NullFilterExpression_ThrowsArgumentException()
    {
        // Arrange
        var parameters = new FilterParameters();
        string filterExpression = null!;

        // Act
        var act = () => parameters.AddFilter(filterExpression);

        // Assert
        var expectedMessage = ErrorMessages.GetErrorMessage(nameof(filterExpression), ErrorType.Null);
        act.Should().Throw<ArgumentException>()
            .WithMessage(expectedMessage);
    }

    [Fact]
    public void AddFilter_WhiteSpaceFilterExpression_ThrowsArgumentException()
    {
        // Arrange
        var parameters = new FilterParameters();
        var filterExpression = "   ";

        // Act
        var act = () => parameters.AddFilter(filterExpression);

        // Assert
        var expectedMessage = ErrorMessages.GetErrorMessage(nameof(filterExpression), ErrorType.Empty);
        act.Should().Throw<ArgumentException>()
            .WithMessage(expectedMessage);
    }

    [Fact]
    public void AddFilter_ValidFilterExpression_AddsCriterion()
    {
        // Arrange
        var parameters = new FilterParameters();
        var filterExpression = "name:test";

        // Act
        parameters.AddFilter(filterExpression);

        // Assert
        parameters.Criteria.Should().HaveCount(1);
    }

    [Fact]
    public void AddFilter_NullCriterion_ThrowsArgumentNullException()
    {
        // Arrange
        var parameters = new FilterParameters();
        FilterCriterion criterion = null!;

        // Act
        var act = () => parameters.AddFilter(criterion);

        // Assert
        var expectedMessage = ErrorMessages.GetErrorMessage(nameof(criterion), ErrorType.Null);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage(expectedMessage);
    }

    [Fact]
    public void AddFilter_ValidCriterion_AddsCriterion()
    {
        // Arrange
        var parameters = new FilterParameters();
        var criterion = FilterCriterion.Parse("name:test");

        // Act
        parameters.AddFilter(criterion);

        // Assert
        parameters.Criteria.Should().HaveCount(1);
        parameters.Criteria.Should().Contain(criterion);
    }

    [Fact]
    public void Clear_WithExistingCriteria_RemovesAllCriteria()
    {
        // Arrange
        var parameters = new FilterParameters();
        var criterion = FilterCriterion.Parse("name:test");
        parameters.AddFilter(criterion);

        // Act
        parameters.Clear();

        // Assert
        parameters.Criteria.Should().BeEmpty();
    }
}

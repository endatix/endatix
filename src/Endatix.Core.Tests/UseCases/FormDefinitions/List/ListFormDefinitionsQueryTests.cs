using Endatix.Core.UseCases.FormDefinitions.List;
using FluentAssertions;

namespace Endatix.Core.UseCases.Tests.FormDefinitions.List;

public class ListFormDefinitionsQueryTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateQuery()
    {
        // Arrange
        var formId = 1;
        int? page = 1;
        int? pageSize = 10;

        // Act
        var query = new ListFormDefinitionsQuery(formId, page, pageSize);

        // Assert
        query.FormId.Should().Be(formId);
        query.Page.Should().Be(page);
        query.PageSize.Should().Be(pageSize);
    }

    [Fact]
    public void Constructor_WithNegativeOrZeroFormId_ShouldThrowArgumentException()
    {
        // Arrange
        var formId = 0;
        int? page = 1;
        int? pageSize = 10;

        // Act
        Action act = () => new ListFormDefinitionsQuery(formId, page, pageSize);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Required input formId cannot be zero or negative. (Parameter 'formId')");
    }
}

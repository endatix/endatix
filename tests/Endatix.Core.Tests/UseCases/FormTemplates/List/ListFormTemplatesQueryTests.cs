using Endatix.Core.UseCases.FormTemplates.List;

namespace Endatix.Core.Tests.UseCases.FormTemplates.List;

public class ListFormTemplatesQueryTests
{
    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        int? page = 1;
        int? pageSize = 10;

        // Act
        var query = new ListFormTemplatesQuery(page, pageSize);

        // Assert
        query.Page.Should().Be(page);
        query.PageSize.Should().Be(pageSize);
    }

    [Fact]
    public void Constructor_NullParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        int? page = null;
        int? pageSize = null;

        // Act
        var query = new ListFormTemplatesQuery(page, pageSize);

        // Assert
        query.Page.Should().BeNull();
        query.PageSize.Should().BeNull();
    }
} 
using Endatix.Core.UseCases.Forms.List;
using FluentAssertions;

namespace Endatix.Core.UseCases.Tests.Forms.List;

public class ListFormsQueryTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateQuery()
    {
        // Arrange
        int? page = 1;
        int? pageSize = 10;

        // Act
        var query = new ListFormsQuery(page, pageSize);

        // Assert
        query.Page.Should().Be(page);
        query.PageSize.Should().Be(pageSize);
    }

    [Fact]
    public void Constructor_WithNullParameters_ShouldCreateQueryWithNullValues()
    {
        // Arrange
        int? page = null;
        int? pageSize = null;

        // Act
        var query = new ListFormsQuery(page, pageSize);

        // Assert
        query.Page.Should().BeNull();
        query.PageSize.Should().BeNull();
    }
}

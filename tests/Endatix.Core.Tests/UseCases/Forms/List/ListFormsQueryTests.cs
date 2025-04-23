using Endatix.Core.UseCases.Forms.List;
using FluentAssertions;

namespace Endatix.Core.Tests.UseCases.Forms.List;

public class ListFormsQueryTests
{
    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        int? page = 1;
        int? pageSize = 10;
        IEnumerable<string>? filter = ["name:form1"];

        // Act
        var query = new ListFormsQuery(page, pageSize, filter);

        // Assert
        query.Page.Should().Be(page);
        query.PageSize.Should().Be(pageSize);
        query.FilterExpressions.Should().BeEquivalentTo(filter);
    }

    [Fact]
    public void Constructor_NullPageAndPageSize_SetsPropertiesCorrectly()
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

    [Fact]
    public void Constructor_NullFilter_SetsPropertiesCorrectly()
    {
        // Arrange
        int? page = 1;
        int? pageSize = 10;
        IEnumerable<string>? filter = null;

        // Act
        var query = new ListFormsQuery(page, pageSize, filter);

        // Assert
        query.Page.Should().Be(page);
        query.PageSize.Should().Be(pageSize);
        query.FilterExpressions.Should().BeNull();
    }
}

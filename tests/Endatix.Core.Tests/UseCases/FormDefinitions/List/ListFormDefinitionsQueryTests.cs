using FluentAssertions;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;
using Endatix.Core.UseCases.FormDefinitions.List;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.List;

public class ListFormDefinitionsQueryTests
{
    [Fact]
    public void Constructor_NegativeOrZeroFormId_ThrowsArgumentException()
    {
        // Arrange
        var formId = -1;
        int? page = 1;
        int? pageSize = 10;

        // Act
        Action act = () => new ListFormDefinitionsQuery(formId, page, pageSize);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(formId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
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
}

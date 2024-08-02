using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.FormDefinitions.List;
using FluentAssertions;
using Moq;

namespace Endatix.Core.UseCases.Tests.FormDefinitions.List;

public class ListFormDefinitionsHandlerTests
{
    private readonly Mock<IRepository<FormDefinition>> _repositoryMock;
    private readonly ListFormDefinitionsHandler _handler;

    public ListFormDefinitionsHandlerTests()
    {
        _repositoryMock = new Mock<IRepository<FormDefinition>>();
        _handler = new ListFormDefinitionsHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidQuery_ShouldReturnSuccessResult()
    {
        // Arrange
        var formDefinitions = new List<FormDefinition>
        {
            new FormDefinition(true, "{\"key\":\"value1\"}", true) { FormId = 1 },
            new FormDefinition(true, "{\"key\":\"value2\"}", true) { FormId = 1 }
        };

        var query = new ListFormDefinitionsQuery(1, 1, 10);
        _repositoryMock.Setup(x => x.ListAsync(It.IsAny<FormDefinitionsByFormIdSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(formDefinitions);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().BeEquivalentTo(formDefinitions);
    }
}

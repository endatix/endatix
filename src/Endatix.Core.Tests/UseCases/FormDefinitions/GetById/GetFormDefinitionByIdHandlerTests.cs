using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormDefinitions.GetById;
using FluentAssertions;
using Moq;

namespace Endatix.Core.UseCases.Tests.FormDefinitions.GetById;

public class GetFormDefinitionByIdHandlerTests
{
    private readonly Mock<IRepository<FormDefinition>> _repositoryMock;
    private readonly GetFormDefinitionByIdHandler _handler;

    public GetFormDefinitionByIdHandlerTests()
    {
        _repositoryMock = new Mock<IRepository<FormDefinition>>();
        _handler = new GetFormDefinitionByIdHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_FormDefinitionNotFound_ShouldReturnNotFoundResult()
    {
        // Arrange
        var query = new GetFormDefinitionByIdQuery(1, 1);
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FormDefinition)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form definition not found.");
    }

    [Fact]
    public async Task Handle_FormDefinitionFoundWithDifferentFormId_ShouldReturnNotFoundResult()
    {
        // Arrange
        var formDefinition = new FormDefinition(true, "{\"key\":\"value\"}", true) { FormId = 2 };
        var query = new GetFormDefinitionByIdQuery(1, 1);
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(formDefinition);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form definition not found.");
    }

    [Fact]
    public async Task Handle_FormDefinitionFound_ShouldReturnSuccessResult()
    {
        // Arrange
        var formDefinition = new FormDefinition(true, "{\"key\":\"value\"}", true) { FormId = 1 };
        var query = new GetFormDefinitionByIdQuery(1, 1);
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(formDefinition);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().Be(formDefinition);
    }
}

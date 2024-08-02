using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormDefinitions.Update;
using FluentAssertions;
using Moq;

namespace Endatix.Core.UseCases.Tests.FormDefinitions.Update;

public class UpdateFormDefinitionHandlerTests
{
    private readonly Mock<IRepository<FormDefinition>> _repositoryMock;
    private readonly UpdateFormDefinitionHandler _handler;

    public UpdateFormDefinitionHandlerTests()
    {
        _repositoryMock = new Mock<IRepository<FormDefinition>>();
        _handler = new UpdateFormDefinitionHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_FormDefinitionNotFound_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new UpdateFormDefinitionCommand(1, 1, true, "{\"key\":\"value\"}", true);
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FormDefinition)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form definition not found.");
    }

    [Fact]
    public async Task Handle_FormDefinitionFoundWithDifferentFormId_ShouldReturnNotFoundResult()
    {
        // Arrange
        var formDefinition = new FormDefinition(true, "{\"key\":\"value\"}", true) { FormId = 2 };
        var command = new UpdateFormDefinitionCommand(1, 1, true, "{\"key\":\"value\"}", true);
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(formDefinition);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form definition not found.");
    }

    [Fact]
    public async Task Handle_FormDefinitionFound_ShouldUpdateAndReturnSuccessResult()
    {
        // Arrange
        var formDefinition = new FormDefinition(true, "{\"key\":\"oldValue\"}", true) { FormId = 1 };
        var command = new UpdateFormDefinitionCommand(1, 1, false, "{\"key\":\"newValue\"}", false);
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(formDefinition);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.IsDraft.Should().Be(false);
        result.Value.JsonData.Should().Be("{\"key\":\"newValue\"}");
        result.Value.IsActive.Should().Be(false);

        _repositoryMock.Verify(x => x.UpdateAsync(It.IsAny<FormDefinition>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.FormDefinitions.UpdateActive;
using FluentAssertions;
using Moq;
using Xunit;

namespace Endatix.Core.UseCases.Tests.FormDefinitions.UpdateActive;

public class UpdateActiveFormDefinitionHandlerTests
{
    private readonly Mock<IRepository<FormDefinition>> _repositoryMock;
    private readonly UpdateActiveFormDefinitionHandler _handler;

    public UpdateActiveFormDefinitionHandlerTests()
    {
        _repositoryMock = new Mock<IRepository<FormDefinition>>();
        _handler = new UpdateActiveFormDefinitionHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ActiveFormDefinitionNotFound_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new UpdateActiveFormDefinitionCommand(1, true, "{\"key\":\"value\"}", true);
        _repositoryMock.Setup(x => x.SingleOrDefaultAsync(It.IsAny<ActiveFormDefinitionByFormIdSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FormDefinition)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Active form definition not found.");
    }

    [Fact]
    public async Task Handle_ActiveFormDefinitionFound_ShouldUpdateAndReturnSuccessResult()
    {
        // Arrange
        var formDefinition = new FormDefinition(true, "{\"key\":\"oldValue\"}", true) { FormId = 1 };
        var command = new UpdateActiveFormDefinitionCommand(1, false, "{\"key\":\"newValue\"}", false);
        _repositoryMock.Setup(x => x.SingleOrDefaultAsync(It.IsAny<ActiveFormDefinitionByFormIdSpec>(), It.IsAny<CancellationToken>()))
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

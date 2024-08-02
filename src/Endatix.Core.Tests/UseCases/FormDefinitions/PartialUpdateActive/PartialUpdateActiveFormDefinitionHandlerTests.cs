using System.Threading;
using System.Threading.Tasks;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.FormDefinitions.PartialUpdateActive;
using FluentAssertions;
using Moq;
using Xunit;

namespace Endatix.Core.UseCases.Tests.FormDefinitions.PartialUpdateActive;

public class PartialUpdateActiveFormDefinitionHandlerTests
{
    private readonly Mock<IRepository<FormDefinition>> _repositoryMock;
    private readonly PartialUpdateActiveFormDefinitionHandler _handler;

    public PartialUpdateActiveFormDefinitionHandlerTests()
    {
        _repositoryMock = new Mock<IRepository<FormDefinition>>();
        _handler = new PartialUpdateActiveFormDefinitionHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ActiveFormDefinitionNotFound_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new PartialUpdateActiveFormDefinitionCommand(1, true, "{\"key\":\"value\"}", true);
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
        var command = new PartialUpdateActiveFormDefinitionCommand(1, false, "{\"key\":\"newValue\"}", false);
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

    [Fact]
    public async Task Handle_NullRequestFields_ShouldPreserveOldValues()
    {
        // Arrange
        var formDefinition = new FormDefinition(true, "{\"key\":\"oldValue\"}", true) { FormId = 1 };
        var command = new PartialUpdateActiveFormDefinitionCommand(1, null, null, null);
        _repositoryMock.Setup(x => x.SingleOrDefaultAsync(It.IsAny<ActiveFormDefinitionByFormIdSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(formDefinition);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.IsDraft.Should().Be(true); // Old value preserved
        result.Value.JsonData.Should().Be("{\"key\":\"oldValue\"}"); // Old value preserved
        result.Value.IsActive.Should().Be(true); // Old value preserved

        _repositoryMock.Verify(x => x.UpdateAsync(It.IsAny<FormDefinition>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

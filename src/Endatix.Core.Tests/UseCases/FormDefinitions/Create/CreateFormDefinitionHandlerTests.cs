using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormDefinitions.Create;
using FluentAssertions;
using Moq;

namespace Endatix.Core.UseCases.Tests.FormDefinitions.Create;

public class CreateFormDefinitionHandlerTests
{
    private readonly Mock<IRepository<FormDefinition>> _definitionsRepositoryMock;
    private readonly Mock<IRepository<Form>> _formsRepositoryMock;
    private readonly CreateFormDefinitionHandler _handler;

    public CreateFormDefinitionHandlerTests()
    {
        _definitionsRepositoryMock = new Mock<IRepository<FormDefinition>>();
        _formsRepositoryMock = new Mock<IRepository<Form>>();
        _handler = new CreateFormDefinitionHandler(_definitionsRepositoryMock.Object, _formsRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_FormNotFound_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new CreateFormDefinitionCommand(1, true, "{\"key\":\"value\"}", true);
        _formsRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Form)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form not found.");
    }

    [Fact]
    public async Task Handle_FormFound_ShouldCreateFormDefinition()
    {
        // Arrange
        var form = new Form();
        var command = new CreateFormDefinitionCommand(1, true, "{\"key\":\"value\"}", true);
        _formsRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(form);
        _definitionsRepositoryMock.Setup(x => x.AddAsync(It.IsAny<FormDefinition>(), It.IsAny<CancellationToken>()))
            .Verifiable();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Should().NotBeNull();
        result.Value.FormId.Should().Be(command.FormId);
        result.Value.IsDraft.Should().Be(command.IsDraft);
        result.Value.JsonData.Should().Be(command.JsonData);
        result.Value.IsActive.Should().Be(command.IsActive);

        _definitionsRepositoryMock.Verify(x => x.AddAsync(It.IsAny<FormDefinition>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

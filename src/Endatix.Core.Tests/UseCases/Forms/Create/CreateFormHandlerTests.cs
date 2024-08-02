using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Forms.Create;
using FluentAssertions;
using Moq;

namespace Endatix.Core.UseCases.Tests.Forms.Create;

public class CreateFormHandlerTests
{
    private readonly Mock<IRepository<Form>> _repositoryMock;
    private readonly CreateFormHandler _handler;

    public CreateFormHandlerTests()
    {
        _repositoryMock = new Mock<IRepository<Form>>();
        _handler = new CreateFormHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateAndReturnForm()
    {
        // Arrange
        var command = new CreateFormCommand("Test Form", "Test Description", true, "{\"key\":\"value\"}");
        var form = new Form("Test Form", "Test Description", true, "{\"key\":\"value\"}");
        _repositoryMock.Setup(x => x.AddAsync(It.IsAny<Form>(), It.IsAny<CancellationToken>()))
            .Verifiable();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(command.Name);
        result.Value.Description.Should().Be(command.Description);
        result.Value.IsEnabled.Should().Be(command.IsEnabled);

        _repositoryMock.Verify(x => x.AddAsync(It.IsAny<Form>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

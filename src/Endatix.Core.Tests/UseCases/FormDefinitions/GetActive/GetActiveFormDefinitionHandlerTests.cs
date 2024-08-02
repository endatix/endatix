using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.FormDefinitions.GetActive;
using FluentAssertions;
using Moq;

namespace Endatix.Core.UseCases.Tests.FormDefinitions.GetActive;

public class GetActiveFormDefinitionHandlerTests
{
    private readonly Mock<IRepository<FormDefinition>> _repositoryMock;
    private readonly GetActiveFormDefinitionHandler _handler;

    public GetActiveFormDefinitionHandlerTests()
    {
        _repositoryMock = new Mock<IRepository<FormDefinition>>();
        _handler = new GetActiveFormDefinitionHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ActiveFormDefinitionNotFound_ShouldReturnNotFoundResult()
    {
        // Arrange
        var query = new GetActiveFormDefinitionQuery(1);
        _repositoryMock.Setup(x => x.SingleOrDefaultAsync(It.IsAny<ActiveFormDefinitionByFormIdSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FormDefinition)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Active form definition not found.");
    }

    [Fact]
    public async Task Handle_ActiveFormDefinitionFound_ShouldReturnSuccessResult()
    {
        // Arrange
        var formDefinition = new FormDefinition(true, "{\"key\":\"value\"}", true) { FormId = 1 };
        var query = new GetActiveFormDefinitionQuery(1);
        _repositoryMock.Setup(x => x.SingleOrDefaultAsync(It.IsAny<ActiveFormDefinitionByFormIdSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(formDefinition);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().Be(formDefinition);
    }
}

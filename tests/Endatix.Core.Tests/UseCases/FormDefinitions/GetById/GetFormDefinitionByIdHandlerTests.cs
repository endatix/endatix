using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormDefinitions.GetById;
using FluentAssertions;
using NSubstitute;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.GetById;

public class GetFormDefinitionByIdHandlerTests
{
    private readonly IRepository<FormDefinition> _repository;
    private readonly GetFormDefinitionByIdHandler _handler;

    public GetFormDefinitionByIdHandlerTests()
    {
        _repository = Substitute.For<IRepository<FormDefinition>>();
        _handler = new GetFormDefinitionByIdHandler(_repository);
    }

    [Fact]
    public async Task Handle_FormDefinitionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        FormDefinition? notFoundFormDefinition = null;
        var request = new GetFormDefinitionByIdQuery(1, 1);
        _repository.GetByIdAsync(request.DefinitionId, Arg.Any<CancellationToken>())
                   .Returns(notFoundFormDefinition);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form definition not found.");
    }

    [Fact]
    public async Task Handle_FormIdMismatch_ReturnsNotFoundResult()
    {
        // Arrange
        var formDefinitionIdToReturn = 123;
        var testForm = new Form(SampleData.FORM_NAME_1) { Id = 1 };
        var formDefinition = new FormDefinition(testForm, true, SampleData.FORM_DEFINITION_JSON_DATA_1, true){
            Id = formDefinitionIdToReturn
        };
        var request = new GetFormDefinitionByIdQuery(456, 457);
        _repository.GetByIdAsync(request.DefinitionId, Arg.Any<CancellationToken>())
                   .Returns(formDefinition);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form definition not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsFormDefinition()
    {
        // Arrange
        var testForm = new Form("Test Form") { Id = 1 };
        var formDefinition = new FormDefinition(testForm, true, SampleData.FORM_DEFINITION_JSON_DATA_1, true);
        var request = new GetFormDefinitionByIdQuery(1, 1);
        _repository.GetByIdAsync(request.DefinitionId, Arg.Any<CancellationToken>())
                   .Returns(formDefinition);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().Be(formDefinition);
    }
}

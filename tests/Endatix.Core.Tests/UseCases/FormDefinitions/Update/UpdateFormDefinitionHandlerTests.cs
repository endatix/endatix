using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormDefinitions.Update;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.Update;

public class UpdateFormDefinitionHandlerTests
{
    private readonly IRepository<FormDefinition> _repository;
    private readonly UpdateFormDefinitionHandler _handler;

    public UpdateFormDefinitionHandlerTests()
    {
        _repository = Substitute.For<IRepository<FormDefinition>>();
        _handler = new UpdateFormDefinitionHandler(_repository);
    }

    [Fact]
    public async Task Handle_FormDefinitionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new UpdateFormDefinitionCommand(1, 1, true, SampleData.FORM_DEFINITION_JSON_DATA_1, true);
        _repository.GetByIdAsync(request.DefinitionId, Arg.Any<CancellationToken>())
                   .Returns((FormDefinition)null);

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
        var notFoundFormId = 2;
        var notFoundFormDefinitionId = 2;
        var request = new UpdateFormDefinitionCommand(notFoundFormId, notFoundFormDefinitionId, true, SampleData.FORM_DEFINITION_JSON_DATA_1, true);

        var testForm = new Form(SampleData.FORM_NAME_1) { Id = 123 };
        var formDefinition = new FormDefinition(testForm, true, SampleData.FORM_DEFINITION_JSON_DATA_1, true){
            Id = 456
        };

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
    public async Task Handle_ValidRequest_UpdatesFormDefinition()
    {
        // Arrange
        var form = new Form(SampleData.FORM_NAME_1) { Id = 1 };
        var formDefinition = new FormDefinition(form, true, SampleData.FORM_DEFINITION_JSON_DATA_1, true);
        var request = new UpdateFormDefinitionCommand(1, 1, false, SampleData.FORM_DEFINITION_JSON_DATA_2, false);
        _repository.GetByIdAsync(request.DefinitionId, Arg.Any<CancellationToken>())
                   .Returns(formDefinition);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.IsDraft.Should().Be(request.IsDraft);
        result.Value.JsonData.Should().Be(request.JsonData);
        result.Value.IsActive.Should().Be(request.IsActive);
        await _repository.Received(1).UpdateAsync(formDefinition, Arg.Any<CancellationToken>());
    }
}

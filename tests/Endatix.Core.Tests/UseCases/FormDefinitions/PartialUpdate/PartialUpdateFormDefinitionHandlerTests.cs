using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormDefinitions.PartialUpdate;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.PartialUpdate;

public class PartialUpdateFormDefinitionHandlerTests
{
    private readonly IRepository<FormDefinition> _repository;
    private readonly PartialUpdateFormDefinitionHandler _handler;

    public PartialUpdateFormDefinitionHandlerTests()
    {
        _repository = Substitute.For<IRepository<FormDefinition>>();
        _handler = new PartialUpdateFormDefinitionHandler(_repository);
    }

    [Fact]
    public async Task Handle_FormDefinitionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        FormDefinition? notFoundFormDefinition = null;
        var request = new PartialUpdateFormDefinitionCommand(1, 1, null, null, null);
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
        var nonExistingFormId = 2;
        var form = new Form(SampleData.FORM_NAME_1) { Id = 1 };
        var formDefinition = new FormDefinition(form, true, SampleData.FORM_DEFINITION_JSON_DATA_1, true);
        var request = new PartialUpdateFormDefinitionCommand(nonExistingFormId, 1, null, null, null);
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
        var testForm = new Form("Test Form") { Id = 1 };
        var formDefinition = new FormDefinition(testForm, true, SampleData.FORM_DEFINITION_JSON_DATA_1, true);
        var request = new PartialUpdateFormDefinitionCommand(1, 1, false, SampleData.FORM_DEFINITION_JSON_DATA_2, false);
        _repository.GetByIdAsync(request.DefinitionId, Arg.Any<CancellationToken>())
                   .Returns(formDefinition);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.IsDraft.Should().Be(request.IsDraft!.Value);
        result.Value.JsonData.Should().Be(request.JsonData);
        result.Value.IsActive.Should().Be(request.IsActive!.Value);
        await _repository.Received(1).UpdateAsync(formDefinition, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PartialUpdate_UpdatesOnlySpecifiedFields()
    {
        // Arrange
        var form = new Form("Test Form"){
            Id = 1
        };
        var formDefinition = new FormDefinition(form, true, SampleData.FORM_DEFINITION_JSON_DATA_1, true);
        var request = new PartialUpdateFormDefinitionCommand(1, 1, null, SampleData.FORM_DEFINITION_JSON_DATA_2, null);
        _repository.GetByIdAsync(request.DefinitionId, Arg.Any<CancellationToken>())
                   .Returns(formDefinition);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.IsDraft.Should().Be(formDefinition.IsDraft);
        result.Value.JsonData.Should().Be(request.JsonData);
        result.Value.IsActive.Should().Be(formDefinition.IsActive);
        await _repository.Received(1).UpdateAsync(formDefinition, Arg.Any<CancellationToken>());
    }
}

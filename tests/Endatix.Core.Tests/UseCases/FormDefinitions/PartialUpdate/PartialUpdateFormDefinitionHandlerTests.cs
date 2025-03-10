using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Tests.TestUtils;
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
        var request = new PartialUpdateFormDefinitionCommand(1, 1, null, null);
        _repository.GetByIdAsync(
            request.DefinitionId,
            cancellationToken: Arg.Any<CancellationToken>()
        ).Returns(notFoundFormDefinition);

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
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1) { Id = 1 };
        var formDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        form.AddFormDefinition(formDefinition);
        var request = new PartialUpdateFormDefinitionCommand(nonExistingFormId, 1, null, null);
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
        var testForm = new Form(SampleData.TENANT_ID, "Test Form") { Id = 1 };
        var formDefinition = FormDefinitionFactory.CreateForTesting(
            jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1,
            formId: 1,
            formDefinitionId: 2
        );

        testForm.AddFormDefinition(formDefinition);
        var request = new PartialUpdateFormDefinitionCommand(1, 1, false, SampleData.FORM_DEFINITION_JSON_DATA_2);
        _repository.GetByIdAsync(
            request.DefinitionId,
            cancellationToken: Arg.Any<CancellationToken>()
        ).Returns(formDefinition);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        var actualResult = result.Value;
        actualResult.IsDraft.Should().Be(request.IsDraft!.Value);
        actualResult.JsonData.Should().Be(request.JsonData);
        actualResult.Id.Should().Be(2);
        actualResult.FormId.Should().Be(1);
        await _repository.Received(1).UpdateAsync(formDefinition, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PartialUpdate_UpdatesOnlySpecifiedFields()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, "Test Form")
        {
            Id = 1
        };
         var formDefinition = FormDefinitionFactory.CreateForTesting(
            jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1,
            formId: 1,
            formDefinitionId: 2
        );
        var request = new PartialUpdateFormDefinitionCommand(1, 1, null, SampleData.FORM_DEFINITION_JSON_DATA_2);
        _repository.GetByIdAsync(
            request.DefinitionId,
            cancellationToken: Arg.Any<CancellationToken>()
        ).Returns(formDefinition);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.IsDraft.Should().Be(formDefinition.IsDraft);
        result.Value.JsonData.Should().Be(request.JsonData);
        await _repository.Received(1).UpdateAsync(formDefinition, Arg.Any<CancellationToken>());
    }
}

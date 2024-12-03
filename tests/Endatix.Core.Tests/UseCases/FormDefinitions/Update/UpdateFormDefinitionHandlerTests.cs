using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Tests.TestUtils;
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
        FormDefinition? notFoundFormDefinition = null;
        var request = new UpdateFormDefinitionCommand(1, 1, true, SampleData.FORM_DEFINITION_JSON_DATA_1);
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
        var notFoundFormId = 2;
        var notFoundFormDefinitionId = 2;
        var request = new UpdateFormDefinitionCommand(notFoundFormId, notFoundFormDefinitionId, true, SampleData.FORM_DEFINITION_JSON_DATA_1);

        var form = new Form(SampleData.FORM_NAME_1) { Id = 123 };
        var formDefinition = new FormDefinition(jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1)
        {
            Id = 456
        };
        form.AddFormDefinition(formDefinition);

        _repository.GetByIdAsync(
            request.DefinitionId,
            cancellationToken: Arg.Any<CancellationToken>()
        ).Returns(formDefinition);

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
        var formDefinition = FormDefinitionFactory.CreateForTesting(
            jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1,
            formId: 1,
            formDefinitionId: 2
        );
        form.AddFormDefinition(formDefinition);
        var request = new UpdateFormDefinitionCommand(1, 1, false, SampleData.FORM_DEFINITION_JSON_DATA_2);
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
        result.Value.IsDraft.Should().Be(request.IsDraft);
        result.Value.JsonData.Should().Be(request.JsonData);
        await _repository.Received(1).UpdateAsync(formDefinition, Arg.Any<CancellationToken>());
    }
}

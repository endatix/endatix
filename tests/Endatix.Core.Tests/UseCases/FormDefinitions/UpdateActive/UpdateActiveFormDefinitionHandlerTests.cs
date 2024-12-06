using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.FormDefinitions.UpdateActive;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.UpdateActive;

public class UpdateActiveFormDefinitionHandlerTests
{
    private readonly IFormsRepository _formsRepository;
    private readonly UpdateActiveFormDefinitionHandler _handler;

    public UpdateActiveFormDefinitionHandlerTests()
    {
        _formsRepository = Substitute.For<IFormsRepository>();
        _handler = new UpdateActiveFormDefinitionHandler(_formsRepository);
    }

    [Fact]
    public async Task Handle_FormDefinitionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new UpdateActiveFormDefinitionCommand(1, true, SampleData.FORM_DEFINITION_JSON_DATA_1);
        var activeFormDefinitionByFormIdSpec = new ActiveFormDefinitionByFormIdSpec(request.FormId);
        _formsRepository.SingleOrDefaultAsync(
            activeFormDefinitionByFormIdSpec,
            cancellationToken: Arg.Any<CancellationToken>()
        ).Returns(Task.FromResult<Form?>(null));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Active form definition not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_UpdatesFormDefinition()
    {
        // Arrange
        var form = new Form(SampleData.FORM_NAME_1);
        var formDefinition = new FormDefinition(jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1)
        {
            Id = 1
        };
        form.AddFormDefinition(formDefinition);

        var request = new UpdateActiveFormDefinitionCommand(1, false, SampleData.FORM_DEFINITION_JSON_DATA_2);

        _formsRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            Arg.Any<CancellationToken>()
        ).Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.IsDraft.Should().Be(request.IsDraft);
        result.Value.JsonData.Should().Be(request.JsonData);
        await _formsRepository.Received(1).UpdateAsync(form, Arg.Any<CancellationToken>());
    }
}

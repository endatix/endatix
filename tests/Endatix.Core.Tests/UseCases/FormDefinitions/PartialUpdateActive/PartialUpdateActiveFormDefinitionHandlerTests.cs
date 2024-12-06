using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.FormDefinitions.PartialUpdateActive;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.PartialUpdateActive;

public class PartialUpdateActiveFormDefinitionHandlerTests
{
    private readonly IFormsRepository _formRepository;
    private readonly PartialUpdateActiveFormDefinitionHandler _handler;

    public PartialUpdateActiveFormDefinitionHandlerTests()
    {
        _formRepository = Substitute.For<IFormsRepository>();
        _handler = new PartialUpdateActiveFormDefinitionHandler(_formRepository);
    }

    [Fact]
    public async Task Handle_FormDefinitionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new PartialUpdateActiveFormDefinitionCommand(1, null, null);
        _formRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            Arg.Any<CancellationToken>()
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
        var form = new Form("Test Form") { Id = 1 };
        var formDefinition = new FormDefinition(jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        form.AddFormDefinition(formDefinition);

        var request = new PartialUpdateActiveFormDefinitionCommand(1, false, SampleData.FORM_DEFINITION_JSON_DATA_2);
        _formRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            Arg.Any<CancellationToken>()
        ).Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.IsDraft.Should().Be(request.IsDraft!.Value);
        result.Value.JsonData.Should().Be(request.JsonData);
        await _formRepository.Received(1).UpdateAsync(form, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PartialUpdate_UpdatesOnlySpecifiedFields()
    {
        // Arrange
        var form = new Form("Test Form") { Id = 1 };
        var formDefinition = new FormDefinition(jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        form.AddFormDefinition(formDefinition);

        var request = new PartialUpdateActiveFormDefinitionCommand(1, null, SampleData.FORM_DEFINITION_JSON_DATA_2);
        _formRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            Arg.Any<CancellationToken>()
        ).Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.IsDraft.Should().Be(formDefinition.IsDraft);
        result.Value.JsonData.Should().Be(request.JsonData);

        await _formRepository.Received(1).UpdateAsync(form, Arg.Any<CancellationToken>());
    }
}

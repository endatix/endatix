using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.FormDefinitions.PartialUpdateActive;
using FluentAssertions;
using NSubstitute;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.PartialUpdateActive;

public class PartialUpdateActiveFormDefinitionHandlerTests
{
    private readonly IRepository<FormDefinition> _repository;
    private readonly PartialUpdateActiveFormDefinitionHandler _handler;

    public PartialUpdateActiveFormDefinitionHandlerTests()
    {
        _repository = Substitute.For<IRepository<FormDefinition>>();
        _handler = new PartialUpdateActiveFormDefinitionHandler(_repository);
    }

    [Fact]
    public async Task Handle_FormDefinitionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new PartialUpdateActiveFormDefinitionCommand(1, null, null, null);
        _repository.SingleOrDefaultAsync(Arg.Any<ActiveFormDefinitionByFormIdSpec>(), Arg.Any<CancellationToken>())
                   .Returns((FormDefinition)null);

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
        var formDefinition = new FormDefinition(true, SampleData.FORM_DEFINITION_JSON_DATA_1, true) { FormId = 1 };
        var request = new PartialUpdateActiveFormDefinitionCommand(1, false, SampleData.FORM_DEFINITION_JSON_DATA_2, false);
        _repository.SingleOrDefaultAsync(Arg.Any<ActiveFormDefinitionByFormIdSpec>(), Arg.Any<CancellationToken>())
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
        var formDefinition = new FormDefinition(true, SampleData.FORM_DEFINITION_JSON_DATA_1, true) { FormId = 1 };
        var request = new PartialUpdateActiveFormDefinitionCommand(1, null, SampleData.FORM_DEFINITION_JSON_DATA_2, null);
        _repository.SingleOrDefaultAsync(Arg.Any<ActiveFormDefinitionByFormIdSpec>(), Arg.Any<CancellationToken>())
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

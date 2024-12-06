using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Tests.TestUtils;
using Endatix.Core.UseCases.FormDefinitions.GetById;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.GetById;

public class GetFormDefinitionByIdHandlerTests
{
    private readonly IFormsRepository _repository;
    private readonly GetFormDefinitionByIdHandler _handler;

    public GetFormDefinitionByIdHandlerTests()
    {
        _repository = Substitute.For<IFormsRepository>();
        _handler = new GetFormDefinitionByIdHandler(_repository);
    }

    [Fact]
    public async Task Handle_FormDefinitionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        FormDefinition? notFoundFormDefinition = null;
        var request = new GetFormDefinitionByIdQuery(1, 1);
        _repository.SingleOrDefaultAsync(
            Arg.Any<DefinitionByFormAndDefinitionIdSpec>(),
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
        var formDefinitionIdToReturn = 123;
        var form = new Form(SampleData.FORM_NAME_1) { Id = 1 };
        var formDefinition = FormDefinitionFactory.CreateForTesting(
            isDraft: false,
            jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1,
            formId: 1,
            formDefinitionId: formDefinitionIdToReturn
        );
        form.AddFormDefinition(formDefinition);

        var request = new GetFormDefinitionByIdQuery(456, 457);
        _repository.SingleOrDefaultAsync(
            Arg.Any<DefinitionByFormAndDefinitionIdSpec>(),
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
    public async Task Handle_ValidRequest_ReturnsFormDefinition()
    {
        // Arrange
        var form = new Form("Test Form") { Id = 1 };
        var formDefinition = FormDefinitionFactory.CreateForTesting(
            isDraft: false,
            jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1,
            formId: 1
        );
        form.AddFormDefinition(formDefinition);
        var request = new GetFormDefinitionByIdQuery(1, 1);
        _repository.SingleOrDefaultAsync(
            Arg.Any<DefinitionByFormAndDefinitionIdSpec>(),
            cancellationToken: Arg.Any<CancellationToken>()
        ).Returns(formDefinition);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.FormId.Should().Be(formDefinition.FormId);
        result.Value.Should().Be(formDefinition);
    }
}

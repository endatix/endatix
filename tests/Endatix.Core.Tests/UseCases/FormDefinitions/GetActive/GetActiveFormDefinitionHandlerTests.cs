using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.FormDefinitions.GetActive;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.GetActive;

public class GetActiveFormDefinitionHandlerTests
{
    private readonly IFormsRepository _repository;
    private readonly GetActiveFormDefinitionHandler _handler;

    public GetActiveFormDefinitionHandlerTests()
    {
        _repository = Substitute.For<IFormsRepository>();
        _handler = new GetActiveFormDefinitionHandler(_repository);
    }

    [Fact]
    public async Task Handle_FormDefinitionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new GetActiveFormDefinitionQuery(1);
        _repository.SingleOrDefaultAsync(Arg.Any<ActiveFormDefinitionByFormIdSpec>(), Arg.Any<CancellationToken>())
                   .Returns(Task.FromResult<Form?>(null));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Active form definition not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsActiveFormDefinition()
    {
        // Arrange
        var formWithActiveDefinition = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1)
        {
            Id = 1
        };
        var activeDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        formWithActiveDefinition.AddFormDefinition(activeDefinition);

        var request = new GetActiveFormDefinitionQuery(1);

        _repository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            CancellationToken.None
        ).Returns(formWithActiveDefinition);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(activeDefinition.Id);
    }
}

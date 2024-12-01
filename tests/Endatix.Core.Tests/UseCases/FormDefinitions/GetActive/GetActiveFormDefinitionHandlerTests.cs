using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.FormDefinitions.GetActive;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.GetActive;

public class GetActiveFormDefinitionHandlerTests
{
    private readonly IRepository<FormDefinition> _repository;
    private readonly GetActiveFormDefinitionHandler _handler;

    public GetActiveFormDefinitionHandlerTests()
    {
        _repository = Substitute.For<IRepository<FormDefinition>>();
        _handler = new GetActiveFormDefinitionHandler(_repository);
    }

    [Fact]
    public async Task Handle_FormDefinitionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new GetActiveFormDefinitionQuery(1);
        FormDefinition? notFoundFormDefinition = null;
        _repository.SingleOrDefaultAsync(Arg.Any<ActiveFormDefinitionByFormIdSpec>(), Arg.Any<CancellationToken>())
                   .Returns(notFoundFormDefinition);

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
        var form = new Form(SampleData.FORM_NAME_1);
        var formDefinition = new FormDefinition(form, false, SampleData.FORM_DEFINITION_JSON_DATA_1, true);
        var request = new GetActiveFormDefinitionQuery(1);
        _repository.SingleOrDefaultAsync(Arg.Any<ActiveFormDefinitionByFormIdSpec>(), Arg.Any<CancellationToken>())
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

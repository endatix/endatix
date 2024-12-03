using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.FormDefinitions.List;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.List;

public class ListFormDefinitionsHandlerTests
{
    private readonly IRepository<FormDefinition> _repository;
    private readonly ListFormDefinitionsHandler _handler;

    public ListFormDefinitionsHandlerTests()
    {
        _repository = Substitute.For<IRepository<FormDefinition>>();
        _handler = new ListFormDefinitionsHandler(_repository);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsFormDefinitions()
    {
        // Arrange
        var form = new Form("Test Form") { Id = 1 };
        var formDefinition1 = new FormDefinition(jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        var formDefinition2 =  new FormDefinition(jsonData: SampleData.FORM_DEFINITION_JSON_DATA_2);
        form.AddFormDefinition(formDefinition1);
        form.AddFormDefinition(formDefinition2);
        var formDefinitions = new List<FormDefinition>
        {
            formDefinition1,
            formDefinition2
        };
        var request = new ListFormDefinitionsQuery(1, 1, 10);
        _repository.ListAsync(
            Arg.Any<FormDefinitionsByFormIdSpec>(),
            Arg.Any<CancellationToken>()
        ).Returns(formDefinitions);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEquivalentTo(formDefinitions);
    }
}

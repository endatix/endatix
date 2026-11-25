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
    public async Task Handle_NoDefinitions_ReturnsNotFound()
    {
        // Arrange
        var request = new ListFormDefinitionsQuery(1, 1, 10);
        _repository.CountAsync(
            Arg.Any<FormDefinitionsListFilterSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(0);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);

        await _repository.DidNotReceive().ListAsync(
            Arg.Any<FormDefinitionsListSpec>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsPagedFormDefinitions()
    {
        // Arrange
        var form = Form.Create(new FormCreateArgs(TenantId: SampleData.TENANT_ID, Name: "Test Form"));
        form.Id = 1;
        var formDefinition1 = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        var formDefinition2 = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_2);
        form.AddFormDefinition(formDefinition1);
        form.AddFormDefinition(formDefinition2);
        var formDefinitions = new List<FormDefinition>
        {
            formDefinition1,
            formDefinition2
        };
        var request = new ListFormDefinitionsQuery(1, 1, 10);
        _repository.CountAsync(
            Arg.Any<FormDefinitionsListFilterSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(2);
        _repository.ListAsync(
            Arg.Any<FormDefinitionsListSpec>(),
            Arg.Any<CancellationToken>()
        ).Returns(formDefinitions);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Items.Should().BeEquivalentTo(formDefinitions);
        result.Value.TotalRecords.Should().Be(2);
        result.Value.Page.Should().Be(1);
    }
}

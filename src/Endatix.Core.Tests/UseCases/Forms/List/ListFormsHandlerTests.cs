using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Forms.List;
using FluentAssertions;
using NSubstitute;

namespace Endatix.Core.Tests.UseCases.Forms.List;

public class ListFormsHandlerTests
{
    private readonly IRepository<Form> _repository;
    private readonly ListFormsHandler _handler;

    public ListFormsHandlerTests()
    {
        _repository = Substitute.For<IRepository<Form>>();
        _handler = new ListFormsHandler(_repository);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsForms()
    {
        // Arrange
        var forms = new List<Form>
        {
            new Form("Form 1", "Description 1", true, SampleData.FORM_DEFINITION_JSON_DATA_1),
            new Form("Form 2", "Description 2", false, SampleData.FORM_DEFINITION_JSON_DATA_2)
        };
        var request = new ListFormsQuery(1, 10);
        _repository.ListAsync(Arg.Any<FormsSpec>(), Arg.Any<CancellationToken>())
                   .Returns(forms);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEquivalentTo(forms);
    }
}

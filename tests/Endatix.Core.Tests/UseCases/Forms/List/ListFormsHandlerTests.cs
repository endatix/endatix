using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Forms;
using Endatix.Core.UseCases.Forms.List;

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
        var formsList = new List<FormDto>        {
            new(){
                Name = "Form 1",
                Description = "Description 1",
                IsEnabled = true
            },
            new(){
                Name = "Form 1",
                Description = "Description 2",
                IsEnabled = true,
            }
        };
        var request = new ListFormsQuery(1, 10);
        _repository.ListAsync(Arg.Any<FormsWithSubmissionsCountSpec>(), Arg.Any<CancellationToken>())
                   .Returns(formsList);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEquivalentTo(formsList);
    }
}

using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Forms.GetById;

namespace Endatix.Core.Tests.UseCases.Forms.GetById;

public class GetFormByIdHandlerTests
{
    private readonly IRepository<Form> _repository;
    private readonly GetFormByIdHandler _handler;

    public GetFormByIdHandlerTests()
    {
        _repository = Substitute.For<IRepository<Form>>();
        _handler = new GetFormByIdHandler(_repository);
    }

    [Fact]
    public async Task Handle_FormNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        Form? notFoundForm = null;
        var request = new GetFormByIdQuery(1);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns(notFoundForm);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsForm()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1) { Id = 1 };
        var request = new GetFormByIdQuery(1);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().Be(form);
    }
}

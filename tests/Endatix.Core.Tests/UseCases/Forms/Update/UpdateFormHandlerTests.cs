using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Forms.Update;

namespace Endatix.Core.Tests.UseCases.Forms.Update;

public class UpdateFormHandlerTests
{
    private readonly IRepository<Form> _repository;
    private readonly UpdateFormHandler _handler;

    public UpdateFormHandlerTests()
    {
        _repository = Substitute.For<IRepository<Form>>();
        _handler = new UpdateFormHandler(_repository);
    }

    [Fact]
    public async Task Handle_FormNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new UpdateFormCommand(1, SampleData.FORM_NAME_1, SampleData.FORM_DESCRIPTION_1, true);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns((Form)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_UpdatesForm()
    {
        // Arrange
        var form = new Form() { Id = 1, Name = SampleData.FORM_NAME_1, Description = SampleData.FORM_DESCRIPTION_1, IsEnabled = true };
        var request = new UpdateFormCommand(1, SampleData.FORM_NAME_2, SampleData.FORM_DESCRIPTION_2, false);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(request.Name);
        result.Value.Description.Should().Be(request.Description);
        result.Value.IsEnabled.Should().Be(request.IsEnabled);
        await _repository.Received(1).UpdateAsync(form, Arg.Any<CancellationToken>());
    }
}
